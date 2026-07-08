namespace Runtime.Agent;

// A direct, lossless remap of Runtime.Execution.ExecutionResult's own
// evidentiary fields - deliberately not reaching into AiExecutionService's
// IEvidenceSink, since EvidenceRecord carries no request/session
// correlation id today (see the GA-2 design spec's "Known non-goal").
public sealed record ExecutionEvidence(
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string>? ValidationResults,
    string? RepositoryAnalyzed,
    IReadOnlyList<string>? FilesChanged,
    bool? TestsExecuted);
