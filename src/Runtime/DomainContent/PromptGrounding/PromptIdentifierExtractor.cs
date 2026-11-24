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
    private static readonly char[] Edge = { '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '`', '<', '>' };

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
            var value = candidate.Trim().Trim(Edge);
            if (value.Length > 1 && seen.Add(value))
            {
                found.Add(value);
            }
        }

        foreach (Match match in Backticked.Matches(prompt))
        {
            Add(match.Groups[1].Value);
        }

        foreach (var raw in Backticked.Replace(prompt, " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim(Edge);
            if (IsPath(token) || IsFileName(token) || IsCompoundIdentifier(token))
            {
                Add(token);
            }
        }

        return found;
    }

    public static bool IsPath(string token) =>
        token.Length > 2 && (token.Contains('/') || token.Contains('\\')) && !token.Contains("://", StringComparison.Ordinal) && token.Any(char.IsLetterOrDigit);

    public static bool IsFileName(string token) => FileName.IsMatch(token) && !IdentifierLooksLikeMemberAccess(token);

    private static bool IsCompoundIdentifier(string token) =>
        IdentifierShape.IsMatch(token) && !NotIdentifiers.Contains(token) && (Hump.IsMatch(token) || Snake.IsMatch(token));

    // "OrderService.Submit" is a member access, not a file called OrderService with extension .Submit.
    private static bool IdentifierLooksLikeMemberAccess(string token)
    {
        var extension = token[(token.LastIndexOf('.') + 1)..];
        return extension.Any(char.IsUpper) && extension.Any(char.IsLower);
    }
}
