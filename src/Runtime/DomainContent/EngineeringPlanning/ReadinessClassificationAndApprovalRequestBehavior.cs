using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringPlanning;

// The executable realization of SKL-EXE-0025. status is always
// "pending_approval" per PCT-EXE-0025's own postcondition - this contract
// never self-renders the Business Approval decision. Readiness is
// classified from OpenItems/AlignmentFindings counts alone - never a
// default guess.
public sealed class ReadinessClassificationAndApprovalRequestBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ReadinessRequest", out var raw) || raw is not ReadinessRequest request)
        {
            return new ReadinessResult("Requires Clarification", "No readiness request was supplied.", "pending_approval");
        }

        var readiness = (request.OpenItems.Count, request.AlignmentFindings.Count) switch
        {
            (0, 0) => "Ready for Implementation",
            (> 0, _) => "Requires Clarification",
            _ => "Requires Redesign",
        };

        var summary = $"{request.OpenItems.Count} open item(s), {request.AlignmentFindings.Count} alignment finding(s). Classified '{readiness}'.";
        return new ReadinessResult(readiness, summary, "pending_approval");
    }
}
