namespace Runtime.Hosts.Strategy;

// The Strategy Host's own transition table, widened to
// docs/discovery/orchestration/01-orchestrator-design.md §2.2's exact edge
// set — a fixed, closed set, with no edge to any state outside it. Notably,
// Running has no direct edge to Terminated: per D10's own table, termination
// is only reachable after a Review Decision, via one of the three Paused-*
// states — a deliberate narrowing from Epic 0002's own trivial four-state
// table, which no existing test exercised directly (epics/0003-...md, S11).
internal static class SessionTransitions
{
    private static readonly Dictionary<SessionState, SessionState[]> Allowed = new()
    {
        [SessionState.Running] = new[]
        {
            SessionState.PausedApproval,
            SessionState.PausedClarification,
            SessionState.PausedTerminal,
            SessionState.Completed,
        },
        [SessionState.PausedApproval] = new[] { SessionState.Running, SessionState.Terminated },
        [SessionState.PausedClarification] = new[] { SessionState.Running },
        [SessionState.PausedTerminal] = new[] { SessionState.Running, SessionState.Terminated },
        [SessionState.Completed] = Array.Empty<SessionState>(),
        [SessionState.Terminated] = Array.Empty<SessionState>(),
    };

    public static bool IsAllowed(SessionState from, SessionState to) => Allowed[from].Contains(to);
}
