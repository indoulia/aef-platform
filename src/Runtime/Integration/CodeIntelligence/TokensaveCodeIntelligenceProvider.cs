using System.Text.Json;
using System.Text.RegularExpressions;

namespace Runtime.Integration.CodeIntelligence;

// Shells out to `tokensave tool <name> --project <root> --json --args <json>`. All parameters travel as one
// JSON argument, so prompts with quotes or newlines and roots with spaces need no shell quoting.
// Never indexes: a repository without an index is reported Unavailable with the command the caller should run.
public sealed class TokensaveCodeIntelligenceProvider : ICodeIntelligenceProvider
{
    private const string Source = "tokensave";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // "- **Name** (kind) - path/file.cs:12"
    private static readonly Regex EntryPointPattern = new(@"^- \*\*(?<name>.+?)\*\* \((?<kind>[^)]+)\) - (?<file>.+):(?<line>\d+)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    // "- path/file.cs: Name:12, Other:30"
    private static readonly Regex RelatedLinePattern = new(@"^- (?<file>[^:\r\n]+\.[A-Za-z0-9]+): (?<symbols>.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex RelatedSymbolPattern = new(@"(?<name>[^,:\s][^,:]*):(?<line>\d+)", RegexOptions.Compiled);

    private readonly ProcessRunner _runner;

    public TokensaveCodeIntelligenceProvider()
        : this(ExternalProcess.Run)
    {
    }

    public TokensaveCodeIntelligenceProvider(ProcessRunner runner)
    {
        _runner = runner;
    }

    public CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults) =>
        Invoke(repositoryRoot, "context", new { task = taskDescription, max_nodes = maxResults }, text => ParseContext(text).Take(maxResults).ToList());

    public CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults) =>
        Invoke(repositoryRoot, "search", new { query = name, limit = maxResults }, text => ParseSearch(text).Take(maxResults).ToList());

    public CodeLookupResult FindTests(string repositoryRoot, string file) =>
        Invoke(repositoryRoot, "test_map", new { file = Normalize(file) }, text => ParseFileList(text, "test_files"));

    public CodeLookupResult FindAffected(string repositoryRoot, IReadOnlyList<string> changedFiles) =>
        changedFiles.Count == 0
            ? CodeLookupResult.Found(Array.Empty<CodeReference>())
            : Invoke(repositoryRoot, "affected", new { files = changedFiles.Select(Normalize).ToArray() }, text => ParseFileList(text, "affected_tests"));

    private CodeLookupResult Invoke(string repositoryRoot, string tool, object parameters, Func<string, IReadOnlyList<CodeReference>> parse)
    {
        var arguments = new[] { "tool", tool, "--project", repositoryRoot, "--json", "--args", JsonSerializer.Serialize(parameters) };

        ProcessOutput output;
        try
        {
            output = _runner(repositoryRoot, "tokensave", arguments, Timeout);
        }
        catch (Exception ex)
        {
            return CodeLookupResult.Unavailable($"tokensave could not be run: {ex.Message}");
        }

        if (output.NotFound)
        {
            return CodeLookupResult.Unavailable("tokensave is not installed or not on PATH.");
        }

        if (output.TimedOut)
        {
            return CodeLookupResult.Unavailable($"tokensave timed out after {Timeout.TotalSeconds:0} seconds.");
        }

        if (output.ExitCode != 0)
        {
            return output.StandardError.Contains("no TokenSave index", StringComparison.OrdinalIgnoreCase)
                ? CodeLookupResult.Unavailable("the repository has no tokensave index - run `tokensave init` in the repository root.")
                : CodeLookupResult.Unavailable($"tokensave exited with code {output.ExitCode}: {FirstLine(output.StandardError)}");
        }

        try
        {
            return CodeLookupResult.Found(parse(UnwrapEnvelope(output.StandardOutput)));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return CodeLookupResult.Unavailable("tokensave returned output that could not be read.");
        }
    }

    // --json wraps the tool's own text in an MCP result envelope: {"content":[{"type":"text","text":"..."}]}
    private static string UnwrapEnvelope(string standardOutput)
    {
        using var document = JsonDocument.Parse(standardOutput.TrimStart('﻿'));
        return string.Concat(document.RootElement.GetProperty("content").EnumerateArray()
            .Where(block => block.GetProperty("type").GetString() == "text")
            .Select(block => block.GetProperty("text").GetString()));
    }

    private static IEnumerable<CodeReference> ParseSearch(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.EnumerateArray().Select(item => new CodeReference(
            item.GetProperty("name").GetString() ?? string.Empty,
            item.GetProperty("kind").GetString() ?? "symbol",
            Normalize(item.GetProperty("file").GetString() ?? string.Empty),
            item.GetProperty("line").GetInt32(),
            item.TryGetProperty("signature", out var signature) && signature.ValueKind == JsonValueKind.String ? signature.GetString() : null,
            Source)).Where(reference => reference.File.Length > 0).ToList();
    }

    private static IEnumerable<CodeReference> ParseContext(string text)
    {
        if (!text.Contains("## Code Context", StringComparison.Ordinal))
        {
            throw new FormatException("Not a tokensave context document.");
        }

        var references = new List<CodeReference>();
        var seen = new HashSet<(string, int)>();

        foreach (Match match in EntryPointPattern.Matches(text))
        {
            var file = Normalize(match.Groups["file"].Value.Trim());
            var line = int.Parse(match.Groups["line"].Value);
            if (seen.Add((file, line)))
            {
                references.Add(new CodeReference(match.Groups["name"].Value, match.Groups["kind"].Value, file, line, null, Source));
            }
        }

        foreach (Match lineMatch in RelatedLinePattern.Matches(text))
        {
            var file = Normalize(lineMatch.Groups["file"].Value.Trim());
            foreach (Match symbol in RelatedSymbolPattern.Matches(lineMatch.Groups["symbols"].Value))
            {
                var line = int.Parse(symbol.Groups["line"].Value);
                if (seen.Add((file, line)))
                {
                    references.Add(new CodeReference(symbol.Groups["name"].Value.Trim(), "symbol", file, line, null, Source));
                }
            }
        }

        return references;
    }

    private static IReadOnlyList<CodeReference> ParseFileList(string text, string property)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.GetProperty(property).EnumerateArray()
            .Select(item => Normalize(item.GetString() ?? string.Empty))
            .Where(file => file.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(file => new CodeReference(Path.GetFileName(file), "test-file", file, 0, null, Source))
            .ToList();
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
}
