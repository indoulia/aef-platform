namespace Runtime.Hosts.Strategy;

// The Workflow Instance state machine (docs/discovery/orchestration/01-orchestrator-design.md
// §2), widened here from Epic 0002's four-state trivial proof to the six
// states the real Orchestration design distinguishes — three separate pause
// reasons instead of one generic Paused state — per epics/0003-...md, S11.
public enum SessionState
{
    Running,
    PausedApproval,
    PausedClarification,
    PausedTerminal,
    Completed,
    Terminated,
}
