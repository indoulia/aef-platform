using Runtime.DomainContent.Onboarding;

namespace Runtime.Execution;

// One target repository's assessed state (P3 §1: Repository Context) -
// distinct from both RuntimeExecutionContext (the four-tier per-instance
// concept) and EngineeringExecutionContext (P2's session-level concept).
// TechnologyProfile reuses AGT-EXE-0007's own TechnologyProfileDraft
// verbatim (Architecture Decision 4) - AGT-EXE-0015's own Role #1
// ("consume, never re-derive") is honored structurally: nothing in
// Runtime.DomainContent.RepositoryIntelligence ever calls StackDetector
// itself, only this factory does.
public sealed record RepositoryExecutionContext(
    string RepositoryRoot,
    string RepositoryName,
    bool IsReadable,
    bool IsGitRepository,
    string? CurrentBranch,
    bool? IsWorkingTreeClean,
    TechnologyProfileDraft? TechnologyProfile,
    IReadOnlyList<string>? ChangedPaths = null);
