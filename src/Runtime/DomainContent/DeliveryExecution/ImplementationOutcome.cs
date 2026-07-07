namespace Runtime.DomainContent.DeliveryExecution;

public sealed record ImplementationLogEntry(
    string StoryReferenceId,
    string BranchName,
    IReadOnlyList<string> VerifiedCommitShas,
    string PlanSummary);

// Mirrors AssessmentOutcome's (Onboarding/AssessmentOutcome.cs) closed,
// no-third-state-guessed shape: a completed run either produces a real,
// independently-verified Implementation Log entry, or escalates with a
// named reason - it never returns a "probably fine" result for the
// isolation/evidence checks RUL-DEL-0004 requires.
public sealed record ImplementationOutcome
{
    public ImplementationLogEntry? LogEntry { get; }
    public string? EscalationReason { get; }

    private ImplementationOutcome(ImplementationLogEntry? logEntry, string? escalationReason)
    {
        LogEntry = logEntry;
        EscalationReason = escalationReason;
    }

    public static ImplementationOutcome Recorded(ImplementationLogEntry logEntry) => new(logEntry, null);

    public static ImplementationOutcome Escalated(string reason) => new(null, reason);

    public bool IsEscalated => EscalationReason is not null;
}
