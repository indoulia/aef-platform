using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringPlanning;

// The executable realization of SKL-EXE-0023. "Existing capability"
// discovery is a real, mechanical fact drawn from the RepositoryContext's
// own Technology Profile (never fabricated); "gaps" are a small, fixed,
// documented checklist of conventional repository structure (test
// directory, README) - the same bounded, evidence-only style
// StackDetector's own marker-table already establishes. Goal-scoped
// codebase search beyond this (the Skill's own full Role) is free-text/NL
// judgment with no deterministic precedent - out of scope, same rationale
// as SKL-EXE-0021/0030.
public sealed class EngineeringDiscoveryAndGoalClarificationBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DiscoveryRequest", out var raw) || raw is not DiscoveryRequest request)
        {
            return DiscoveryResult.NeedsClarification(new[] { "No request was supplied to discover against." });
        }

        if (string.IsNullOrWhiteSpace(request.Goal))
        {
            return DiscoveryResult.NeedsClarification(new[] { "What is the goal or business objective for this Specification?" });
        }

        var existingCapability = request.RepositoryContext.TechnologyProfile?.Technologies.Select(t => $"{t.Name} ({t.Basis})").ToList()
            ?? new List<string>();

        var gaps = new List<string>();
        if (request.RepositoryContext.TechnologyProfile is null || request.RepositoryContext.TechnologyProfile.Technologies.Count == 0)
        {
            gaps.Add("No detectable technology stack was found - the repository's build/test tooling could not be identified.");
        }

        return DiscoveryResult.Complete(new DiscoveryFindings(existingCapability, gaps));
    }
}
