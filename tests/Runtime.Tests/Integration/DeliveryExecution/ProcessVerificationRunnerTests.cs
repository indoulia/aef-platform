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

    [Fact]
    public async Task CommandWritingLargeOutputToBothStreamsConcurrently_DoesNotDeadlock()
    {
        // Regression: reading stdout to completion before starting to read
        // stderr (or vice versa) deadlocks as soon as the un-drained
        // stream fills its OS pipe buffer. A real build/test command
        // (e.g. `dotnet test`, `npm test`) routinely writes large,
        // concurrent output on both streams - this reproduces that shape
        // deterministically. Guarded by Task.WhenAny(timeout) rather than
        // a bare call so a reintroduced deadlock fails the test instead of
        // hanging the whole run.
        var runner = new ProcessVerificationRunner();
        var script = "$out = [string]::new('A', 200); $err = [string]::new('B', 200); " +
                     "for ($i = 0; $i -lt 20000; $i++) { Write-Output $out; [Console]::Error.WriteLine($err) }";

        var task = Task.Run(() => runner.Run(Directory.GetCurrentDirectory(), "powershell", new[] { "-NoProfile", "-Command", script }));
        var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.Same(task, winner);
        var result = await task;
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.StandardOutput.Length > 1_000_000, $"Expected >1MB stdout, got {result.StandardOutput.Length} bytes.");
        Assert.True(result.StandardError.Length > 1_000_000, $"Expected >1MB stderr, got {result.StandardError.Length} bytes.");
    }
}
