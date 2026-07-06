namespace Runtime.DomainContent.EpicLifecycle;

public enum DiscoveryStatus
{
    Eligible,
    NoneEligible,
    Ambiguous,
}

// SKL-EXE-0012's three declared outputs
// (framework/execution/skills/next-eligible-item-discovery.md Behavior
// steps 3-5): exactly one eligible candidate, or an explicit "none
// eligible" result, or an explicit "ambiguous - Founder decision
// required" result carrying every tied candidate. Never a guess.
public sealed record DiscoveryOutcome(DiscoveryStatus Status, CandidateItem? Candidate, IReadOnlyList<CandidateItem>? AmbiguousCandidates)
{
    public static DiscoveryOutcome Eligible(CandidateItem candidate) => new(DiscoveryStatus.Eligible, candidate, null);

    public static readonly DiscoveryOutcome NoneEligible = new(DiscoveryStatus.NoneEligible, null, null);

    public static DiscoveryOutcome Ambiguous(IReadOnlyList<CandidateItem> candidates) => new(DiscoveryStatus.Ambiguous, null, candidates);
}
