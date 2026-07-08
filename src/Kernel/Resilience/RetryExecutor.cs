namespace Kernel.Resilience;

// Realizes docs/discovery/orchestration/03-resilience-and-context.md §1.1's
// exact algorithm: up to 3 retries with exponential backoff (base 4 - 1s,
// 4s, 16s) for a Transient failure; a Structural failure reclassifies as
// Terminal immediately, no retry attempted. Every attempt shares one
// attempt_group_id (§1.2). Backoff/delay are injectable so a test can
// exercise the real classification and attempt-count behavior without
// waiting on a real wall-clock delay.
public sealed class RetryExecutor
{
    private const int MaxRetries = 3;

    private readonly Func<int, TimeSpan> _backoff;
    private readonly Action<TimeSpan> _delay;

    public RetryExecutor(Func<int, TimeSpan>? backoff = null, Action<TimeSpan>? delay = null)
    {
        _backoff = backoff ?? (retryCount => TimeSpan.FromSeconds(Math.Pow(4, retryCount)));
        _delay = delay ?? Thread.Sleep;
    }

    public RetryOutcome Execute(Func<AttemptResult> attempt)
    {
        var attemptGroupId = Guid.NewGuid();
        var attempts = new List<AttemptRecord>();
        var retryCount = 0;

        while (true)
        {
            var result = attempt();
            var attemptNumber = attempts.Count + 1;

            if (result.Succeeded)
            {
                attempts.Add(new AttemptRecord(attemptNumber, attemptGroupId, Succeeded: true, FailureReason: null));
                return RetryOutcome.Success(result.Value, attemptGroupId, attempts);
            }

            attempts.Add(new AttemptRecord(attemptNumber, attemptGroupId, Succeeded: false, result.FailureReason));

            if (result.Classification == FailureClassification.Structural)
            {
                return RetryOutcome.TerminallyFailed(
                    $"Structural failure, no retry attempted: {result.FailureReason}", attemptGroupId, attempts);
            }

            if (retryCount >= MaxRetries)
            {
                return RetryOutcome.TerminallyFailed(
                    $"Transient failure persisted past {MaxRetries} retries: {result.FailureReason}", attemptGroupId, attempts);
            }

            _delay(_backoff(retryCount));
            retryCount++;
        }
    }
}
