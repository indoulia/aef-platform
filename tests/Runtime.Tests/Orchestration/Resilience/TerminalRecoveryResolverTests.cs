using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.Hosts.Strategy;
using Runtime.Orchestration;
using Runtime.Orchestration.Resilience;
using Xunit;

namespace Runtime.Tests.Orchestration.Resilience;

// Epic 0005 (ai-engineering-framework repo), Story S33: the first real
// exercise of docs/discovery/orchestration/03-resilience-and-context.md
// §3.1's Paused-Terminal recovery model (Resume/Redirect/Terminate),
// against a real, bootstrapped StrategyInstanceHandle - the same
// real-Runtime bootstrap pattern HumanApprovalGateRoutingTests (Story
// S14) already established for the other two pause-reason gates.
public sealed class TerminalRecoveryResolverTests
{
    [Fact]
    public void Resume_FromPausedTerminal_ResumesToRunning_UsingTheRealStateMachine()
    {
        var strategy = ActivateAndPauseForTerminalFailure();

        var result = TerminalRecoveryResolver.Resolve(strategy, TerminalRecoveryDecision.Resume);

        Assert.Equal(TerminalRecoveryOutcome.Resumed, result.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);
    }

    [Fact]
    public void Redirect_FromPausedTerminal_ResumesToRunning_AndCarriesTheRedirectTargetStepId()
    {
        var strategy = ActivateAndPauseForTerminalFailure();

        var result = TerminalRecoveryResolver.Resolve(strategy, TerminalRecoveryDecision.Redirect, redirectToStepId: "RootCauseAnalysis");

        Assert.Equal(TerminalRecoveryOutcome.Redirected, result.Outcome);
        Assert.Equal("RootCauseAnalysis", result.RedirectToStepId);
        Assert.Equal(SessionState.Running, strategy.State);
    }

    [Fact]
    public void Redirect_WithNoTargetStepId_IsRejected_NeverSilentlyResumesWithoutADestination()
    {
        var strategy = ActivateAndPauseForTerminalFailure();

        var result = TerminalRecoveryResolver.Resolve(strategy, TerminalRecoveryDecision.Redirect, redirectToStepId: null);

        Assert.Equal(TerminalRecoveryOutcome.Rejected, result.Outcome);
        Assert.Equal(SessionState.PausedTerminal, strategy.State);
    }

    [Fact]
    public void Terminate_FromPausedTerminal_Terminates_UsingTheRealStateMachine()
    {
        var strategy = ActivateAndPauseForTerminalFailure();

        var result = TerminalRecoveryResolver.Resolve(strategy, TerminalRecoveryDecision.Terminate);

        Assert.Equal(TerminalRecoveryOutcome.Terminated, result.Outcome);
        Assert.Equal(SessionState.Terminated, strategy.State);
    }

    [Fact]
    public void AnyDecision_WhenNotActuallyPausedTerminal_IsRejected_NeverSilentlyApplied()
    {
        var strategy = ActivateRunningStrategy(out _);

        var result = TerminalRecoveryResolver.Resolve(strategy, TerminalRecoveryDecision.Resume);

        Assert.Equal(TerminalRecoveryOutcome.Rejected, result.Outcome);
        Assert.Equal(SessionState.Running, strategy.State);
    }

    private static StrategyInstanceHandle ActivateAndPauseForTerminalFailure()
    {
        var strategy = ActivateRunningStrategy(out _);
        Assert.True(strategy.PauseForTerminalFailure());
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
