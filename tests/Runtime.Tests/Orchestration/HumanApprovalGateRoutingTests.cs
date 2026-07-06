using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.Hosts.Strategy;
using Runtime.Orchestration;
using Xunit;

namespace Runtime.Tests.Orchestration;

// S14 (epics/0003-feature-lifecycle-dogfooding.md) — Human-Approval-Gates
// table entries #2 (Clarification) and #3 (Roadmap Review)
// (docs/discovery/orchestration/09-human-approval-gates.md §1), routed
// through the widened state machine (S11) via the Orchestrator (S13).
public class HumanApprovalGateRoutingTests
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
    public void SupplyClarification_WhenNotPausedForClarification_IsRejected()
    {
        var strategy = ActivateRunningStrategy(out var orchestrator);

        var result = orchestrator.SupplyClarification(strategy, "irrelevant");

        Assert.Equal(GateResumeOutcome.Rejected, result.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);
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

    [Fact]
    public void ResolveRoadmapReview_WhenNotPausedForApproval_IsRejected()
    {
        var strategy = ActivateRunningStrategy(out var orchestrator);

        var result = orchestrator.ResolveRoadmapReview(strategy, new ReviewDecision(Approved: true, Reason: null));

        Assert.Equal(GateResumeOutcome.Rejected, result.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);
    }

    [Fact]
    public void ResolveRoadmapReview_CannotBeCalledFromPausedClarification_TypeAndStateBothEnforceTheDistinction()
    {
        var strategy = ActivateAndPauseForClarification(out var orchestrator);

        var result = orchestrator.ResolveRoadmapReview(strategy, new ReviewDecision(Approved: true, Reason: null));

        Assert.Equal(GateResumeOutcome.Rejected, result.Outcome);
        Assert.Equal(SessionState.PausedClarification, strategy.State);
    }

    private static StrategyInstanceHandle ActivateAndPauseForClarification(out FeatureLifecycleOrchestrator orchestrator)
    {
        var strategy = ActivateRunningStrategy(out orchestrator);
        Assert.True(strategy.PauseForClarification());
        return strategy;
    }

    private static StrategyInstanceHandle ActivateAndPauseForApproval(out FeatureLifecycleOrchestrator orchestrator)
    {
        var strategy = ActivateRunningStrategy(out orchestrator);
        Assert.True(strategy.PauseForApproval());
        return strategy;
    }

    private static StrategyInstanceHandle ActivateRunningStrategy(out FeatureLifecycleOrchestrator orchestrator)
    {
        var runtime = RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0002", "Execution"));
        orchestrator = new FeatureLifecycleOrchestrator(runtime.StrategyHost);

        var started = orchestrator.Start(new ManualTrigger("WFL-DEL-0001", InitialInput: null));
        return started.Strategy!;
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
