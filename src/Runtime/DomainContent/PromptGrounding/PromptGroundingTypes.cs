using Runtime.Execution;
using Runtime.Integration.CodeIntelligence;

namespace Runtime.DomainContent.PromptGrounding;

public sealed record PromptGroundingRequest(string UserPrompt, RepositoryExecutionContext Repository);

// A statement plus the file that proves it. A statement with no evidencing file is not a fact here.
public sealed record EvidencedFact(string Statement, string File, int? Line, string Source);

public sealed record GroundedBrief(
    string Goal,
    IReadOnlyList<EvidencedFact> VerifiedFacts,
    IReadOnlyList<CodeReference> WhereToWork,
    IReadOnlyList<string> TestFiles,
    string? BuildAndTestCommand,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> AcceptanceChecks,
    int OmittedCount,
    // Files named in the prompt or matched by two or more distinct prompt terms. Everything else under
    // WhereToWork is a search match whose relevance is unconfirmed.
    IReadOnlyList<string>? StrongFiles = null);
