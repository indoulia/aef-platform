using System.Diagnostics;
using Runtime.Integration.GitOperations;

namespace Runtime.Tests.Integration.GitOperations;

public class ChangedFilesSinceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aef-changed-").FullName;

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100 * (attempt + 1));
            }
        }
    }

    private void Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git") { WorkingDirectory = _root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    private void Write(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void InitWithOneCommit(string branch = "main")
    {
        Git("init", "-b", branch);
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test User");
        Write("base.txt", "base");
        Write("tracked.txt", "v1");
        Git("add", "-A");
        Git("commit", "-m", "initial");
    }

    [Fact]
    public void ChangedFilesSince_Null_OnFeatureBranch_ReturnsCommittedStagedUnstagedAndUntracked()
    {
        InitWithOneCommit();
        Git("checkout", "-b", "feature/x");
        Write("committed.txt", "c");
        Git("add", "-A");
        Git("commit", "-m", "feature commit");
        Write("staged.txt", "s");
        Git("add", "staged.txt");
        Write("tracked.txt", "v2");
        Write("new dir/untracked file.txt", "u");

        var result = new LocalGitOperations(_root).ChangedFilesSince(null);

        Assert.True(result.Succeeded, result.FailureReason);
        Assert.Equal(new[] { "committed.txt", "new dir/untracked file.txt", "staged.txt", "tracked.txt" }, result.Files.OrderBy(f => f, StringComparer.Ordinal));
        Assert.Contains("main", result.ResolvedBaseRef);
    }

    [Fact]
    public void ChangedFilesSince_ExplicitBaseRef_ComparesAgainstIt()
    {
        InitWithOneCommit();
        Write("second.txt", "2");
        Git("add", "-A");
        Git("commit", "-m", "second");
        Write("third.txt", "3");
        Git("add", "-A");
        Git("commit", "-m", "third");

        var result = new LocalGitOperations(_root).ChangedFilesSince("HEAD~1");

        Assert.Equal(new[] { "third.txt" }, result.Files);
    }

    [Fact]
    public void ChangedFilesSince_UnknownBaseRef_ReturnsFailedWithReason()
    {
        InitWithOneCommit();

        var result = new LocalGitOperations(_root).ChangedFilesSince("no-such-ref");

        Assert.False(result.Succeeded);
        Assert.Contains("no-such-ref", result.FailureReason);
    }

    [Fact]
    public void ChangedFilesSince_NotAGitRepository_ReturnsFailedWithReason_NeverThrows()
    {
        var result = new LocalGitOperations(_root).ChangedFilesSince(null);

        Assert.False(result.Succeeded);
        Assert.Contains("not a git repository", result.FailureReason);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void ChangedFilesSince_RepositoryWithNoCommits_ReturnsFailedWithReason()
    {
        Git("init", "-b", "main");

        var result = new LocalGitOperations(_root).ChangedFilesSince(null);

        Assert.False(result.Succeeded);
        Assert.Contains("no commits", result.FailureReason);
    }

    [Fact]
    public void ChangedFilesSince_NoDefaultBranchResolvable_FallsBackToHead_AndSaysSo()
    {
        InitWithOneCommit(branch: "trunk");
        Write("tracked.txt", "v2");

        var result = new LocalGitOperations(_root).ChangedFilesSince(null);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "tracked.txt" }, result.Files);
        Assert.Contains("HEAD", result.ResolvedBaseRef);
        Assert.Contains("only uncommitted changes", result.ResolvedBaseRef);
    }
}
