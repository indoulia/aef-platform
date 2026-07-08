using System.Diagnostics;
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
    // cleared first - a test-only concern, not a Runtime behavior.
    private static void DeleteRepository(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(root, true);
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
