using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryMaintenance;

// The executable realization of SKL-EXE-0043. Formulates one Maintenance
// Item per Health finding classified WARNING or FAIL - PASS/UNKNOWN
// findings never become Items, since there is nothing resolvable to act
// on. Severity mirrors the finding's own classification (Critical|Major,
// SCH-DEL-0004's vocabulary). Risk classification always defaults to the
// design's own stated conservative default, "Potentially breaking" - this
// Behavior never assumes Non-breaking without a resolvable check, and no
// such check exists in this Runtime yet. Effort is a required qualitative
// note, never a fabricated numeric estimate. Sequencing is left unset -
// recorded only where a real dependency between items is known, never
// invented from two unrelated findings.
public sealed class MaintenancePlanningRiskAndSequencingBehavior : IBehavior
{
    private const string RollbackNote = "Revert this item's own Story branch/PR, per STD-DEL-0002's one-branch-one-PR-per-Story discipline.";

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("MaintenancePlanningRequest", out var raw) || raw is not MaintenancePlanningRequest request)
        {
            return new MaintenancePlanningResult(Array.Empty<MaintenanceItem>(), new[] { "No maintenance planning request was supplied." });
        }

        var items = request.HealthFindings
            .Where(f => f.Classification is "WARNING" or "FAIL")
            .Select(finding => new MaintenanceItem(
                Description: $"{finding.Dimension}: {finding.Rationale}",
                Severity: finding.Classification == "FAIL" ? "Critical" : "Major",
                Priority: finding.Classification == "FAIL" ? "Critical" : "Major",
                RiskClassification: "Potentially breaking",
                EffortNote: "Effort not assessed by automated analysis - requires human input to scope the actual change.",
                SafeToAutomate: false,
                Sequencing: null,
                RollbackNote: RollbackNote))
            .ToList();

        var openItems = new List<string>();
        if (items.Count == 0)
        {
            openItems.Add("No Maintenance Items were derived - every Health finding is PASS or UNKNOWN; supply a resolved WARNING/FAIL finding to generate a Maintenance Item.");
        }

        return new MaintenancePlanningResult(items, openItems);
    }
}
