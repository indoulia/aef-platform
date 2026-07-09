using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.ConversationManagement;

// The executable realization of SKL-EXE-0021 (framework/execution/skills/
// conversation-intake-and-routing.md)'s Behavior. The governed Skill spec
// is written as free-text/NL interpretation ("no fixed command grammar
// required") intended for LLM-driven execution - this classifier itself
// stays a deterministic keyword table honoring PCT-EXE-0021's Input/Output
// contract shape exactly (routing is a cheap, low-ambiguity decision;
// GA-1 spends its AI budget on the Skills that actually need judgment -
// SKL-EXE-0026/27/28/29 - not on this dispatch table). Limited to the
// capabilities this Runtime can actually dispatch to (WFL-DEL-0001,
// WFL-DEL-0004, the Delivery Execution skill trio, WFL-DEL-0006,
// WFL-DEL-0008, the repository validation path, WFL-DEL-0007 Specification
// Review, WFL-DEL-0005 Implementation Validation, WFL-DEL-0009 Repository
// Migration Planning, WFL-DEL-0011 Repository Maintenance Planning, and
// WFL-DEL-0012 Platform Lifecycle Management) - it never routes to a
// capability with no executable realization, and asks for clarification
// rather than guess. Only "upgrade" is kept AEF-qualified ("upgrade aef",
// never bare "upgrade") - Migration's and Maintenance's own worked
// examples ("upgrade this repository," "upgrade the .NET SDK") would
// otherwise collide with Platform Lifecycle's self-referential Upgrade.
// The other five verbs (install/repair/reset/backup/restore) claim no
// keyword anywhere else in this table today, so they route bare, matching
// the design's own worked examples verbatim ("Reset generated artifacts,"
// "Rollback an upgrade" - neither says "AEF").
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
        ("review specification", "WFL-DEL-0007"),
        ("merge readiness", "WFL-DEL-0005"),
        ("migrate", "WFL-DEL-0009"),
        ("migration", "WFL-DEL-0009"),
        ("modernize", "WFL-DEL-0009"),
        ("maintenance", "WFL-DEL-0011"),
        ("maintain", "WFL-DEL-0011"),
        ("hygiene", "WFL-DEL-0011"),
        ("install", "WFL-DEL-0012"),
        ("upgrade aef", "WFL-DEL-0012"),
        ("upgrade my aef", "WFL-DEL-0012"),
        ("repair", "WFL-DEL-0012"),
        ("reset", "WFL-DEL-0012"),
        ("backup", "WFL-DEL-0012"),
        ("restore", "WFL-DEL-0012"),
        ("rollback", "WFL-DEL-0012"),
        ("platform lifecycle", "WFL-DEL-0012"),
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
            "Which capability should handle this request? Supported: feature, epic, delivery, analyze, plan, validate, review specification, merge readiness, migration, maintenance, platform lifecycle (install/upgrade/repair/backup/restore/reset AEF)."
        });
    }
}
