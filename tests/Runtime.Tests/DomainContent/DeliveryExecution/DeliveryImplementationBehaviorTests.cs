using System.Diagnostics;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Integration.DeliveryExecution;
using Xunit;

namespace Runtime.Tests.DomainContent.DeliveryExecution;

// Epic 0005 (ai-engineering-framework repo), Story S30: exercised against
// a real, temporary, on-disk Git repository via the real
// LocalGitChangeVerifier - never a synthetic in-memory fixture or a fake
// verifier - per this Epic's own Validation Strategy ("each run against a
// real, isolated test repository").
public sealed class DeliveryImplementationBehaviorTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public DeliveryImplementationBehaviorTests()
    {
        _root = Directory.CreateTempSubdirectory("s30-behavior-tests-");
        RunGit(_root.FullName, "init", "--initial-branch=main");
        RunGit(_root.FullName, "config", "user.email", "s30-test@example.com");
        RunGit(_root.FullName, "config", "user.name", "S30 Test");
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch
        {
            // Best-effort cleanup only - never fail a test on teardown.
        }
    }

    [Fact]
    public void RealIsolatedBranchWithVerifiedCommit_ProducesARecordedImplementationLogEntry()
    {
        CommitFile(_root.FullName, "README.md", "hello");
        RunGit(_root.FullName, "checkout", "-b", "feature/s30-story");
        var sha = CommitFile(_root.FullName, "change.txt", "the real change");

        var request = new ImplementationRequest("S30", "feature/s30-story", new[] { sha }, "Implement the thing the Plan describes.");
        var behavior = new DeliveryImplementationBehavior(request, new LocalGitChangeVerifier(_root.FullName, "main"));

        var outcome = Assert.IsType<ImplementationOutcome>(behavior.Execute(context: null!));

        Assert.False(outcome.IsEscalated);
        Assert.NotNull(outcome.LogEntry);
        Assert.Equal("S30", outcome.LogEntry!.StoryReferenceId);
        Assert.Equal("feature/s30-story", outcome.LogEntry.BranchName);
        Assert.Contains(sha, outcome.LogEntry.VerifiedCommitShas);
    }

    [Fact]
    public void TargetingTheDefaultBranch_Escalates_NeverRecordsAgainstThePrimaryIntegrationLine()
    {
        var sha = CommitFile(_root.FullName, "README.md", "hello");

        var request = new ImplementationRequest("S30", "main", new[] { sha }, "Attempted change directly on main.");
        var behavior = new DeliveryImplementationBehavior(request, new LocalGitChangeVerifier(_root.FullName, "main"));

        var outcome = Assert.IsType<ImplementationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Null(outcome.LogEntry);
        Assert.Contains("primary integration line", outcome.EscalationReason);
    }

    [Fact]
    public void NonExistentBranch_Escalates_NeverFabricatesALogEntry()
    {
        var request = new ImplementationRequest("S30", "no-such-branch", new[] { "deadbeef" }, "Claims a branch that was never created.");
        var behavior = new DeliveryImplementationBehavior(request, new LocalGitChangeVerifier(_root.FullName, "main"));

        var outcome = Assert.IsType<ImplementationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Null(outcome.LogEntry);
    }

    [Fact]
    public void NoCommitShasSupplied_Escalates_NeverAcceptsACallerSuppliedSummaryInstead()
    {
        RunGit(_root.FullName, "checkout", "-b", "feature/s30-empty");
        CommitFile(_root.FullName, "README.md", "hello");

        var request = new ImplementationRequest("S30", "feature/s30-empty", Array.Empty<string>(), "I promise I changed something.");
        var behavior = new DeliveryImplementationBehavior(request, new LocalGitChangeVerifier(_root.FullName, "main"));

        var outcome = Assert.IsType<ImplementationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Contains("caller-supplied summary", outcome.EscalationReason);
    }

    [Fact]
    public void CommitClaimedButNotReachableFromTheBranch_Escalates_NeverTrustsTheClaim()
    {
        CommitFile(_root.FullName, "README.md", "hello");
        RunGit(_root.FullName, "checkout", "-b", "feature/s30-a");
        var shaOnA = CommitFile(_root.FullName, "a.txt", "content a");
        RunGit(_root.FullName, "checkout", "main");
        RunGit(_root.FullName, "checkout", "-b", "feature/s30-b");
        CommitFile(_root.FullName, "b.txt", "content b");

        var request = new ImplementationRequest("S30", "feature/s30-b", new[] { shaOnA }, "Claims a-branch's commit while isolated on b-branch.");
        var behavior = new DeliveryImplementationBehavior(request, new LocalGitChangeVerifier(_root.FullName, "main"));

        var outcome = Assert.IsType<ImplementationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Null(outcome.LogEntry);
    }

    private static string CommitFile(string repositoryRoot, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(repositoryRoot, fileName), content);
        RunGit(repositoryRoot, "add", fileName);
        RunGit(repositoryRoot, "commit", "-m", $"add {fileName}");
        return RunGitCapture(repositoryRoot, "rev-parse", "HEAD").Trim();
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        RunGitCapture(workingDirectory, arguments);
    }

    private static string RunGitCapture(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }

        return output;
    }
}
