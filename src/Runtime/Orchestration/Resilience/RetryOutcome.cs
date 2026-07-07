namespace Runtime.Orchestration.Resilience;

// One try within a retry sequence - every attempt is retained (per
// §1.2's "earlier attempts' partial Evidence... is retained for audit"),
// not just the final one.
public sealed record AttemptRecord(int AttemptNumber, Guid AttemptGroupId, bool Succeeded, string? FailureReason);

// The result of a full retry sequence sharing one attempt_group_id
// (§1.2's idempotency requirement: "a retried invocation carries the
// same attempt_group_id; only the final successful (or finally-Terminal)
// attempt's Evidence is treated as authoritative").
public sealed record RetryOutcome
{
    public bool Succeeded { get; }
    public object? Value { get; }
    public string? TerminalFailureReason { get; }
    public Guid AttemptGroupId { get; }
    public IReadOnlyList<AttemptRecord> Attempts { get; }

    private RetryOutcome(bool succeeded, object? value, string? terminalFailureReason, Guid attemptGroupId, IReadOnlyList<AttemptRecord> attempts)
    {
        Succeeded = succeeded;
        Value = value;
        TerminalFailureReason = terminalFailureReason;
        AttemptGroupId = attemptGroupId;
        Attempts = attempts;
    }

    public static RetryOutcome Success(object? value, Guid attemptGroupId, IReadOnlyList<AttemptRecord> attempts) =>
        new(true, value, null, attemptGroupId, attempts);

    public static RetryOutcome TerminallyFailed(string reason, Guid attemptGroupId, IReadOnlyList<AttemptRecord> attempts) =>
        new(false, null, reason, attemptGroupId, attempts);
}
