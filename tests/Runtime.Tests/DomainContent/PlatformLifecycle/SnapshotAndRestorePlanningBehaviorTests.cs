using Runtime.Context;
using Runtime.DomainContent.PlatformLifecycle;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.PlatformLifecycle;

public class SnapshotAndRestorePlanningBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(SnapshotAndRestorePlanningRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["SnapshotAndRestorePlanningRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "PlatformLifecycle-Snapshot", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsNoDetailsWithOpenItem()
    {
        var behavior = new SnapshotAndRestorePlanningBehavior();

        var result = (SnapshotAndRestorePlanningResult)behavior.Execute(ContextWith(null))!;

        Assert.Empty(result.Details);
        Assert.Contains(result.OpenItems, i => i.Contains("no snapshot and restore planning request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_Backup_WithKnownBranch_NamesItAsTheRef()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, true, "main", true, null);
        var behavior = new SnapshotAndRestorePlanningBehavior();

        var result = (SnapshotAndRestorePlanningResult)behavior.Execute(
            ContextWith(new SnapshotAndRestorePlanningRequest("Backup", repoContext, null)))!;

        Assert.Contains(result.Details, d => d.Contains("main"));
    }

    [Fact]
    public void Execute_Backup_WithUnknownBranch_RequiresHumanInput_NeverFabricated()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new SnapshotAndRestorePlanningBehavior();

        var result = (SnapshotAndRestorePlanningResult)behavior.Execute(
            ContextWith(new SnapshotAndRestorePlanningRequest("Backup", repoContext, null)))!;

        Assert.Contains(result.OpenItems, i => i.Contains("requires human input", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_Restore_NoTargetRefSupplied_RequiresClarification()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, true, "main", true, null);
        var behavior = new SnapshotAndRestorePlanningBehavior();

        var result = (SnapshotAndRestorePlanningResult)behavior.Execute(
            ContextWith(new SnapshotAndRestorePlanningRequest("Restore", repoContext, null)))!;

        Assert.Contains(result.OpenItems, i => i.Contains("target git ref", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_Restore_WithTargetRef_NamesConsistencyChecksToRun_NeverExecutesThem()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, true, "main", true, null);
        var behavior = new SnapshotAndRestorePlanningBehavior();

        var result = (SnapshotAndRestorePlanningResult)behavior.Execute(
            ContextWith(new SnapshotAndRestorePlanningRequest("Restore", repoContext, "v1.0-backup")))!;

        Assert.Contains(result.Details, d => d.Contains("v1.0-backup"));
        Assert.Contains(result.Details, d => d.Contains("reference/registration integrity", StringComparison.OrdinalIgnoreCase));
    }
}
