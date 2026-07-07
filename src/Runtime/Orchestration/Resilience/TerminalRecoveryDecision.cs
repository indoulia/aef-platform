namespace Runtime.Orchestration.Resilience;

// docs/discovery/orchestration/03-resilience-and-context.md §3.1: a
// Paused-Terminal instance resumes only via a Review's recorded
// Engineering Decision, specifying exactly one of these three.
public enum TerminalRecoveryDecision
{
    Resume,
    Redirect,
    Terminate,
}
