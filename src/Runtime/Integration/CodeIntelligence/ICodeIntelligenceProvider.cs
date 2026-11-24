namespace Runtime.Integration.CodeIntelligence;

// Where a symbol or file lives, and which tool said so. File is repository-relative with forward slashes.
public sealed record CodeReference(string Name, string Kind, string File, int Line, string? Signature, string Source);

public sealed record CodeLookupResult(bool Available, string? UnavailableReason, IReadOnlyList<CodeReference> References)
{
    public static CodeLookupResult Unavailable(string reason) => new(false, reason, Array.Empty<CodeReference>());

    public static CodeLookupResult Found(IReadOnlyList<CodeReference> references) => new(true, null, references);
}

// Repository facts from existing public tooling. A binding never guesses: when its tool cannot answer, it says Unavailable.
public interface ICodeIntelligenceProvider
{
    CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults);

    CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults);

    CodeLookupResult FindTests(string repositoryRoot, string file);

    CodeLookupResult FindAffected(string repositoryRoot, IReadOnlyList<string> changedFiles);
}
