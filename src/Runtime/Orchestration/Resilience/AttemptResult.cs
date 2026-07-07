namespace Runtime.Orchestration.Resilience;

public enum FailureClassification
{
    // A Skill invocation fails due to a transient condition (POL-GOV-0001) -
    // retried up to 3 times with backoff before being reclassified as
    // Terminal (docs/discovery/orchestration/03-resilience-and-context.md §1.1).
    Transient,

    // A Prompt Contract validation failure or an uncovered Rule violation -
    // reclassified as Terminal immediately, no retry attempted.
    Structural,
}

// What one attempt at a step produced - never a guess about whether a
// failure is retryable; the caller supplying this result is the one
// place that classification decision is made, per POL-GOV-0001's own
// stated boundary.
public sealed record AttemptResult
{
    public bool Succeeded { get; }
    public object? Value { get; }
    public FailureClassification? Classification { get; }
    public string? FailureReason { get; }

    private AttemptResult(bool succeeded, object? value, FailureClassification? classification, string? reason)
    {
        Succeeded = succeeded;
        Value = value;
        Classification = classification;
        FailureReason = reason;
    }

    public static AttemptResult Success(object? value) => new(true, value, null, null);

    public static AttemptResult Failure(FailureClassification classification, string reason) =>
        new(false, null, classification, reason);
}
