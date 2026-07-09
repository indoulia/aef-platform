using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryMigration;

// The executable realization of SKL-EXE-0035. Authoring a real, multi-phase
// migration strategy - which files change in which order, what breaks,
// what stays compatible - is free-text/NL judgment with no deterministic
// precedent (the same class of limit SpecificationDraftingAndAlignment-
// Behavior already accepts for Success Criteria/Functional Requirements).
// This Behavior always produces exactly one concrete Phase 1 (Assessment)
// - never a fabricated multi-phase plan - and explicitly defers everything
// beyond that to human input via OpenItems, never silently blank (PCT-EXE-
// 0035's own postcondition). Rollback boundary language mirrors STD-DEL-
// 0002's own one-branch-one-PR-per-Story discipline, applied per phase.
public sealed class MigrationStrategyPhasingAndRiskAnalysisBehavior : IBehavior
{
    private const string RequiresHumanInput = "Not assessed by automated analysis - requires human input.";

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("MigrationStrategyRequest", out var raw) || raw is not MigrationStrategyRequest request)
        {
            return new MigrationStrategyResult(Array.Empty<MigrationPhase>(), RequiresHumanInput, RequiresHumanInput, new[] { "No migration strategy request was supplied." });
        }

        var phaseOne = new MigrationPhase(
            PhaseNumber: 1,
            Description: $"{request.TransformationClassification} for '{request.RepositoryContext.RepositoryName}' - current state: {request.CurrentState}",
            RevertBoundary: "Revert this phase's constituent Story branches/PRs, in reverse merge order, before the next phase begins.");

        var openItems = new List<string>
        {
            "Phasing beyond Phase 1 requires human input - automated analysis cannot decompose a multi-phase migration from a goal statement alone.",
            "Dependency & Breaking-Change Analysis requires human input - automated analysis cannot assess real dependency impact without executing or reading the affected code.",
            "Compatibility Analysis requires human input - automated analysis cannot assess real compatibility impact without executing or reading the affected code.",
        };

        return new MigrationStrategyResult(new[] { phaseOne }, RequiresHumanInput, RequiresHumanInput, openItems);
    }
}
