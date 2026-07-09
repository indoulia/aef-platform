using Runtime.DomainContent.DeliveryExecution;
using Runtime.Integration.DeliveryExecution;
using Xunit;

namespace Runtime.Tests.DomainContent.DeliveryExecution;

// Epic 0005 (ai-engineering-framework repo), Story S31: exercised via the
// real ProcessVerificationRunner and real external commands (git) -
// never a synthetic in-memory fixture, per this Epic's own Validation
// Strategy.
public sealed class DeliveryValidationBehaviorTests
{
    [Fact]
    public void RealPassingVerification_RecordsAPassingEntry()
    {
        var request = new ValidationRequest("S31", Directory.GetCurrentDirectory(), "git", new[] { "--version" });
        var behavior = new DeliveryValidationBehavior(request, new ProcessVerificationRunner());

        var outcome = Assert.IsType<ValidationOutcome>(behavior.Execute(context: null!));

        Assert.False(outcome.IsEscalated);
        Assert.NotNull(outcome.LogEntry);
        Assert.True(outcome.LogEntry!.Passed);
        Assert.Equal(0, outcome.LogEntry.ExitCode);
    }

    // Injected failing case (Story S31's own Definition of Done): a real
    // failing verification run is recorded as a failing Test Plan &
    // Results entry, never hidden, downgraded, or silently retried away.
    [Fact]
    public void RealFailingVerification_IsLoggedAsFailing_NeverHiddenOrRetried()
    {
        var request = new ValidationRequest("S31", Directory.GetCurrentDirectory(), "git", new[] { "this-is-not-a-real-git-subcommand" });
        var behavior = new DeliveryValidationBehavior(request, new ProcessVerificationRunner());

        var outcome = Assert.IsType<ValidationOutcome>(behavior.Execute(context: null!));

        Assert.False(outcome.IsEscalated);
        Assert.NotNull(outcome.LogEntry);
        Assert.False(outcome.LogEntry!.Passed);
        Assert.NotEqual(0, outcome.LogEntry.ExitCode);
    }

    [Fact]
    public void NoVerificationProcedureConstructible_Escalates_NeverFabricatesAPass()
    {
        var request = new ValidationRequest("S31", Directory.GetCurrentDirectory(), Command: null, Array.Empty<string>());
        var behavior = new DeliveryValidationBehavior(request, new ProcessVerificationRunner());

        var outcome = Assert.IsType<ValidationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Null(outcome.LogEntry);
        Assert.Contains("No verification procedure", outcome.EscalationReason);
    }

    // Execution-discovered (dogfooding sweep): a Technology-Profile-selected
    // command (e.g. 'npm') that is simply not installed/on PATH on this host
    // used to propagate ProcessVerificationRunner's InvalidOperationException
    // all the way to an unhandled 500 (REST) / generic "unexpected error"
    // (CLI) - real command, real process-start failure, never a synthetic
    // fixture, per this file's own Validation Strategy.
    [Fact]
    public void VerificationCommandNotInstalled_Escalates_NeverThrows()
    {
        var request = new ValidationRequest("S31", Directory.GetCurrentDirectory(), "this-command-definitely-does-not-exist-xyz", Array.Empty<string>());
        var behavior = new DeliveryValidationBehavior(request, new ProcessVerificationRunner());

        var outcome = Assert.IsType<ValidationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Null(outcome.LogEntry);
        Assert.Contains("Could not run the verification procedure", outcome.EscalationReason);
    }

    [Fact]
    public void VerificationCommandTimesOut_Escalates_NeverThrows()
    {
        var runner = new ThrowingVerificationRunner(new TimeoutException("Verification command 'stub' did not exit within 00:10:00."));
        var request = new ValidationRequest("S31", Directory.GetCurrentDirectory(), "stub", Array.Empty<string>());
        var behavior = new DeliveryValidationBehavior(request, runner);

        var outcome = Assert.IsType<ValidationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Null(outcome.LogEntry);
        Assert.Contains("Could not run the verification procedure", outcome.EscalationReason);
    }

    [Fact]
    public void LargeOutput_IsExcerpted_NeverUnboundedInTheLogEntry()
    {
        // A stub runner simulating a verification command with pathologically
        // large output, proving the Behavior bounds what it records without
        // needing to actually spawn a process that prints megabytes of text.
        var hugeOutput = new string('x', 10_000);
        var runner = new StubVerificationRunner(new VerificationRunResult(0, hugeOutput, string.Empty));
        var request = new ValidationRequest("S31", Directory.GetCurrentDirectory(), "stub", Array.Empty<string>());
        var behavior = new DeliveryValidationBehavior(request, runner);

        var outcome = Assert.IsType<ValidationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.LogEntry!.StandardOutputExcerpt.Length < hugeOutput.Length);
        Assert.EndsWith("(truncated)", outcome.LogEntry.StandardOutputExcerpt);
    }

    private sealed class StubVerificationRunner : IVerificationRunner
    {
        private readonly VerificationRunResult _result;

        public StubVerificationRunner(VerificationRunResult result) => _result = result;

        public VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments) => _result;
    }

    private sealed class ThrowingVerificationRunner : IVerificationRunner
    {
        private readonly Exception _exception;

        public ThrowingVerificationRunner(Exception exception) => _exception = exception;

        public VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments) => throw _exception;
    }
}
