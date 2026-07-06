namespace Runtime.Context;

// The Tier 1-3 input a Host supplies when an Instance is created; Tier 0
// is carried as part of the same seed since the Manager itself holds no
// Policy/Domain-index source of its own.
public sealed record ContextSeed(
    Tier0Context Tier0,
    string ProjectProfileId,
    string? CurrentStepId,
    IReadOnlyDictionary<string, object>? Tier3SkillContent);
