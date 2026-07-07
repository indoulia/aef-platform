namespace Runtime.Orchestration.Resilience;

public enum TerminalRecoveryOutcome
{
    Resumed,
    Redirected,
    Terminated,
    Rejected,
}

// Mirrors GateResumeResult's existing shape (Runtime.Orchestration),
// extended with Redirected/RedirectToStepId for the Paused-Terminal
// gate's own three-way decision (§3.1), which GateResumeResult's
// Approve/Reject shape does not cover.
public sealed record TerminalRecoveryResult(TerminalRecoveryOutcome Outcome, string? RedirectToStepId, string? Reason)
{
    public static readonly TerminalRecoveryResult Resumed = new(TerminalRecoveryOutcome.Resumed, null, null);

    public static TerminalRecoveryResult Redirected(string stepId) =>
        new(TerminalRecoveryOutcome.Redirected, stepId, null);

    public static readonly TerminalRecoveryResult Terminated = new(TerminalRecoveryOutcome.Terminated, null, null);

    public static TerminalRecoveryResult Rejected(string reason) =>
        new(TerminalRecoveryOutcome.Rejected, null, reason);
}
