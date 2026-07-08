using System.Diagnostics;
using System.Threading;
using Runtime.Integration.GitOperations;

namespace Runtime.Tests.Integration.GitOperations;

public class LocalGitOperationsTests
{
    private static string InitRepository()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        RunGit(root, "init", "-b", "main");
        RunGit(root, "config", "user.email", "test@example.com");
        RunGit(root, "config", "user.name", "Test User");
        File.WriteAllText(Path.Combine(root, "README.md"), "# test repo");
        RunGit(root, "add", "-A");
        RunGit(root, "commit", "-m", "initial commit");
        return root;
    }

    private static void RunGit(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in arguments) startInfo.ArgumentList.Add(arg);
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
    }

    // git marks its own object-store files read-only on Windows;
    // Directory.Delete fails on those unless the read-only attribute is
    // cleared first - a test-only concern, not a Runtime behavior. Retried
    // briefly because Windows can hold a transient handle on a just-written
    // file (e.g. AV scanning) for a few hundred ms after a large git
    // operation - also test-only, not a Runtime behavior.
    private static void DeleteRepository(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(root, true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(200);
            }
        }
    }

    [Fact]
    public void IsGitRepository_RealRepository_ReturnsTrue()
    {
        var root = InitRepository();
        try
        {
            Assert.True(new LocalGitOperations(root).IsGitRepository());
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void GetStatus_CleanRepository_ReportsCleanAndCurrentBranch()
    {
        var root = InitRepository();
        try
        {
            var status = new LocalGitOperations(root).GetStatus();

            Assert.Equal("main", status.CurrentBranch);
            Assert.True(status.IsClean);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void GetStatus_UntrackedFile_ReportsNotCleanWithUntrackedPath()
    {
        var root = InitRepository();
        try
        {
            File.WriteAllText(Path.Combine(root, "new-file.txt"), "content");

            var status = new LocalGitOperations(root).GetStatus();

            Assert.False(status.IsClean);
            Assert.Contains("new-file.txt", status.UntrackedPaths);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void CreateBranch_ThenCommit_Succeeds()
    {
        var root = InitRepository();
        try
        {
            var ops = new LocalGitOperations(root);

            var branchResult = ops.CreateBranch("feature/x");
            File.WriteAllText(Path.Combine(root, "new-file.txt"), "content");
            var commitResult = ops.Commit("add new file");

            Assert.True(branchResult.Success);
            Assert.True(commitResult.Success);
            Assert.True(ops.GetStatus().IsClean);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void CheckMergeReadiness_DefaultBranch_IsNotReady()
    {
        var root = InitRepository();
        try
        {
            var result = new LocalGitOperations(root).CheckMergeReadiness("main", "main");

            Assert.False(result.IsReady);
            Assert.NotEmpty(result.Reasons);
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void CheckMergeReadiness_UnrelatedBranchIsCurrentlyCheckedOutAndDirty_DoesNotBlameTheNamedBranch()
    {
        var root = InitRepository();
        try
        {
            var ops = new LocalGitOperations(root);
            ops.CreateBranch("feature/x"); // created from main, never checked out - has no independent working-tree state

            RunGit(root, "checkout", "-b", "other");
            File.WriteAllText(Path.Combine(root, "unrelated.txt"), "dirty");

            var result = ops.CheckMergeReadiness("feature/x", "main");

            Assert.True(result.IsReady, string.Join("; ", result.Reasons));
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void GetStatus_RenamedFile_ReportsTheDestinationPathNotTheRawPorcelainLine()
    {
        // Regression: `git status --porcelain` emits rename/copy lines as
        // "R  old -> new" - naive `trimmed[3..]` parsing previously
        // returned the literal string "old -> new" instead of a usable path.
        var root = InitRepository();
        try
        {
            var ops = new LocalGitOperations(root);
            RunGit(root, "mv", "README.md", "RENAMED.md");

            var status = ops.GetStatus();

            Assert.Contains("RENAMED.md", status.ModifiedPaths.Concat(status.UntrackedPaths));
            Assert.DoesNotContain(status.ModifiedPaths.Concat(status.UntrackedPaths), p => p.Contains("->"));
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public async Task GetDiff_LargeChange_CompletesWithoutDeadlockAndReturnsFullContent()
    {
        // Regression: reading stdout to completion before starting to read
        // stderr (or vice versa) deadlocks once the un-drained stream
        // fills its OS pipe buffer - `git diff` on a multi-megabyte change
        // is exactly the realistic trigger. Guarded by Task.WhenAny(timeout)
        // so a reintroduced deadlock fails the test instead of hanging
        // the whole run.
        var root = InitRepository();
        try
        {
            var ops = new LocalGitOperations(root);
            var largeContent = string.Concat(Enumerable.Repeat("line of content padding the diff output\n", 100_000));
            File.WriteAllText(Path.Combine(root, "README.md"), largeContent);

            var task = Task.Run(() => ops.GetDiff());
            var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)));

            Assert.Same(task, winner);
            var diff = await task;
            Assert.True(diff.Length > 1_000_000, $"Expected >1MB diff output, got {diff.Length} bytes.");
        }
        finally
        {
            DeleteRepository(root);
        }
    }

    [Fact]
    public void CheckMergeReadiness_CleanFeatureBranch_IsReady()
    {
        var root = InitRepository();
        try
        {
            var ops = new LocalGitOperations(root);
            ops.CreateBranch("feature/x");

            var result = ops.CheckMergeReadiness("feature/x", "main");

            Assert.True(result.IsReady);
            Assert.Empty(result.Reasons);
        }
        finally
        {
            DeleteRepository(root);
        }
    }
}
