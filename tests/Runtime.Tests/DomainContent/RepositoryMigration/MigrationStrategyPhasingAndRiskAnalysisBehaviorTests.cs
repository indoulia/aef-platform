using Runtime.Context;
using Runtime.DomainContent.RepositoryMigration;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryMigration;

public class MigrationStrategyPhasingAndRiskAnalysisBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(MigrationStrategyRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["MigrationStrategyRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Migration-Strategy", content);
    }

    private static RepositoryExecutionContext RepoContext() => new("root", "test-repo", true, false, null, null, null);

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsOpenItemAndNoPhases()
    {
        var behavior = new MigrationStrategyPhasingAndRiskAnalysisBehavior();

        var result = (MigrationStrategyResult)behavior.Execute(ContextWith(null))!;

        Assert.Empty(result.Phases);
        Assert.Contains(result.OpenItems, i => i.Contains("no migration strategy request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_RealClassification_ProducesAtLeastOnePhaseWithRevertBoundary()
    {
        var behavior = new MigrationStrategyPhasingAndRiskAnalysisBehavior();

        var result = (MigrationStrategyResult)behavior.Execute(
            ContextWith(new MigrationStrategyRequest("Technology/Framework Upgrade", "dotnet", "Not determined - requires human input.", RepoContext())))!;

        Assert.NotEmpty(result.Phases);
        Assert.Equal(1, result.Phases[0].PhaseNumber);
        Assert.Contains("Technology/Framework Upgrade", result.Phases[0].Description);
        Assert.False(string.IsNullOrWhiteSpace(result.Phases[0].RevertBoundary));
    }

    [Fact]
    public void Execute_PhasingBeyondPhaseOne_FlaggedAsOpenItem_NeverFabricated()
    {
        var behavior = new MigrationStrategyPhasingAndRiskAnalysisBehavior();

        var result = (MigrationStrategyResult)behavior.Execute(
            ContextWith(new MigrationStrategyRequest("Technology/Framework Upgrade", "dotnet", "Not determined - requires human input.", RepoContext())))!;

        Assert.Contains(result.OpenItems, i => i.Contains("Phasing beyond Phase 1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_DependencyAndCompatibilityAnalysis_RequireHumanInput_NeverFabricated()
    {
        var behavior = new MigrationStrategyPhasingAndRiskAnalysisBehavior();

        var result = (MigrationStrategyResult)behavior.Execute(
            ContextWith(new MigrationStrategyRequest("Technology/Framework Upgrade", "dotnet", "Not determined - requires human input.", RepoContext())))!;

        Assert.Contains("requires human input", result.DependencyAndBreakingChangeAnalysis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requires human input", result.CompatibilityAnalysis, StringComparison.OrdinalIgnoreCase);
    }
}
