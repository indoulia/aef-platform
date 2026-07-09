using Runtime.Context;

namespace Runtime.Tests.Context;

// S5 — Execution Context Manager (Construction Unit 4, I1 §7).
public class ExecutionContextManagerUnitTests
{
    [Fact]
    public void CreateContext_BuildsFourTierContextScopedToInstance()
    {
        var manager = new ExecutionContextManager();
        var seed = new ContextSeed(
            Tier0: new Tier0Context(new[] { "stop-on-active-transition" }, new[] { "domain-execution" }),
            ProjectProfileId: "profile-1",
            CurrentStepId: "step-1",
            Tier3SkillContent: new Dictionary<string, object> { ["skill-1"] = "behavior-payload" });

        var context = manager.CreateContext("instance-1", seed);

        Assert.Equal("instance-1", context.InstanceId);
        Assert.Equal(new[] { "stop-on-active-transition" }, context.Tier0.PolicyStopConditions);
        Assert.Equal(new[] { "domain-execution" }, context.Tier0.DomainIndexes);
        Assert.Equal("profile-1", context.ProjectProfileId);
        Assert.Equal("step-1", context.CurrentStepId);
        Assert.Equal("behavior-payload", context.Tier3SkillContent["skill-1"]);
        Assert.Same(context, manager.GetContext("instance-1"));
    }

    [Fact]
    public void OnInstanceCompletedOrTerminated_DiscardsContext_ByDefault()
    {
        var manager = new ExecutionContextManager();
        manager.CreateContext("instance-1", MinimalSeed());

        manager.OnInstanceCompletedOrTerminated("instance-1");

        Assert.Null(manager.GetContext("instance-1"));
    }

    [Fact]
    public void OnInstancePaused_PreservesContext_NotDestroyed()
    {
        var manager = new ExecutionContextManager();
        manager.CreateContext("instance-1", MinimalSeed());

        manager.OnInstancePaused("instance-1");

        Assert.NotNull(manager.GetContext("instance-1"));
    }

    [Fact]
    public void OnInstanceResumed_AfterPause_ContextStillPresent_ThenTerminate_Discards()
    {
        var manager = new ExecutionContextManager();
        manager.CreateContext("instance-1", MinimalSeed());
        manager.OnInstancePaused("instance-1");

        manager.OnInstanceResumed("instance-1");
        Assert.NotNull(manager.GetContext("instance-1"));

        manager.OnInstanceCompletedOrTerminated("instance-1");
        Assert.Null(manager.GetContext("instance-1"));
    }

    [Fact]
    public void DisposalPolicy_IsSinglePluggableParameter_NotHardCoded()
    {
        var manager = new ExecutionContextManager(new RetainAlwaysPolicy());
        manager.CreateContext("instance-1", MinimalSeed());

        manager.OnInstanceCompletedOrTerminated("instance-1");

        Assert.NotNull(manager.GetContext("instance-1"));
    }

    [Fact]
    public void CreateContext_ConcurrentCalls_NoCorruption()
    {
        // GA-3 Capability 5's real-concurrent-HTTP-request test found this
        // class's underlying Dictionary corrupted under concurrent REST
        // requests - fixed via ConcurrentDictionary. Regression test:
        // many threads creating distinct contexts concurrently must not
        // throw and every context must remain retrievable afterward.
        var manager = new ExecutionContextManager();
        const int count = 200;

        Parallel.For(0, count, i => manager.CreateContext($"instance-{i}", MinimalSeed()));

        for (var i = 0; i < count; i++)
        {
            Assert.NotNull(manager.GetContext($"instance-{i}"));
        }
    }

    private static ContextSeed MinimalSeed() =>
        new(new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "profile-1", CurrentStepId: null, Tier3SkillContent: null);

    private sealed class RetainAlwaysPolicy : IContextDisposalPolicy
    {
        public bool ShouldDiscardOnCompletion(string instanceId) => false;
    }
}
