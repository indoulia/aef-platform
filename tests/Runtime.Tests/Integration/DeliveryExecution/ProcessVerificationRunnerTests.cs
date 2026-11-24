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
    public void WindowsBatchShimOnPath_LikeNpm_IsRunnable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // npm is npm.cmd only on Windows; CreateProcess cannot launch a .cmd directly
        }

        var directory = Directory.CreateTempSubdirectory("aef-shim-").FullName;
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            File.WriteAllText(Path.Combine(directory, "aef-fake-npm.cmd"), "@echo shim ran %1\r\n@exit /b 3\r\n");
            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + originalPath);

            var result = new ProcessVerificationRunner().Run(directory, "aef-fake-npm", new[] { "test" });

            Assert.Equal(3, result.ExitCode);
            Assert.Contains("shim ran test", result.StandardOutput);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RepositoryLocalWrapper_LikeGradlew_IsRunnableFromTheWorkingDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("aef-wrapper-").FullName;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(Path.Combine(directory, "aef-fake-gradlew.bat"), "@echo wrapper ran %1\r\n@exit /b 0\r\n");
            }
            else
            {
                var script = Path.Combine(directory, "aef-fake-gradlew");
                File.WriteAllText(script, "#!/bin/sh\necho wrapper ran $1\n");
                File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var result = new ProcessVerificationRunner().Run(directory, "aef-fake-gradlew", new[] { "test" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("wrapper ran test", result.StandardOutput);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
