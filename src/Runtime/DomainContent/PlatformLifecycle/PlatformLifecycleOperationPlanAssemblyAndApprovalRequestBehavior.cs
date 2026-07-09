using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.PlatformLifecycle;

// The executable realization of SKL-EXE-0049. Assembles a SCH-DEL-0007
// "Platform Lifecycle Operation Plan" instance and requests Human/Founder
// Approval. Status is always "pending_approval" - this Behavior never
// self-renders the approval decision, mirroring every sibling Assembly
// Behavior's own identical postcondition. Names, informationally, the two
// new POL-GOV-0002 Escalation Authority conditions this design introduces
// (a framework major-version self-Upgrade, and any Reset/Restore beyond
// the Runtime Catalog projection) - this Behavior classifies and reports,
// it never itself enforces a Policy, since no Policy-evaluation engine
// exists anywhere in this Runtime.
public sealed class PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("PlatformLifecycleOperationPlanAssemblyRequest", out var raw) || raw is not PlatformLifecycleOperationPlanAssemblyRequest request)
        {
            var emptyPlan = new PlatformLifecycleOperationPlan("Requires Clarification", string.Empty, string.Empty, Array.Empty<string>());
            return new PlatformLifecycleOperationPlanAssemblyResult(emptyPlan, "Requires Clarification", "No platform lifecycle operation plan assembly request was supplied.", "pending_approval", Array.Empty<string>());
        }

        var plan = new PlatformLifecycleOperationPlan(
            OperationType: request.OperationType,
            Goal: request.Goal,
            TargetContext: request.RepositoryContext.RepositoryName,
            Details: request.Details);

        var readiness = request.OpenItems.Count == 0 ? "Ready for Implementation" : "Requires Clarification";
        var approvalTier = ApprovalTierFor(request.OperationType);
        var summary = $"{request.OpenItems.Count} open item(s). Classified '{readiness}'. Operation: {request.OperationType}, {request.Details.Count} detail(s). {approvalTier}";

        return new PlatformLifecycleOperationPlanAssemblyResult(plan, readiness, summary, "pending_approval", request.OpenItems);
    }

    // Informational only, per the class comment above.
    private static string ApprovalTierFor(string operationType) => operationType switch
    {
        "Upgrade" => "A framework major-version self-Upgrade always requires Founder-level Human Approval, never a delegated Escalation Authority.",
        "Reset" or "Restore" => "Any Reset/Restore beyond the Runtime Catalog projection always requires Human Approval, regardless of other Grant conditions.",
        _ => "Requires ordinary Human/Business Approval before any human-directed execution.",
    };
}
