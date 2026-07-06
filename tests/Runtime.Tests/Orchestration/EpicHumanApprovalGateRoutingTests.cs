using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.Hosts.Strategy;
using Runtime.Orchestration;
using Xunit;

namespace Runtime.Tests.Orchestration;

// S17 - the same two Human-Approval-Gates-table entries (#2 Clarification,
// #3 Roadmap Review) routed through EpicLifecycleOrchestrator instead of
// FeatureLifecycleOrchestrator, proving the gate-routing machinery
// (StrategyHost, SessionTransitions, ReviewDecision, GateResumeResult) is
// reused verbatim, not reimplemented, for Epic-granularity content.
public class EpicHumanApprovalGateRoutingTests
{
    [Fact]
    public void SupplyClarification_FromPausedClarification_ResumesAtSameStep()
    {
        var strategy = ActivateAndPauseForClarification(out var orchestrator);
        var stepBeforeResume = strategy.Context.CurrentStepId;

        var result = orchestrator.SupplyClarification(strategy, "the missing scope detail");

        Assert.Equal(GateResumeOutcome.Resumed, result.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);
        Assert.Equal(stepBeforeResume, strategy.Context.CurrentStepId);
    }

    [Fact]
    public void ResolveRoadmapReview_Approved_ResumesToRunning()
    {
        var strategy = ActivateAndPauseForApproval(out var orchestrator);

        var result = orchestrator.ResolveRoadmapReview(strategy, new ReviewDecision(Approved: true, Reason: null));

        Assert.Equal(GateResumeOutcome.Resumed, result.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);
    }

    [Fact]
    public void ResolveRoadmapReview_Rejected_Terminates()
    {
        var strategy = ActivateAndPauseForApproval(out var orchestrator);

        var result = orchestrator.ResolveRoadmapReview(strategy, new ReviewDecision(Approved: false, Reason: "scope too broad"));

        Assert.Equal(GateResumeOutcome.Terminated, result.Outcome);
        Assert.Equal(SessionState.Terminated, strategy.State);
    }

    private static StrategyInstanceHandle ActivateAndPauseForClarification(out EpicLifecycleOrchestrator orchestrator)
    {
        var strategy = ActivateRunningStrategy(out orchestrator);
        Assert.True(strategy.PauseForClarification());
        return strategy;
    }

    private static StrategyInstanceHandle ActivateAndPauseForApproval(out EpicLifecycleOrchestrator orchestrator)
    {
        var strategy = ActivateRunningStrategy(out orchestrator);
        Assert.True(strategy.PauseForApproval());
        return strategy;
    }

    private static StrategyInstanceHandle ActivateRunningStrategy(out EpicLifecycleOrchestrator orchestrator)
    {
        var runtime = RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0006", "Execution"));
        orchestrator = new EpicLifecycleOrchestrator(runtime.StrategyHost);

        var started = orchestrator.Start(new ManualTrigger("WFL-DEL-0004", InitialInput: null));
        return started.Strategy!;
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
