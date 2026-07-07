using Runtime.Integration.DeliveryExecution;
using Xunit;

namespace Runtime.Tests.Integration.DeliveryExecution;

// Epic 0005 (ai-engineering-framework repo), Story S31: exercised via
// real, external process invocations (git, a real executable already on
// PATH) - never a synthetic in-memory fixture, per this Epic's own
// Validation Strategy.
public sealed class ProcessVerificationRunnerTests
{
    [Fact]
    public void RealPassingCommand_ReturnsZeroExitCodeAndRealOutput()
    {
        var runner = new ProcessVerificationRunner();

        var result = runner.Run(Directory.GetCurrentDirectory(), "git", new[] { "--version" });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("git version", result.StandardOutput);
    }

    [Fact]
    public void RealFailingCommand_ReturnsNonZeroExitCode_NeverMaskedAsSuccess()
    {
        var runner = new ProcessVerificationRunner();

        var result = runner.Run(Directory.GetCurrentDirectory(), "git", new[] { "this-is-not-a-real-git-subcommand" });

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void NonExistentCommand_ThrowsInvalidOperationException_WithClearMessage()
    {
        var runner = new ProcessVerificationRunner();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            runner.Run(Directory.GetCurrentDirectory(), "this-executable-definitely-does-not-exist-s31", Array.Empty<string>()));

        Assert.Contains("this-executable-definitely-does-not-exist-s31", exception.Message);
    }

    [Fact]
    public void EmptyCommand_ThrowsArgumentException()
    {
        var runner = new ProcessVerificationRunner();

        Assert.Throws<ArgumentException>(() => runner.Run(Directory.GetCurrentDirectory(), string.Empty, Array.Empty<string>()));
    }
}
