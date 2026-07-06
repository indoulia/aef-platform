namespace Runtime.Context;

// I1 §7 Tier 0 — active Policy stop-conditions and Domain indexes.
public sealed record Tier0Context(IReadOnlyList<string> PolicyStopConditions, IReadOnlyList<string> DomainIndexes);
