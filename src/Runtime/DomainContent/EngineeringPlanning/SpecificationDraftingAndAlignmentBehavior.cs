using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EngineeringPlanning;

// The executable realization of SKL-EXE-0024. Authoring real prose for
// Success Criteria/Functional Requirements/Risks/Acceptance Criteria is
// free-text/NL judgment with no deterministic precedent - every
// Always-required SCH-DEL-0003 section is still populated (conformance
// requires this), but sections this Behavior cannot honestly derive state
// so explicitly and are also recorded in open_items - never silently
// blank, never invented (PCT-EXE-0024's own postcondition).
public sealed class SpecificationDraftingAndAlignmentBehavior : IBehavior
{
    private const string RequiresHumanInput = "Not determined by automated discovery - requires human input. See Open Items.";

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DraftingRequest", out var raw) || raw is not DraftingRequest request)
        {
            var emptyDraft = new SpecificationDraft("Standard", RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, RequiresHumanInput, Array.Empty<string>(), RequiresHumanInput);
            return new DraftingResult(emptyDraft, Array.Empty<string>(), new[] { "No drafting request was supplied." });
        }

        var openItems = new List<string>();
        var existingCapabilityAnalysis = request.DiscoveryFindings.ExistingCapability.Count > 0
            ? $"Existing capability, per Repository Intelligence: {string.Join("; ", request.DiscoveryFindings.ExistingCapability)}."
            : "No existing capability was detected by automated discovery.";

        foreach (var gap in request.DiscoveryFindings.Gaps)
        {
            openItems.Add(gap);
        }

        openItems.Add("Success Criteria requires human input - automated discovery cannot derive measurable success criteria from a goal statement alone.");
        openItems.Add("Functional Requirements requires human input - automated discovery cannot derive user-facing behavior from a goal statement alone.");
        openItems.Add("Acceptance Criteria requires human input - automated discovery cannot derive completion criteria from a goal statement alone.");

        var draft = new SpecificationDraft(
            SpecificationType: "Standard",
            GoalProblemStatement: request.Goal,
            SuccessCriteria: RequiresHumanInput,
            ExistingCapabilityAnalysis: existingCapabilityAnalysis,
            FunctionalRequirements: RequiresHumanInput,
            OutOfScope: "Not determined by automated discovery.",
            Risks: "Not assessed by automated discovery.",
            Assumptions: Array.Empty<string>(),
            AcceptanceCriteria: RequiresHumanInput);

        return new DraftingResult(draft, Array.Empty<string>(), openItems);
    }
}
