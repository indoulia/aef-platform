using Kernel.Resilience;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Integration.DeliveryExecution;
using Xunit;

namespace Runtime.Tests.Orchestration.Resilience;

// Epic 0005 (ai-engineering-framework repo), Story S33: the first real
// exercise of docs/discovery/orchestration/03-resilience-and-context.md's
// retry/idempotency model, per that document's own §1.1 flagged risk
// ("no real Orchestrator has ever actually run any of these state
// transitions"). Exercised against a real Skill invocation
// (DeliveryValidationBehavior, Story S31), not a synthetic delegate,
// wherever that is practical without waiting on a real wall-clock delay.
public sealed class RetryExecutorTests
{
    [Fact]
    public void TransientFailureThatEventuallySucceeds_RetriesWithBackoffThenSucceeds_AllUnderOneAttemptGroupId()
    {
        var recordedDelays = new List<TimeSpan>();
        var executor = new RetryExecutor(delay: recordedDelays.Add);
        var callCount = 0;

        var outcome = executor.Execute(() =>
        {
            callCount++;
            return callCount < 3
                ? AttemptResult.Failure(FailureClassification.Transient, $"transient blip #{callCount}")
                : AttemptResult.Success("real work done");
        });

        Assert.True(outcome.Succeeded);
        Assert.Equal("real work done", outcome.Value);
        Assert.Equal(3, callCount);
        Assert.Equal(3, outcome.Attempts.Count);
        Assert.All(outcome.Attempts, a => Assert.Equal(outcome.AttemptGroupId, a.AttemptGroupId));
        // §1.1's own literal example: 1s, 4s, 16s (base 4, exponential).
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4) }, recordedDelays);
    }

    [Fact]
    public void TransientFailurePersistingPastThreeRetries_ReclassifiesAsTerminal_NeverRetriesForever()
    {
        var executor = new RetryExecutor(delay: _ => { });
        var callCount = 0;

        var outcome = executor.Execute(() =>
        {
            callCount++;
            return AttemptResult.Failure(FailureClassification.Transient, "still failing");
        });

        Assert.False(outcome.Succeeded);
        Assert.Equal(4, callCount); // initial attempt + 3 retries
        Assert.Equal(4, outcome.Attempts.Count);
        Assert.Contains("persisted past 3 retries", outcome.TerminalFailureReason);
    }

    [Fact]
    public void StructuralFailure_ReclassifiesAsTerminalImmediately_NoRetryAttempted()
    {
        var delayCalls = 0;
        var executor = new RetryExecutor(delay: _ => delayCalls++);
        var callCount = 0;

        var outcome = executor.Execute(() =>
        {
            callCount++;
            return AttemptResult.Failure(FailureClassification.Structural, "Prompt Contract shape mismatch");
        });

        Assert.False(outcome.Succeeded);
        Assert.Equal(1, callCount); // no retry at all
        Assert.Equal(0, delayCalls);
        Assert.Contains("Structural failure, no retry attempted", outcome.TerminalFailureReason);
    }

    // Real-Skill exercise: DeliveryValidationBehavior (Story S31) wrapped
    // in a retry sequence via a verification runner that fails twice
    // (simulating a transient environment blip) before succeeding - the
    // real Skill is genuinely re-invoked, not simulated.
    [Fact]
    public void RealDeliveryValidationBehavior_RetriedThroughATransientRunnerFailure_EventuallyRecordsAPass()
    {
        var runner = new FlakyThenPassingVerificationRunner(failuresBeforeSuccess: 2);
        var request = new ValidationRequest("S33", Directory.GetCurrentDirectory(), "git", new[] { "--version" });
        var behavior = new DeliveryValidationBehavior(request, runner);
        var executor = new RetryExecutor(delay: _ => { });

        var outcome = executor.Execute(() =>
        {
            var validationOutcome = (ValidationOutcome)behavior.Execute(context: null!)!;
            return validationOutcome.LogEntry is { Passed: true }
                ? AttemptResult.Success(validationOutcome)
                : AttemptResult.Failure(FailureClassification.Transient, "verification runner reported a transient failure");
        });

        Assert.True(outcome.Succeeded);
        Assert.Equal(3, runner.CallCount);
        Assert.Equal(3, outcome.Attempts.Count);
    }

    private sealed class FlakyThenPassingVerificationRunner : IVerificationRunner
    {
        private readonly int _failuresBeforeSuccess;
        public int CallCount { get; private set; }

        public FlakyThenPassingVerificationRunner(int failuresBeforeSuccess) => _failuresBeforeSuccess = failuresBeforeSuccess;

        public VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments)
        {
            CallCount++;
            return CallCount <= _failuresBeforeSuccess
                ? new VerificationRunResult(1, string.Empty, "simulated transient failure")
                : new VerificationRunResult(0, "ok", string.Empty);
        }
    }
}
