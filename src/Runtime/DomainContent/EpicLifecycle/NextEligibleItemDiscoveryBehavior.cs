using Kernel.Registry;
using Runtime.Catalog;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EpicLifecycle;

// The executable realization of SKL-EXE-0012
// (framework/execution/skills/next-eligible-item-discovery.md)'s Behavior:
// read a declared candidate-and-dependency table plus live Runtime Catalog
// state; return exactly one eligible candidate, or escalate ("none
// eligible" / "ambiguous - Founder decision required"). Per this Skill's
// own Contract and epics/0004-...md's S16 acceptance criteria, this
// Behavior never writes to the candidate table under any circumstance -
// it holds no reference capable of mutating it (IReadOnlyList<CandidateItem>).
public sealed class NextEligibleItemDiscoveryBehavior : IBehavior
{
    private readonly RuntimeCatalog _catalog;
    private readonly IReadOnlyList<CandidateItem> _candidates;

    public NextEligibleItemDiscoveryBehavior(RuntimeCatalog catalog, IReadOnlyList<CandidateItem> candidates)
    {
        _catalog = catalog;
        _candidates = candidates;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        var eligible = new List<CandidateItem>();
        foreach (var candidate in _candidates)
        {
            var result = _catalog.Query(candidate.DependencyId);
            if (result.Found && result.Entry!.Status == RegistryEntryStatus.Active)
            {
                eligible.Add(candidate);
            }
        }

        return eligible.Count switch
        {
            0 => DiscoveryOutcome.NoneEligible,
            1 => DiscoveryOutcome.Eligible(eligible[0]),
            _ => DiscoveryOutcome.Ambiguous(eligible),
        };
    }
}
