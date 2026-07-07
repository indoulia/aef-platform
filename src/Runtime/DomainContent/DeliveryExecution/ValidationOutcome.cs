namespace Runtime.DomainContent.DeliveryExecution;

public sealed record TestPlanAndResultsEntry(
    string StoryReferenceId,
    bool Passed,
    int ExitCode,
    string StandardOutputExcerpt,
    string StandardErrorExcerpt);

// Mirrors ImplementationOutcome's closed shape: a completed run either
// produces a real, observed Test Plan & Results entry (passing or
// failing - a failure is still a Recorded outcome, never hidden, per
// this Story's own "log it rather than silently omitting it" acceptance
// criterion) or escalates when no verification procedure could be run at
// all - never a fabricated pass.
public sealed record ValidationOutcome
{
    public TestPlanAndResultsEntry? LogEntry { get; }
    public string? EscalationReason { get; }

    private ValidationOutcome(TestPlanAndResultsEntry? logEntry, string? escalationReason)
    {
        LogEntry = logEntry;
        EscalationReason = escalationReason;
    }

    public static ValidationOutcome Recorded(TestPlanAndResultsEntry logEntry) => new(logEntry, null);

    public static ValidationOutcome Escalated(string reason) => new(null, reason);

    public bool IsEscalated => EscalationReason is not null;
}
