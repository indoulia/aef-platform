namespace Runtime.Execution;

// The structured execution result P2 requires. No governed PCT-/SCH-
// schema exists for this generic shape anywhere in the architecture - this
// is a genuine, implementation-only DTO.
public sealed record ExecutionResult(
    bool Success,
    string? ExecutedWorkflowId,
    string? ExecutedAgentId,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string>? ValidationResults,
    TimeSpan Duration,
    string? PresentedResult,
    string? SuggestedNextStep,
    string? RepositoryAnalyzed = null,
    IReadOnlyList<string>? FilesChanged = null,
    bool? TestsExecuted = null,
    ExecutionResultKind Kind = ExecutionResultKind.Unspecified,
    string? Document = null);
