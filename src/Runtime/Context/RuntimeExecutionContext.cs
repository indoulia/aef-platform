namespace Runtime.Context;

// The bounded, four-tier Execution Context (I1 §7): Tier 0 (this record's
// Tier0), Tier 1 (ProjectProfileId), Tier 2 (CurrentStepId), Tier 3
// (Tier3SkillContent, fetched on demand). Scoped to exactly one Runtime
// Instance's execution window.
public sealed record RuntimeExecutionContext(
    string InstanceId,
    Tier0Context Tier0,
    string ProjectProfileId,
    string? CurrentStepId,
    IReadOnlyDictionary<string, object> Tier3SkillContent);
