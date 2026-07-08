using Kernel.Evidence;
using Runtime.Ai;
using Xunit;

namespace Runtime.Ai.Tests;

public class AiExecutionServiceTests
{
    private sealed class StubProvider : IAiProvider
    {
        private readonly Func<AiCompletionResult> _result;
        private Exception? _throwOnce;
        private int _calls;

        public StubProvider(Func<AiCompletionResult> result, Exception? throwOnce = null)
        {
            _result = result;
            _throwOnce = throwOnce;
        }

        public string Name => "Stub";
        public int Calls => _calls;

        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            _calls++;
            if (_throwOnce is not null && _calls == 1)
            {
                var ex = _throwOnce;
                _throwOnce = null;
                throw ex;
            }
            return Task.FromResult(_result());
        }
    }

    private sealed class ThrowAlwaysProvider : IAiProvider
    {
        private readonly Func<Exception> _exceptionFactory;
        public ThrowAlwaysProvider(Func<Exception> exceptionFactory) => _exceptionFactory = exceptionFactory;
        public string Name => "Stub";
        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken) => throw _exceptionFactory();
    }

    private sealed class SlowProvider : IAiProvider
    {
        public string Name => "Stub";
        public async Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return new AiCompletionResult("{}", new AiUsage(0, 0, 0), "Stub", "test-model");
        }
    }

    private sealed class RecordingEvidenceSink : IEvidenceSink
    {
        public readonly List<EvidenceRecord> Records = new();
        public void Record(EvidenceRecord record) => Records.Add(record);
    }

    [Fact]
    public void SucceedsOnFirstAttemptAndRecordsEvidence()
    {
        var expected = new AiCompletionResult("{}", new AiUsage(10, 5, 15), "Stub", "test-model");
        var provider = new StubProvider(() => expected);
        var sink = new RecordingEvidenceSink();
        var service = new AiExecutionService(provider, sink, timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(1, outcome.AttemptCount);
        Assert.Same(expected, outcome.Result);
        Assert.Single(sink.Records);
        Assert.Equal("Completed", sink.Records[0].Decision);
        Assert.Equal("PCT-EXE-0026", sink.Records[0].SubjectId);
    }

    [Fact]
    public void RetriesATransientFailureThenSucceeds()
    {
        var expected = new AiCompletionResult("{}", new AiUsage(1, 1, 2), "Stub", "test-model");
        var provider = new StubProvider(() => expected, throwOnce: new AiProviderException("rate limited", isTransient: true));
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(2, outcome.AttemptCount);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public void DoesNotRetryAStructuralFailure()
    {
        var provider = new ThrowAlwaysProvider(() => new AiProviderException("bad request", isTransient: false));
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal(1, outcome.AttemptCount);
        Assert.Contains("bad request", outcome.FailureReason);
    }

    [Fact]
    public void TimesOutAndReportsFailure()
    {
        var provider = new SlowProvider();
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromMilliseconds(10), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Contains("timed out", outcome.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HonorsCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new ThrowAlwaysProvider(() => new OperationCanceledException());
        var service = new AiExecutionService(provider, new RecordingEvidenceSink(), timeout: TimeSpan.FromSeconds(5), delay: _ => { });

        var outcome = service.Execute(new AiPromptRequest("PCT-EXE-0026", "sys", "user"), cts.Token);

        Assert.False(outcome.Success);
        Assert.Contains("cancelled", outcome.FailureReason, StringComparison.OrdinalIgnoreCase);
    }
}
