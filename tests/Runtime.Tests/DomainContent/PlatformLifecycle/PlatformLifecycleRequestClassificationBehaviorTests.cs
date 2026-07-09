using Runtime.Context;
using Runtime.DomainContent.PlatformLifecycle;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.PlatformLifecycle;

public class PlatformLifecycleRequestClassificationBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(OperationClassificationRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["OperationClassificationRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "PlatformLifecycle-Classification", content);
    }

    private static RepositoryExecutionContext RepoContext() => new("root", "aef-platform", true, false, null, null, null);

    [Fact]
    public void Execute_NoRequestSupplied_RequiresClarification()
    {
        var behavior = new PlatformLifecycleRequestClassificationBehavior();

        var result = (OperationClassificationResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("Requires Clarification", result.OperationType);
    }

    [Theory]
    [InlineData("Install AEF into this repository", "Installation")]
    [InlineData("Upgrade AEF from v1.0 to v1.1", "Upgrade")]
    [InlineData("Repair my AEF installation", "Repair")]
    [InlineData("Reset generated artifacts", "Reset")]
    [InlineData("Backup AEF now", "Backup")]
    [InlineData("Restore AEF from the last backup", "Restore")]
    public void Execute_KnownGoal_ClassifiesCorrectly(string goal, string expected)
    {
        var behavior = new PlatformLifecycleRequestClassificationBehavior();

        var result = (OperationClassificationResult)behavior.Execute(
            ContextWith(new OperationClassificationRequest(goal, RepoContext())))!;

        Assert.Equal(expected, result.OperationType);
    }

    [Fact]
    public void Execute_AmbiguousGoal_RequiresClarification_NeverGuesses()
    {
        var behavior = new PlatformLifecycleRequestClassificationBehavior();

        var result = (OperationClassificationResult)behavior.Execute(
            ContextWith(new OperationClassificationRequest("Do something to AEF", RepoContext())))!;

        Assert.Equal("Requires Clarification", result.OperationType);
        Assert.Contains(result.OpenItems, i => i.Contains("Installation", StringComparison.OrdinalIgnoreCase));
    }
}
