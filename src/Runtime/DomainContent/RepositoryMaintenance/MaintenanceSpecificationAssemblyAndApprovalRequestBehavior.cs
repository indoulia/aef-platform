using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.RepositoryMaintenance;

// The executable realization of SKL-EXE-0044. Assembles SKL-EXE-0042/0043's
// own outputs into one SCH-DEL-0003 "Specification Type: Maintenance"
// instance, decomposes it into one Story per Maintenance Item, and
// requests Business Approval. Status is always "pending_approval" -
// mirrors MigrationSpecificationAssemblyAndApprovalRequestBehavior's own
// identical postcondition: this Behavior never self-renders the Business
// Approval decision.
public sealed class MaintenanceSpecificationAssemblyAndApprovalRequestBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("MaintenanceSpecificationAssemblyRequest", out var raw) || raw is not MaintenanceSpecificationAssemblyRequest request)
        {
            var emptySpecification = new MaintenanceSpecification(
                "Maintenance", string.Empty, Array.Empty<HealthDimensionFinding>(), Array.Empty<MaintenanceItem>(), Array.Empty<string>());
            return new MaintenanceSpecificationAssemblyResult(emptySpecification, "Requires Clarification", "No maintenance specification assembly request was supplied.", "pending_approval", Array.Empty<string>());
        }

        var decomposedStories = request.Items
            .Select(item => $"Story: {item.Description}")
            .ToList();

        var specification = new MaintenanceSpecification(
            SpecificationType: "Maintenance",
            Goal: request.Goal,
            HealthFindings: request.HealthFindings,
            Items: request.Items,
            DecomposedStories: decomposedStories);

        var readiness = request.OpenItems.Count == 0 ? "Ready for Implementation" : "Requires Clarification";
        var summary = $"{request.OpenItems.Count} open item(s). Classified '{readiness}'. {request.Items.Count} maintenance item(s) across {request.HealthFindings.Count} health dimension(s).";

        return new MaintenanceSpecificationAssemblyResult(specification, readiness, summary, "pending_approval", request.OpenItems);
    }
}
