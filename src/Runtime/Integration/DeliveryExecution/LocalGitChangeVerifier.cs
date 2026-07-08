using System.Diagnostics;

namespace Runtime.Integration.DeliveryExecution;

// The sole concrete binding of IIsolatedChangeVerifier for a local, on-disk
// Git repository (Epic 0005, Story S30). Every git invocation uses
// ProcessStartInfo.ArgumentList (never a concatenated command-line string),
// so a branch name or commit sha can never be interpreted as a second
// shell command - the same injection-safety discipline this platform
// already applies via IRepositoryScanner's own "no process-execution
// argument" contract, extended here to a capability that must itself
// invoke a process to do its (still read-only) job.
public sealed class LocalGitChangeVerifier : IIsolatedChangeVerifier
{
    private readonly string _repositoryRoot;
    private readonly string _defaultBranchName;

    public LocalGitChangeVerifier(string repositoryRoot, string defaultBranchName)
    {
        if (string.IsNullOrEmpty(repositoryRoot))
        {
            throw new ArgumentException("repositoryRoot must not be null or empty.", nameof(repositoryRoot));
        }

        if (string.IsNullOrEmpty(defaultBranchName))
        {
            throw new ArgumentException("defaultBranchName must not be null or empty.", nameof(defaultBranchName));
        }

        _repositoryRoot = repositoryRoot;
        _defaultBranchName = defaultBranchName;
    }

    public bool BranchExists(string branchName)
    {
        if (string.IsNullOrEmpty(branchName))
        {
            return false;
        }

        var result = RunGit("rev-parse", "--verify", "--quiet", $"refs/heads/{branchName}");
        return result.ExitCode == 0;
    }

    public bool IsDefaultBranch(string branchName)
    {
        return string.Equals(branchName, _defaultBranchName, StringComparison.Ordinal);
    }

    public bool CommitExistsOnBranch(string branchName, string commitSha)
    {
        if (string.IsNullOrEmpty(branchName) || string.IsNullOrEmpty(commitSha))
        {
            return false;
        }

        var commitLookup = RunGit("cat-file", "-e", $"{commitSha}^{{commit}}");
        if (commitLookup.ExitCode != 0)
        {
            return false;
        }

        var reachability = RunGit("merge-base", "--is-ancestor", commitSha, $"refs/heads/{branchName}");
        return reachability.ExitCode == 0;
    }

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
        // buffer - both streams must be drained concurrently (same fix
        // already applied to LocalGitOperations.RunGit and
        // ProcessVerificationRunner.Run - issue #23).
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();

        return (process.ExitCode, standardOutput, standardError);
    }
}
