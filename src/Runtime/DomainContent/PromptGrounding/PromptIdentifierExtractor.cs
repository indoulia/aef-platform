using System.Text.RegularExpressions;

namespace Runtime.DomainContent.PromptGrounding;

// Pulls out the things a prompt names that can be checked against a repository: backticked spans,
// paths, file names, and compound identifiers. Plain English words are never returned.
public static class PromptIdentifierExtractor
{
    private static readonly Regex Backticked = new("`([^`\r\n]+)`", RegexOptions.Compiled);
    private static readonly Regex FileName = new(@"^[\w.-]{2,}\.[A-Za-z][A-Za-z0-9]{1,4}$", RegexOptions.Compiled);
    private static readonly Regex Hump = new("[a-z0-9][A-Z]", RegexOptions.Compiled);
    private static readonly Regex Snake = new("[A-Za-z0-9]_[A-Za-z0-9]", RegexOptions.Compiled);
    private static readonly Regex IdentifierShape = new(@"^[A-Za-z_][\w.]*$", RegexOptions.Compiled);
    private static readonly Regex DotFile = new(@"^\.[A-Za-z][\w-]+(\.[\w-]+)*$", RegexOptions.Compiled);
    private static readonly char[] Edge = { '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '`', '<', '>' };

    // A leading dot is part of the name (.gitignore, .github/...); a trailing one is punctuation.
    private static readonly char[] LeadingEdge = Edge.Where(character => character != '.').ToArray();

    private static string TrimEdges(string token) => token.TrimEnd(Edge).TrimStart(LeadingEdge);

    // Product names shaped like identifiers; naming one is not a claim about the repository.
    private static readonly HashSet<string> NotIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "JavaScript", "TypeScript", "GitHub", "GitLab", "OpenAI", "PowerShell", "PostgreSQL", "MongoDB", "MySQL",
        "GraphQL", "DevOps", "NuGet", "WebSocket", "WebSockets", "iOS", "macOS", "DotNet", "NodeJS", "OAuth", "SQLite", "YouTube", "LinkedIn",
    };

    public static IReadOnlyList<string> Extract(string prompt)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string candidate)
        {
            var value = TrimEdges(candidate.Trim());
            if (value.Length > 1 && seen.Add(value))
            {
                found.Add(value);
            }
        }

        foreach (Match match in Backticked.Matches(prompt))
        {
            // A backticked phrase with a space is a command or a snippet (`npm test`), not a name to look up.
            if (!match.Groups[1].Value.Trim().Any(char.IsWhiteSpace))
            {
                Add(match.Groups[1].Value);
            }
        }

        foreach (var raw in Backticked.Replace(prompt, " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = TrimEdges(raw);
            if (IsPath(token) || IsFileName(token) || IsCompoundIdentifier(token))
            {
                Add(token);
            }
        }

        return found;
    }

    private static readonly Regex CommitPrefix = new(@"^\s*[A-Za-z]+(\((?<scope>[^)]+)\))?!?:\s*", RegexOptions.Compiled);
    private static readonly Regex Code = new(@"^[A-Z][A-Z0-9]+(-\d+)?$", RegexOptions.Compiled);
    private static readonly Regex ProperNoun = new("^[A-Z][a-z]{2,}$", RegexOptions.Compiled);
    private const int MaxSearchTerms = 6;

    // Distinctive words worth searching the repository for: a conventional-commit scope, codes and acronyms
    // (T4, QR, EPIC-138), and capitalised names that do not merely start a sentence. Unlike Extract, a term
    // that finds nothing is not a claim the prompt made, so it never becomes an open question.
    public static IReadOnlyList<string> ExtractSearchTerms(string prompt)
    {
        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var identifiers = new HashSet<string>(Extract(prompt), StringComparer.Ordinal);

        void Add(string term)
        {
            if (term.Length > 1 && terms.Count < MaxSearchTerms && !identifiers.Contains(term) && seen.Add(term))
            {
                terms.Add(term);
            }
        }

        var body = prompt;
        var prefix = CommitPrefix.Match(prompt);
        if (prefix.Success)
        {
            foreach (var scope in prefix.Groups["scope"].Value.Split(new[] { ',', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Add(scope);
            }

            body = prompt[prefix.Length..];
        }

        var sentenceStart = true;
        foreach (var raw in body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim(Edge);
            if (Code.IsMatch(token) || (!sentenceStart && ProperNoun.IsMatch(token)))
            {
                Add(token);
            }

            sentenceStart = raw.EndsWith('.') || raw.EndsWith('!') || raw.EndsWith('?');
        }

        return terms;
    }

    public static bool IsPath(string token) =>
        token.Length > 2 && (token.Contains('/') || token.Contains('\\')) && !token.Contains("://", StringComparison.Ordinal) && token.Any(char.IsLetterOrDigit);

    private static readonly HashSet<string> ProductFileLikeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Node.js", "Vue.js", "Next.js", "React.js", "Nuxt.js", "Express.js", "Three.js", "D3.js", "ASP.NET", "VB.NET", "ADO.NET", "ML.NET",
    };

    private static readonly HashSet<string> DomainSuffixes = new(StringComparer.OrdinalIgnoreCase) { "com", "org", "net", "io", "ai", "dev", "app" };

    // Whether a file-shaped token that was NOT found is worth asking the user about. "and/or", "TCP/IP", "Node.js"
    // and "example.com" are ordinary writing; asking about them adds a user round for nothing.
    public static bool IsDeliberateFileReference(string token)
    {
        var normalized = token.Replace('\\', '/');
        var lastSegment = normalized[(normalized.LastIndexOf('/') + 1)..];
        if (normalized.Contains('/'))
        {
            return normalized.StartsWith('.') || normalized.Count(character => character == '/') >= 2 || lastSegment.Contains('.');
        }

        var extension = lastSegment[(lastSegment.LastIndexOf('.') + 1)..];
        return !ProductFileLikeNames.Contains(lastSegment) && !DomainSuffixes.Contains(extension);
    }

    public static bool IsFileName(string token) => DotFile.IsMatch(token) || (FileName.IsMatch(token) && !IdentifierLooksLikeMemberAccess(token));

    private static bool IsCompoundIdentifier(string token) =>
        IdentifierShape.IsMatch(token) && !NotIdentifiers.Contains(token) && (Hump.IsMatch(token) || Snake.IsMatch(token));

    // "OrderService.Submit" is a member access, not a file called OrderService with extension .Submit.
    private static bool IdentifierLooksLikeMemberAccess(string token)
    {
        var extension = token[(token.LastIndexOf('.') + 1)..];
        return extension.Any(char.IsUpper) && extension.Any(char.IsLower);
    }
}
