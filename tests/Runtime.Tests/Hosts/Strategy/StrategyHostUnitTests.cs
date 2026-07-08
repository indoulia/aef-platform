using Kernel.Bootstrapping;
using Kernel.Governance;
using Kernel.Registry;
using Runtime.Context;
using Runtime.Hosts.Agent;
using Runtime.Hosts.Strategy;

namespace Runtime.Tests.Hosts.Strategy;

// S8 (Construction Unit 14, I3 §14) + S11 (Workflow Instance state machine
// widened to D10 §2's six states, epics/0003-feature-lifecycle-dogfooding.md).
public class StrategyHostUnitTests
{
    [Fact]
    public void Activate_WithClearedGovernance_CreatesRunningStrategyInstance()
    {
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var host = new StrategyHost(kernel.Supervisor, new ExecutionContextManager());

        var result = host.Activate(new ActivationRequest("strategy-1"), MinimalSeed());

        Assert.Equal(StrategyActivationOutcome.Created, result.Outcome);
        Assert.Equal(SessionState.Running, result.Handle!.State);
    }

    [Fact]
    public void Activate_Rejected_WhenNotRegistered()
    {
        var kernel = BootWithAllowAllPolicy();
        var host = new StrategyHost(kernel.Supervisor, new ExecutionContextManager());

        var result = host.Activate(new ActivationRequest("strategy-unregistered"), MinimalSeed());

        Assert.Equal(StrategyActivationOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AllocateStep_WithNoAllocatorConfigured_ReturnsRejected()
    {
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var handle = ActivateStrategy(new StrategyHost(kernel.Supervisor, new ExecutionContextManager()));

        var result = handle.AllocateStep("agent-1", MinimalSeed());

        Assert.Equal(AgentActivationOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AllocateStep_WithAllocatorConfigured_Delegates()
    {
        var kernel = BootWithAllowAllPolicy();
        var agentHost = new AgentHost(kernel.Supervisor, new ExecutionContextManager());
        kernel.Registry.Register(new DefinitionSubmission("agent-1", "domain-agents"));
        var strategyHost = new StrategyHost(kernel.Supervisor, new ExecutionContextManager(), new AgentHostAllocator(agentHost));
        kernel.Registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var handle = strategyHost.Activate(new ActivationRequest("strategy-1"), MinimalSeed()).Handle!;

        var result = handle.AllocateStep("agent-1", MinimalSeed());

        Assert.Equal(AgentActivationOutcome.Created, result.Outcome);
    }

    [Fact]
    public void PauseForApproval_ThenResume_IsAllowed_AndCheckpointsContext()
    {
        var contextManager = new ExecutionContextManager();
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var host = new StrategyHost(kernel.Supervisor, contextManager);
        var handle = host.Activate(new ActivationRequest("strategy-1"), MinimalSeed()).Handle!;

        Assert.True(handle.PauseForApproval());
        Assert.Equal(SessionState.PausedApproval, handle.State);
        Assert.NotNull(contextManager.GetContext(handle.InstanceId));

        Assert.True(handle.Resume());
        Assert.Equal(SessionState.Running, handle.State);
    }

    [Fact]
    public void PauseForApproval_ThenTerminate_IsAllowed()
    {
        var handle = ActivateRegisteredStrategy();

        Assert.True(handle.PauseForApproval());
        Assert.True(handle.Terminate());
        Assert.Equal(SessionState.Terminated, handle.State);
    }

    [Fact]
    public void Complete_FromPausedApproval_IsRejected_ClosedSetHasNoSuchEdge()
    {
        var handle = ActivateRegisteredStrategy();

        Assert.True(handle.PauseForApproval());
        Assert.False(handle.Complete());
        Assert.Equal(SessionState.PausedApproval, handle.State);
    }

    [Fact]
    public void PauseForClarification_ThenResume_IsAllowed_AndCheckpointsContext()
    {
        var contextManager = new ExecutionContextManager();
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var host = new StrategyHost(kernel.Supervisor, contextManager);
        var handle = host.Activate(new ActivationRequest("strategy-1"), MinimalSeed()).Handle!;

        Assert.True(handle.PauseForClarification());
        Assert.Equal(SessionState.PausedClarification, handle.State);
        Assert.NotNull(contextManager.GetContext(handle.InstanceId));

        Assert.True(handle.Resume());
        Assert.Equal(SessionState.Running, handle.State);
    }

    [Fact]
    public void PauseForClarification_ThenTerminate_IsRejected_ClosedSetHasNoSuchEdge()
    {
        var handle = ActivateRegisteredStrategy();

        Assert.True(handle.PauseForClarification());
        Assert.False(handle.Terminate());
        Assert.Equal(SessionState.PausedClarification, handle.State);
    }

    [Fact]
    public void PauseForTerminalFailure_ThenResume_IsAllowed()
    {
        var handle = ActivateRegisteredStrategy();

        Assert.True(handle.PauseForTerminalFailure());
        Assert.True(handle.Resume());
        Assert.Equal(SessionState.Running, handle.State);
    }

    [Fact]
    public void PauseForTerminalFailure_ThenTerminate_IsAllowed()
    {
        var handle = ActivateRegisteredStrategy();

        Assert.True(handle.PauseForTerminalFailure());
        Assert.True(handle.Terminate());
        Assert.Equal(SessionState.Terminated, handle.State);
    }

    [Fact]
    public void Terminate_DirectlyFromRunning_IsRejected_NoDirectEdgePerD10()
    {
        // D10 §2.2's own table has no Running -> Terminated edge — termination
        // is only reachable after a Review Decision, via a Paused-* state.
        var handle = ActivateRegisteredStrategy();

        Assert.False(handle.Terminate());
        Assert.Equal(SessionState.Running, handle.State);
    }

    [Fact]
    public void StrategyInstanceHandle_ExposesExactlyTheSessionLifecycleSurface()
    {
        var publicMethodNames = typeof(StrategyInstanceHandle)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            new[] { "AllocateStep", "Complete", "PauseForApproval", "PauseForClarification", "PauseForTerminalFailure", "Resume", "Terminate" },
            publicMethodNames);
    }

    [Fact]
    public void Activate_ConcurrentCalls_NoCorruption()
    {
        // GA-3 Capability 5's real-concurrent-HTTP-request test found this
        // class's underlying Dictionary corrupted under concurrent REST
        // requests - fixed via ConcurrentDictionary. Regression test:
        // many concurrent Activate calls against the same registered
        // strategy must not throw and every resulting instance id must
        // be unique and retrievable.
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var host = new StrategyHost(kernel.Supervisor, new ExecutionContextManager());
        const int count = 200;
        var results = new StrategyActivationResult[count];

        Parallel.For(0, count, i => results[i] = host.Activate(new ActivationRequest("strategy-1"), MinimalSeed()));

        Assert.All(results, r => Assert.Equal(StrategyActivationOutcome.Created, r.Outcome));
        Assert.Equal(count, results.Select(r => r.Handle!.InstanceId).Distinct().Count());
    }

    private static StrategyInstanceHandle ActivateRegisteredStrategy()
    {
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        return ActivateStrategy(new StrategyHost(kernel.Supervisor, new ExecutionContextManager()));
    }

    private static StrategyInstanceHandle ActivateStrategy(StrategyHost host)
    {
        return host.Activate(new ActivationRequest("strategy-1"), MinimalSeed()).Handle
               ?? throw new InvalidOperationException("Test setup requires a successful Activation.");
    }

    private static KernelHandle BootWithAllowAllPolicy() =>
        KernelBootstrapper.Boot(new KernelConfiguration(PolicyResolver: new AllowAllPolicyResolver()));

    private static ContextSeed MinimalSeed() =>
        new(new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "profile-1", CurrentStepId: null, Tier3SkillContent: null);

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class AgentHostAllocator : IAgentAllocator
    {
        private readonly AgentHost _agentHost;

        public AgentHostAllocator(AgentHost agentHost) => _agentHost = agentHost;

        public AgentActivationResult AllocateStep(string agentDefinitionId, ContextSeed seed) =>
            _agentHost.Activate(new ActivationRequest(agentDefinitionId), seed);
    }
}
