using Runtime.Context;
using Runtime.DomainContent.RepositoryMaintenance;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryMaintenance;

public class MaintenanceSpecificationAssemblyAndApprovalRequestBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(MaintenanceSpecificationAssemblyRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["MaintenanceSpecificationAssemblyRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Maintenance-Assembly", content);
    }

    private static MaintenanceItem Item(string description) =>
        new(description, "Major", "Major", "Potentially breaking", "Not assessed.", false, null, "Revert this item's own Story branch/PR.");

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsEmptySpecification_RequiresClarification()
    {
        var behavior = new MaintenanceSpecificationAssemblyAndApprovalRequestBehavior();

        var result = (MaintenanceSpecificationAssemblyResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("Requires Clarification", result.ImplementationReadiness);
        Assert.Equal("pending_approval", result.Status);
        Assert.Equal("Maintenance", result.Specification.SpecificationType);
    }

    [Fact]
    public void Execute_NoOpenItems_ReadyForImplementation()
    {
        var behavior = new MaintenanceSpecificationAssemblyAndApprovalRequestBehavior();
        var request = new MaintenanceSpecificationAssemblyRequest(
            "Update NuGet packages", Array.Empty<HealthDimensionFinding>(), new[] { Item("Bump five patch-level NuGet packages") }, Array.Empty<string>());

        var result = (MaintenanceSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Ready for Implementation", result.ImplementationReadiness);
    }

    [Fact]
    public void Execute_WithOpenItems_RequiresClarification()
    {
        var behavior = new MaintenanceSpecificationAssemblyAndApprovalRequestBehavior();
        var request = new MaintenanceSpecificationAssemblyRequest(
            "Update NuGet packages", Array.Empty<HealthDimensionFinding>(), Array.Empty<MaintenanceItem>(), new[] { "Which package registry should be consulted?" });

        var result = (MaintenanceSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Requires Clarification", result.ImplementationReadiness);
    }

    [Fact]
    public void Execute_DecomposesOneStoryPerItem()
    {
        var behavior = new MaintenanceSpecificationAssemblyAndApprovalRequestBehavior();
        var request = new MaintenanceSpecificationAssemblyRequest(
            "Update NuGet packages", Array.Empty<HealthDimensionFinding>(),
            new[] { Item("Bump five patch-level NuGet packages"), Item("Upgrade the .NET SDK") }, Array.Empty<string>());

        var result = (MaintenanceSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal(2, result.Specification.DecomposedStories.Count);
    }

    [Fact]
    public void Execute_StatusAlwaysPendingApproval_NeverSelfRendered()
    {
        var behavior = new MaintenanceSpecificationAssemblyAndApprovalRequestBehavior();
        var request = new MaintenanceSpecificationAssemblyRequest(
            "Update NuGet packages", Array.Empty<HealthDimensionFinding>(), Array.Empty<MaintenanceItem>(), Array.Empty<string>());

        var result = (MaintenanceSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("pending_approval", result.Status);
    }
}
