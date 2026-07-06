using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.Hosts.Strategy;
using Runtime.Orchestration;
using Xunit;

namespace Runtime.Tests.Orchestration;

// S13 (epics/0003-feature-lifecycle-dogfooding.md) — the Manual-trigger shape
// (docs/discovery/orchestration/01-orchestrator-design.md §3, §3.1): a
// trigger names only the target Workflow and its first step's input, never a
// full script of subsequent steps. Realizes WFL-DEL-0001's own Step 1:
// "AGT-EXE-0002 invokes SKL-EXE-0004 (Intake & Clarification)."
public class FeatureLifecycleOrchestratorTests
{
    private const string OrchestratorId = "ORC-EXE-0001";
    private const string WorkflowId = "WFL-DEL-0001";
    private const string FirstStepAgentId = "AGT-EXE-0002";

    [Fact]
    public void Start_WithSupportedWorkflowId_StartsInstanceRunning_AndAllocatesFirstStepToAgent()
    {
        var runtime = RuntimeBootstrapper.Boot(AllowAllConfiguration());
        RegisterOrchestratorAndAgent(runtime);
        var orchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);

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
        var orchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);

        var result = orchestrator.Start(new ManualTrigger("WFL-DEL-0002", InitialInput: null));

        Assert.Equal(OrchestrationStartOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Start_WhenOrchestratorNotRegistered_ReturnsRejected()
    {
        var runtime = RuntimeBootstrapper.Boot(AllowAllConfiguration());
        runtime.Kernel.Registry.Register(new DefinitionSubmission(FirstStepAgentId, "Execution"));
        var orchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);

        var result = orchestrator.Start(new ManualTrigger(WorkflowId, InitialInput: null));

        Assert.Equal(OrchestrationStartOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Start_WhenFirstStepAgentNotRegistered_ReturnsRejected()
    {
        var runtime = RuntimeBootstrapper.Boot(AllowAllConfiguration());
        runtime.Kernel.Registry.Register(new DefinitionSubmission(OrchestratorId, "Execution"));
        var orchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);

        var result = orchestrator.Start(new ManualTrigger(WorkflowId, InitialInput: null));

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
