using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryMigration;

// The executable realization of SKL-EXE-0036. Assembles SKL-EXE-0034/0035's
// own outputs into one SCH-DEL-0003 "Specification Type: Migration"
// instance, decomposes it into an ordered list of ordinary-sized Stories
// for Lifecycle Intake (a migration is never itself one oversized Story,
// per the Migration Planning Agent's own Responsibility #6), and requests
// Business Approval. Status is always "pending_approval" - mirrors
// ReadinessClassificationAndApprovalRequestBehavior's own postcondition:
// this Behavior never self-renders the Business Approval decision.
public sealed class MigrationSpecificationAssemblyAndApprovalRequestBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("MigrationSpecificationAssemblyRequest", out var raw) || raw is not MigrationSpecificationAssemblyRequest request)
        {
            var emptySpecification = new MigrationSpecification(
                "Migration", string.Empty, "Requires Clarification", string.Empty, string.Empty,
                Array.Empty<MigrationPhase>(), string.Empty, string.Empty, string.Empty, Array.Empty<string>());
            return new MigrationSpecificationAssemblyResult(emptySpecification, "Requires Clarification", "No migration specification assembly request was supplied.", "pending_approval", Array.Empty<string>());
        }

        var decomposedStories = request.Phases
            .Select(phase => $"Phase {phase.PhaseNumber}: {phase.Description}")
            .ToList();

        var rollbackPlan = request.Phases.Count > 0
            ? string.Join(" ", request.Phases.Select(p => $"Phase {p.PhaseNumber}: {p.RevertBoundary}"))
            : "Not determined - no phase was supplied to derive a rollback boundary from.";

        var specification = new MigrationSpecification(
            SpecificationType: "Migration",
            Goal: request.Goal,
            TransformationClassification: request.TransformationClassification,
            CurrentState: request.CurrentState,
            TargetState: request.TargetState,
            Phases: request.Phases,
            DependencyAndBreakingChangeAnalysis: request.DependencyAndBreakingChangeAnalysis,
            CompatibilityAnalysis: request.CompatibilityAnalysis,
            RollbackPlan: rollbackPlan,
            DecomposedStories: decomposedStories);

        var readiness = request.OpenItems.Count == 0 ? "Ready for Implementation" : "Requires Clarification";
        var summary = $"{request.OpenItems.Count} open item(s). Classified '{readiness}'. Transformation: {request.TransformationClassification}, {request.Phases.Count} phase(s).";

        return new MigrationSpecificationAssemblyResult(specification, readiness, summary, "pending_approval", request.OpenItems);
    }
}
