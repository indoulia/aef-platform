using Runtime.Hosts.Strategy;

namespace Runtime.Orchestration.Resilience;

// Exercises the real StrategyInstanceHandle/SessionTransitions state
// machine (Epic 0003, unmodified) for the first time against a
// Paused-Terminal instance, per docs/discovery/orchestration/
// 03-resilience-and-context.md §3.1. Resume and Redirect both continue
// at Running (they differ only in which step is allocated next, a
// caller-level concern this Resolver surfaces via RedirectToStepId, not
// a new SessionState); Terminate ends the instance. A decision rendered
// against an instance that is not actually Paused-Terminal is rejected,
// never silently applied.
public static class TerminalRecoveryResolver
{
    public static TerminalRecoveryResult Resolve(StrategyInstanceHandle handle, TerminalRecoveryDecision decision, string? redirectToStepId = null)
    {
        if (handle.State != SessionState.PausedTerminal)
        {
            return TerminalRecoveryResult.Rejected(
                $"Instance '{handle.InstanceId}' is not Paused-Terminal (actual state: {handle.State}) - a Resume/Redirect/Terminate Decision only applies to a genuine Terminal pause.");
        }

        switch (decision)
        {
            case TerminalRecoveryDecision.Resume:
                return handle.Resume()
                    ? TerminalRecoveryResult.Resumed
                    : TerminalRecoveryResult.Rejected("The Strategy Host's own state machine refused the Resume transition.");

            case TerminalRecoveryDecision.Redirect:
                if (string.IsNullOrEmpty(redirectToStepId))
                {
                    return TerminalRecoveryResult.Rejected("Redirect requires a target step id - none was supplied.");
                }

                return handle.Resume()
                    ? TerminalRecoveryResult.Redirected(redirectToStepId)
                    : TerminalRecoveryResult.Rejected("The Strategy Host's own state machine refused the Redirect (Resume) transition.");

            case TerminalRecoveryDecision.Terminate:
                return handle.Terminate()
                    ? TerminalRecoveryResult.Terminated
                    : TerminalRecoveryResult.Rejected("The Strategy Host's own state machine refused the Terminate transition.");

            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unrecognized TerminalRecoveryDecision.");
        }
    }
}
