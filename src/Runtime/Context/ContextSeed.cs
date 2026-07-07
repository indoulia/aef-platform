namespace Runtime.Context;

// The Tier 1-3 input a Host supplies when an Instance is created; Tier 0
// is carried as part of the same seed since the Manager itself holds no
// Policy/Domain-index source of its own.
//
// WorkItemId (Epic 0005, Story S33): a minimal, additive extension of
// Agent-to-step work allocation (docs/aep/P5-Orchestration-Platform-
// Architecture.md §13, previously "allocate the next step to a bound
// Agent" with no Story-level granularity) to carry a specific work-item
// pointer (e.g. "S30") alongside CurrentStepId (the Workflow's own step
// name, e.g. "Implementation") - the two answer different questions
// (which Story vs. which step) and are deliberately kept as distinct
// fields. Optional and defaulted to preserve every existing call site
// unmodified - this is wiring, not a new Runtime Service, per this
// Story's own Definition of Done.
public sealed record ContextSeed(
    Tier0Context Tier0,
    string ProjectProfileId,
    string? CurrentStepId,
    IReadOnlyDictionary<string, object>? Tier3SkillContent,
    string? WorkItemId = null);
