namespace Runtime.Ai;

public sealed record AiExecutionOutcome
{
    public bool Success { get; }
    public AiCompletionResult? Result { get; }
    public string? FailureReason { get; }
    public TimeSpan Duration { get; }
    public int AttemptCount { get; }

    private AiExecutionOutcome(bool success, AiCompletionResult? result, string? failureReason, TimeSpan duration, int attemptCount)
    {
        Success = success;
        Result = result;
        FailureReason = failureReason;
        Duration = duration;
        AttemptCount = attemptCount;
    }

    public static AiExecutionOutcome Completed(AiCompletionResult result, TimeSpan duration, int attemptCount) =>
        new(true, result, null, duration, attemptCount);

    public static AiExecutionOutcome Failed(string failureReason, TimeSpan duration, int attemptCount) =>
        new(false, null, failureReason, duration, attemptCount);
}
