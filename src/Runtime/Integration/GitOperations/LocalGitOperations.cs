using System.Diagnostics;

namespace Runtime.Integration.GitOperations;

// The one concrete binding of IGitOperations in scope: local, on-disk Git
// repository. Every git invocation uses ProcessStartInfo.ArgumentList,
// never a concatenated command-line string - the same injection-safety
// discipline LocalGitChangeVerifier/ProcessVerificationRunner already
// apply. A separate, independent adapter from IIsolatedChangeVerifier
// (Architecture Decision 1) - not a replacement, not a shared base class.
public sealed class LocalGitOperations : IGitOperations
{
    private readonly string _repositoryRoot;

    public LocalGitOperations(string repositoryRoot)
    {
        if (string.IsNullOrEmpty(repositoryRoot))
        {
            throw new ArgumentException("repositoryRoot must not be null or empty.", nameof(repositoryRoot));
        }

        _repositoryRoot = repositoryRoot;
    }

    public bool IsGitRepository() => RunGit("rev-parse", "--is-inside-work-tree").ExitCode == 0;

    public GitStatusResult GetStatus()
    {
        var branchResult = RunGit("rev-parse", "--abbrev-ref", "HEAD");
        var currentBranch = branchResult.ExitCode == 0 ? branchResult.StandardOutput.Trim() : null;

        var statusResult = RunGit("status", "--porcelain");
        var modified = new List<string>();
        var untracked = new List<string>();

        if (statusResult.ExitCode == 0)
        {
            foreach (var line in statusResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length < 4)
                {
                    continue;
                }

                var marker = trimmed[..2];
                var path = trimmed[3..];
                if (marker[0] is 'R' or 'C')
                {
                    // Rename/copy porcelain lines are "old -> new" - the
                    // destination path is what a caller means by "changed".
                    var arrowIndex = path.IndexOf(" -> ", StringComparison.Ordinal);
                    if (arrowIndex >= 0)
                    {
                        path = path[(arrowIndex + 4)..];
                    }
                }

                if (marker == "??")
                {
                    untracked.Add(path);
                }
                else
                {
                    modified.Add(path);
                }
            }
        }

        return new GitStatusResult(currentBranch, modified.Count == 0 && untracked.Count == 0, modified, untracked);
    }

    public string GetDiff()
    {
        var result = RunGit("diff");
        return result.ExitCode == 0 ? result.StandardOutput : string.Empty;
    }

    public GitOperationResult CreateBranch(string branchName)
    {
        if (string.IsNullOrEmpty(branchName))
        {
            return GitOperationResult.Failure("branchName must not be null or empty.");
        }

        var result = RunGit("branch", branchName);
        return result.ExitCode == 0 ? GitOperationResult.Ok : GitOperationResult.Failure(result.StandardError.Trim());
    }

    public GitOperationResult Commit(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return GitOperationResult.Failure("message must not be null or empty.");
        }

        var addResult = RunGit("add", "-A");
        if (addResult.ExitCode != 0)
        {
            return GitOperationResult.Failure(addResult.StandardError.Trim());
        }

        var commitResult = RunGit("commit", "-m", message);
        return commitResult.ExitCode == 0 ? GitOperationResult.Ok : GitOperationResult.Failure(commitResult.StandardError.Trim());
    }

    public MergeReadinessResult CheckMergeReadiness(string branchName, string defaultBranchName)
    {
        var reasons = new List<string>();

        var branchExists = RunGit("rev-parse", "--verify", "--quiet", $"refs/heads/{branchName}").ExitCode == 0;
        if (!branchExists)
        {
            reasons.Add($"Branch '{branchName}' does not exist.");
        }

        if (string.Equals(branchName, defaultBranchName, StringComparison.Ordinal))
        {
            reasons.Add($"'{branchName}' is the default branch - nothing to merge into itself.");
        }

        // A working tree's clean/dirty state belongs to whatever is
        // currently checked out, not to an arbitrary named branch - a
        // symlink-free but analogous conflation to the path-escape class
        // of bug (Architecture Decision 2's own "evidence-only, never
        // guess" discipline applies here too). Only attribute this signal
        // to branchName when it is actually the current checkout; a
        // non-checked-out branch's own tree cannot be inspected without
        // mutating state (checking it out), which this evidence-only
        // check deliberately never does.
        var status = GetStatus();
        if (string.Equals(status.CurrentBranch, branchName, StringComparison.Ordinal) && !status.IsClean)
        {
            reasons.Add("The working tree has uncommitted changes.");
        }

        return new MergeReadinessResult(reasons.Count == 0, reasons);
    }

    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(120);

    private (int ExitCode, string StandardOutput, string StandardError) RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the 'git' process - is Git installed and on PATH?");

        // Reading one stream to completion before starting the other
        // deadlocks as soon as the un-drained stream fills its OS pipe
        // buffer (e.g. `git diff` on a large change plus any stderr
        // warning) - both streams must be drained concurrently.
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            TryKill(process);
            throw new TimeoutException($"git {string.Join(' ', arguments)} did not exit within {ProcessTimeout}.");
        }

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();

        return (process.ExitCode, standardOutput, standardError);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort only - the process may have exited between the
            // timeout check and this call.
        }
    }
}
