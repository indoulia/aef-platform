using System.Text.RegularExpressions;
using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.PlatformLifecycle;

// The executable realization of SKL-EXE-0045. Classifies the six operation
// types via a deterministic keyword classifier - the same house convention
// ConversationIntakeAndRoutingBehavior and Migration's own Transformation
// Classification already established for a cheap, low-ambiguity decision:
// never guess a category from an ambiguous goal - ask for clarification
// instead. The actual delegation this Skill's own design names (Upgrade to
// AGT-EXE-0016, Installation to CU-3's onboarding sequence) is performed by
// EngineeringExecutionPipeline's own dispatch, not by this Behavior - the
// same "Behaviors classify, the Pipeline composes" separation every prior
// sibling capability already follows.
public sealed class PlatformLifecycleRequestClassificationBehavior : IBehavior
{
    private static readonly (string Keyword, string OperationType)[] KnownOperations =
    {
        ("install", "Installation"),
        ("upgrade", "Upgrade"),
        ("repair", "Repair"),
        ("reset", "Reset"),
        ("restore", "Restore"),
        ("rollback", "Restore"),
        ("backup", "Backup"),
    };

    private const string SupportedOperations = "Installation, Upgrade, Repair, Reset, Backup, Restore";

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("OperationClassificationRequest", out var raw) || raw is not OperationClassificationRequest request)
        {
            return new OperationClassificationResult("Requires Clarification", new[] { "No operation classification request was supplied." });
        }

        var lowered = request.Goal.ToLowerInvariant();
        foreach (var (keyword, operationType) in KnownOperations)
        {
            if (Regex.IsMatch(lowered, $@"\b{keyword}\b"))
            {
                return new OperationClassificationResult(operationType, Array.Empty<string>());
            }
        }

        return new OperationClassificationResult("Requires Clarification", new[] { $"Which lifecycle operation applies? Supported: {SupportedOperations}." });
    }
}
