using Runtime.Context;
using Runtime.DomainContent.PlatformLifecycle;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.PlatformLifecycle;

public class PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(PlatformLifecycleOperationPlanAssemblyRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["PlatformLifecycleOperationPlanAssemblyRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "PlatformLifecycle-Assembly", content);
    }

    private static RepositoryExecutionContext RepoContext() => new("root", "aef-platform", true, false, null, null, null);

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsEmptyPlan_RequiresClarification()
    {
        var behavior = new PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior();

        var result = (PlatformLifecycleOperationPlanAssemblyResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("Requires Clarification", result.ImplementationReadiness);
        Assert.Equal("pending_approval", result.Status);
    }

    [Fact]
    public void Execute_NoOpenItems_ReadyForImplementation()
    {
        var behavior = new PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior();
        var request = new PlatformLifecycleOperationPlanAssemblyRequest("Reset generated artifacts", "Reset", RepoContext(), new[] { "[Derived Projection] Runtime Catalog." }, Array.Empty<string>());

        var result = (PlatformLifecycleOperationPlanAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Ready for Implementation", result.ImplementationReadiness);
    }

    [Fact]
    public void Execute_WithOpenItems_RequiresClarification()
    {
        var behavior = new PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior();
        var request = new PlatformLifecycleOperationPlanAssemblyRequest("Install AEF", "Installation", RepoContext(), Array.Empty<string>(), new[] { "Onboarding sequence not invoked by this Runtime." });

        var result = (PlatformLifecycleOperationPlanAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Requires Clarification", result.ImplementationReadiness);
    }

    [Fact]
    public void Execute_StatusAlwaysPendingApproval_NeverSelfRendered()
    {
        var behavior = new PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior();
        var request = new PlatformLifecycleOperationPlanAssemblyRequest("Reset generated artifacts", "Reset", RepoContext(), Array.Empty<string>(), Array.Empty<string>());

        var result = (PlatformLifecycleOperationPlanAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("pending_approval", result.Status);
    }

    [Fact]
    public void Execute_UpgradeOperation_ApprovalSummaryNamesFounderLevelRequirement()
    {
        var behavior = new PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior();
        var request = new PlatformLifecycleOperationPlanAssemblyRequest("Upgrade AEF", "Upgrade", RepoContext(), Array.Empty<string>(), Array.Empty<string>());

        var result = (PlatformLifecycleOperationPlanAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Contains("Founder", result.ApprovalSummary);
    }

    [Fact]
    public void Execute_RestoreOperation_ApprovalSummaryNamesHumanApprovalRequirement()
    {
        var behavior = new PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior();
        var request = new PlatformLifecycleOperationPlanAssemblyRequest("Restore AEF", "Restore", RepoContext(), Array.Empty<string>(), Array.Empty<string>());

        var result = (PlatformLifecycleOperationPlanAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Contains("Human Approval", result.ApprovalSummary);
    }
}
