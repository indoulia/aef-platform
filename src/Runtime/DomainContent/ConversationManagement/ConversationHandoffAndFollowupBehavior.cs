using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ConversationManagement;

// The executable realization of SKL-EXE-0022 (framework/execution/skills/
// conversation-handoff-and-followup.md)'s Behavior: translates a routing
// decision plus (if any) its dispatch outcome into plain-language
// presentation, per PCT-EXE-0022's Output shape (presented_result,
// pause_reason equivalent, suggested_next_step). Never touches
// Runtime.Execution types directly.
public sealed class ConversationHandoffAndFollowupBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("HandoffRequest", out var raw) || raw is not HandoffRequest handoff)
        {
            return new HandoffResult("No handoff payload was supplied.", null);
        }

        if (handoff.RoutingResult.Outcome == RoutingOutcome.NeedsClarification)
        {
            var questions = string.Join(" ", handoff.RoutingResult.ClarificationQuestions ?? Array.Empty<string>());
            return new HandoffResult($"I need more information: {questions}", "Answer the clarification question(s) above and resubmit.");
        }

        if (handoff.DispatchOutcome is null)
        {
            return new HandoffResult("Routing succeeded but no dispatch outcome was supplied.", null);
        }

        return handoff.DispatchOutcome.Success
            ? new HandoffResult($"Completed via {handoff.DispatchOutcome.ExecutedCapabilityId}.", "No further action needed.")
            : new HandoffResult($"Execution failed: {string.Join("; ", handoff.DispatchOutcome.Diagnostics)}", "Review the diagnostics and retry.");
    }
}
