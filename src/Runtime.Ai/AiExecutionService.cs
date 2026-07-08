using System.Diagnostics;
using Kernel.Evidence;
using Kernel.Resilience;

namespace Runtime.Ai;

// The GA-1 orchestration point: wraps one IAiProvider call with the
// existing RetryExecutor's classification/backoff algorithm (no new
// resilience mechanism), a timeout via a linked CancellationTokenSource
// (no generic timeout wrapper existed anywhere in the Runtime before
// this), and records one EvidenceRecord per call via IEvidenceSink,
// mirroring SkillHost.Invoke's own Contract/Decision/SubjectId/Reason/
// Timestamp shape exactly.
public sealed class AiExecutionService
{
    private readonly IAiProvider _provider;
    private readonly IEvidenceSink _evidenceSink;
    private readonly TimeSpan _timeout;
    private readonly RetryExecutor _retryExecutor;

    public AiExecutionService(
        IAiProvider provider,
        IEvidenceSink? evidenceSink = null,
        TimeSpan? timeout = null,
        Action<TimeSpan>? delay = null)
    {
        _provider = provider;
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        _retryExecutor = new RetryExecutor(delay: delay);
    }

    public AiExecutionOutcome Execute(AiPromptRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var retryOutcome = _retryExecutor.Execute(() =>
        {
            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                var result = _provider.CompleteAsync(request, linkedCts.Token).GetAwaiter().GetResult();
                return AttemptResult.Success(result);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                return AttemptResult.Failure(FailureClassification.Transient, $"AI provider call timed out after {_timeout}.");
            }
            catch (OperationCanceledException)
            {
                return AttemptResult.Failure(FailureClassification.Structural, "AI provider call was cancelled by the caller.");
            }
            catch (AiProviderException ex)
            {
                return AttemptResult.Failure(ex.IsTransient ? FailureClassification.Transient : FailureClassification.Structural, ex.Message);
            }
        });

        stopwatch.Stop();

        _evidenceSink.Record(new EvidenceRecord(
            Contract: "AiExecutionService",
            Decision: retryOutcome.Succeeded ? "Completed" : "Failed",
            SubjectId: request.PromptContractId,
            Reason: retryOutcome.Succeeded ? null : retryOutcome.TerminalFailureReason,
            Timestamp: DateTimeOffset.UtcNow));

        return retryOutcome.Succeeded
            ? AiExecutionOutcome.Completed((AiCompletionResult)retryOutcome.Value!, stopwatch.Elapsed, retryOutcome.Attempts.Count)
            : AiExecutionOutcome.Failed(retryOutcome.TerminalFailureReason!, stopwatch.Elapsed, retryOutcome.Attempts.Count);
    }
}
