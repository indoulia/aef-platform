using Runtime.Context;
using Runtime.DomainContent.RepositoryMaintenance;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryMaintenance;

public class MaintenancePlanningRiskAndSequencingBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(MaintenancePlanningRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["MaintenancePlanningRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Maintenance-Planning", content);
    }

    private static RepositoryExecutionContext RepoContext() => new("root", "test-repo", true, false, null, null, null);

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsNoItemsWithOpenItem()
    {
        var behavior = new MaintenancePlanningRiskAndSequencingBehavior();

        var result = (MaintenancePlanningResult)behavior.Execute(ContextWith(null))!;

        Assert.Empty(result.Items);
        Assert.Contains(result.OpenItems, i => i.Contains("no maintenance planning request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_AllUnknownFindings_ProducesNoItems_AddsOpenItem()
    {
        var findings = new[] { new HealthDimensionFinding("Dependency health", "UNKNOWN", "No Automation resolves.") };
        var behavior = new MaintenancePlanningRiskAndSequencingBehavior();

        var result = (MaintenancePlanningResult)behavior.Execute(
            ContextWith(new MaintenancePlanningRequest(findings, RepoContext())))!;

        Assert.Empty(result.Items);
        Assert.Contains(result.OpenItems, i => i.Contains("No Maintenance Items were derived", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_PassFinding_NeverProducesItem()
    {
        var findings = new[] { new HealthDimensionFinding("Build health", "PASS", "Build is current.") };
        var behavior = new MaintenancePlanningRiskAndSequencingBehavior();

        var result = (MaintenancePlanningResult)behavior.Execute(
            ContextWith(new MaintenancePlanningRequest(findings, RepoContext())))!;

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Execute_FailFinding_ProducesCriticalItem_RiskDefaultsPotentiallyBreaking()
    {
        var findings = new[] { new HealthDimensionFinding("SDK health", "FAIL", "SDK is out of support.") };
        var behavior = new MaintenancePlanningRiskAndSequencingBehavior();

        var result = (MaintenancePlanningResult)behavior.Execute(
            ContextWith(new MaintenancePlanningRequest(findings, RepoContext())))!;

        var item = Assert.Single(result.Items);
        Assert.Equal("Critical", item.Severity);
        Assert.Equal("Potentially breaking", item.RiskClassification);
        Assert.False(item.SafeToAutomate);
    }

    [Fact]
    public void Execute_WarningFinding_ProducesMajorItem()
    {
        var findings = new[] { new HealthDimensionFinding("Dependency health", "WARNING", "Three packages are one minor version behind.") };
        var behavior = new MaintenancePlanningRiskAndSequencingBehavior();

        var result = (MaintenancePlanningResult)behavior.Execute(
            ContextWith(new MaintenancePlanningRequest(findings, RepoContext())))!;

        var item = Assert.Single(result.Items);
        Assert.Equal("Major", item.Severity);
    }

    [Fact]
    public void Execute_EveryItem_CarriesRollbackNote_NeverBlank()
    {
        var findings = new[] { new HealthDimensionFinding("SDK health", "FAIL", "SDK is out of support.") };
        var behavior = new MaintenancePlanningRiskAndSequencingBehavior();

        var result = (MaintenancePlanningResult)behavior.Execute(
            ContextWith(new MaintenancePlanningRequest(findings, RepoContext())))!;

        Assert.All(result.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.RollbackNote)));
    }

    [Fact]
    public void Execute_MultipleActionableFindings_ProducesOneItemPerFinding()
    {
        var findings = new[]
        {
            new HealthDimensionFinding("SDK health", "FAIL", "SDK is out of support."),
            new HealthDimensionFinding("Dependency health", "WARNING", "Packages are behind."),
            new HealthDimensionFinding("Build health", "PASS", "Build is current."),
        };
        var behavior = new MaintenancePlanningRiskAndSequencingBehavior();

        var result = (MaintenancePlanningResult)behavior.Execute(
            ContextWith(new MaintenancePlanningRequest(findings, RepoContext())))!;

        Assert.Equal(2, result.Items.Count);
    }
}
