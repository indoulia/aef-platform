using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.Hosts.Strategy;
using Runtime.Orchestration;
using Xunit;

namespace Runtime.Tests.Orchestration;

// S17 - a new, Epic-scoped orchestrator mirroring FeatureLifecycleOrchestrator
// exactly (same StrategyHost/SessionTransitions/gate-routing machinery,
// unmodified), bound to WFL-DEL-0004/AGT-EXE-0006 instead of
// WFL-DEL-0001/AGT-EXE-0002. FeatureLifecycleOrchestrator's own file is
// untouched by this Story (confirmed by inspection).
public class EpicLifecycleOrchestratorTests
{
    private const string OrchestratorId = "ORC-EXE-0001";
    private const string WorkflowId = "WFL-DEL-0004";
    private const string FirstStepAgentId = "AGT-EXE-0006";

    [Fact]
    public void Start_WithSupportedWorkflowId_StartsInstanceRunning_AndAllocatesFirstStepToAgent()
    {
        var runtime = RuntimeBootstrapper.Boot(AllowAllConfiguration());
        RegisterOrchestratorAndAgent(runtime);
        var orchestrator = new EpicLifecycleOrchestrator(runtime.StrategyHost);

        var result = orchestrator.Start(new ManualTrigger(WorkflowId, InitialInput: null));

        Assert.Equal(OrchestrationStartOutcome.Started, result.Outcome);
        Assert.Equal(SessionState.Running, result.Strategy!.State);
        Assert.NotNull(result.FirstStepAgent);
    }

    [Fact]
    public void Start_WithUnsupportedWorkflowId_ReturnsRejected()
    {
        var runtime = RuntimeBootstrapper.Boot(AllowAllConfiguration());
        RegisterOrchestratorAndAgent(runtime);
        var orchestrator = new EpicLifecycleOrchestrator(runtime.StrategyHost);

        var result = orchestrator.Start(new ManualTrigger("WFL-DEL-0001", InitialInput: null));

        Assert.Equal(OrchestrationStartOutcome.Rejected, result.Outcome);
    }

    private static void RegisterOrchestratorAndAgent(RuntimeHandle runtime)
    {
        runtime.Kernel.Registry.Register(new DefinitionSubmission(OrchestratorId, "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission(FirstStepAgentId, "Execution"));
    }

    private static RuntimeConfiguration AllowAllConfiguration() =>
        new(PolicyResolver: new AllowAllPolicyResolver());

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
