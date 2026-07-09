using Runtime.Context;
using Runtime.DomainContent.RepositoryMigration;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryMigration;

public class MigrationSpecificationAssemblyAndApprovalRequestBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(MigrationSpecificationAssemblyRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["MigrationSpecificationAssemblyRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Migration-Assembly", content);
    }

    private static readonly MigrationPhase PhaseOne = new(1, "Technology/Framework Upgrade for 'test-repo'", "Revert Phase 1's Story branches/PRs before Phase 2 begins.");

    [Fact]
    public void Execute_NoRequestSupplied_StatusIsPendingApproval_NeverSelfRendered()
    {
        var behavior = new MigrationSpecificationAssemblyAndApprovalRequestBehavior();

        var result = (MigrationSpecificationAssemblyResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("pending_approval", result.Status);
    }

    [Fact]
    public void Execute_RealAssembly_ProducesMigrationSpecificationType()
    {
        var behavior = new MigrationSpecificationAssemblyAndApprovalRequestBehavior();

        var request = new MigrationSpecificationAssemblyRequest(
            "Upgrade this repository to .NET 10", "Technology/Framework Upgrade", "dotnet 8", "Not determined - requires human input.",
            new[] { PhaseOne }, "Not assessed - requires human input.", "Not assessed - requires human input.", Array.Empty<string>());

        var result = (MigrationSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Migration", result.Specification.SpecificationType);
        Assert.Equal("Technology/Framework Upgrade", result.Specification.TransformationClassification);
    }

    [Fact]
    public void Execute_DecomposesOnePhase_IntoAtLeastOneOrdinaryStory()
    {
        var behavior = new MigrationSpecificationAssemblyAndApprovalRequestBehavior();

        var request = new MigrationSpecificationAssemblyRequest(
            "Upgrade this repository to .NET 10", "Technology/Framework Upgrade", "dotnet 8", "Not determined - requires human input.",
            new[] { PhaseOne }, "Not assessed - requires human input.", "Not assessed - requires human input.", Array.Empty<string>());

        var result = (MigrationSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.NotEmpty(result.Specification.DecomposedStories);
        Assert.Contains("Phase 1", result.Specification.DecomposedStories[0]);
    }

    [Fact]
    public void Execute_RollbackPlan_NamesEveryPhasesRevertBoundary()
    {
        var behavior = new MigrationSpecificationAssemblyAndApprovalRequestBehavior();

        var request = new MigrationSpecificationAssemblyRequest(
            "Upgrade this repository to .NET 10", "Technology/Framework Upgrade", "dotnet 8", "Not determined - requires human input.",
            new[] { PhaseOne }, "Not assessed - requires human input.", "Not assessed - requires human input.", Array.Empty<string>());

        var result = (MigrationSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Contains(PhaseOne.RevertBoundary, result.Specification.RollbackPlan);
    }

    [Fact]
    public void Execute_NoOpenItemsInherited_ClassifiesReadyForImplementation()
    {
        var behavior = new MigrationSpecificationAssemblyAndApprovalRequestBehavior();

        var request = new MigrationSpecificationAssemblyRequest(
            "Upgrade this repository to .NET 10", "Technology/Framework Upgrade", "dotnet 8", "Not determined - requires human input.",
            new[] { PhaseOne }, "Not assessed - requires human input.", "Not assessed - requires human input.", Array.Empty<string>());

        var result = (MigrationSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Ready for Implementation", result.ImplementationReadiness);
    }

    [Fact]
    public void Execute_InheritedOpenItems_ClassifiesRequiresClarification_NeverFabricatesReadiness()
    {
        var behavior = new MigrationSpecificationAssemblyAndApprovalRequestBehavior();

        var request = new MigrationSpecificationAssemblyRequest(
            "Upgrade this repository to .NET 10", "Requires Clarification", "dotnet 8", "Not determined - requires human input.",
            new[] { PhaseOne }, "Not assessed - requires human input.", "Not assessed - requires human input.",
            new[] { "Which transformation category applies?" });

        var result = (MigrationSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Requires Clarification", result.ImplementationReadiness);
        Assert.NotEmpty(result.OpenItems);
    }

    [Fact]
    public void Execute_AlwaysRequestsBusinessApproval_NeverSelfRenders()
    {
        var behavior = new MigrationSpecificationAssemblyAndApprovalRequestBehavior();

        var request = new MigrationSpecificationAssemblyRequest(
            "Upgrade this repository to .NET 10", "Technology/Framework Upgrade", "dotnet 8", "Not determined - requires human input.",
            new[] { PhaseOne }, "Not assessed - requires human input.", "Not assessed - requires human input.", Array.Empty<string>());

        var result = (MigrationSpecificationAssemblyResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("pending_approval", result.Status);
    }
}
