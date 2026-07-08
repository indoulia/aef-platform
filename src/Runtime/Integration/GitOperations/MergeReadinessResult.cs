namespace Runtime.Integration.GitOperations;

// Evidence-only merge readiness (Architecture Decision 2): never a
// mutating dry-run merge. Reasons lists every unmet condition, never
// silently absent.
public sealed record MergeReadinessResult(bool IsReady, IReadOnlyList<string> Reasons);
