using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ConversationManagement;

// The executable realization of SKL-EXE-0021 (framework/execution/skills/
// conversation-intake-and-routing.md)'s Behavior. The governed Skill spec
// is written as free-text/NL interpretation ("no fixed command grammar
// required") intended for LLM-driven execution - no LLM integration point
// exists anywhere in this Runtime today, and building one is out of this
// Prompt's scope. This is therefore a deterministic keyword classifier
// honoring PCT-EXE-0021's Input/Output contract shape exactly, limited to
// the capabilities this Runtime can actually dispatch to (WFL-DEL-0001,
// WFL-DEL-0004, the Delivery Execution skill trio, WFL-DEL-0006, WFL-DEL-0008,
// and the repository validation path) - it never routes to a capability
// with no executable realization, and asks for clarification rather than
// guess.
public sealed class ConversationIntakeAndRoutingBehavior : IBehavior
{
    private static readonly (string Keyword, string CapabilityId)[] KnownCapabilities =
    {
        ("epic", "WFL-DEL-0004"),
        ("feature", "WFL-DEL-0001"),
        ("deliver", "SKL-EXE-0018"),
        ("delivery", "SKL-EXE-0018"),
        ("analyze", "WFL-DEL-0008"),
        ("understand", "WFL-DEL-0008"),
        ("diagnose", "WFL-DEL-0008"),
        ("plan", "WFL-DEL-0006"),
        ("specify", "WFL-DEL-0006"),
        ("validate", "REPO-VALIDATION"),
    };

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RoutingRequest", out var raw) || raw is not RoutingRequest request)
        {
            return RoutingResult.NeedsClarification(new[] { "No request text was supplied to route." });
        }

        var lowered = request.UserRequest.ToLowerInvariant();
        foreach (var (keyword, capabilityId) in KnownCapabilities)
        {
            if (lowered.Contains(keyword))
            {
                return RoutingResult.Routed(capabilityId);
            }
        }

        return RoutingResult.NeedsClarification(new[]
        {
            "Which capability should handle this request? Supported: feature, epic, delivery, analyze, plan, validate."
        });
    }
}
