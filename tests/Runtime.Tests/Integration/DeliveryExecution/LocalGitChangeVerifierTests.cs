using System.Diagnostics;
using Runtime.Integration.DeliveryExecution;
using Xunit;

namespace Runtime.Tests.Integration.DeliveryExecution;

// Epic 0005 (ai-engineering-framework repo), Story S30: IIsolatedChangeVerifier's
// sole concrete binding, exercised against a real, temporary, on-disk Git
// repository per test - never a synthetic in-memory fixture, per this
// Epic's own Validation Strategy (mirroring Story S22/S25's own
// real-fixture convention for IRepositoryScanner).
public sealed class LocalGitChangeVerifierTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public LocalGitChangeVerifierTests()
    {
        _root = Directory.CreateTempSubdirectory("s30-git-verifier-tests-");
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

    // Structural guarantee behind RUL-DEL-0004's isolation boundary: this
    // contract's public surface has zero merge/push/checkout/write-capable
    // member, by static/reflective inspection - the same technique Story
    // S25 established for IRepositoryScanner.
    [Fact]
    public void Contract_HasNoMergePushCheckoutOrWriteCapableMember()
    {
        var mutatingKeywords = new[]
        {
            "merge", "push", "checkout", "reset", "rebase", "delete",
            "write", "create", "commit" /* as a verb; the one member using
            "Commit" as a noun (CommitExistsOnBranch) is asserted separately
            below to have a bool return, never an action-capable one */,
        };

        var interfaceType = typeof(IIsolatedChangeVerifier);
        var methods = interfaceType.GetMethods();

        foreach (var method in methods)
        {
            var nameLower = method.Name.ToLowerInvariant();
            var isCommitExistsQuery = method.Name == nameof(IIsolatedChangeVerifier.CommitExistsOnBranch);
            if (isCommitExistsQuery)
            {
                continue;
            }

            Assert.False(
                mutatingKeywords.Any(keyword => nameLower.Contains(keyword)),
                $"Method '{method.Name}' looks merge/push/checkout/write-capable, which IIsolatedChangeVerifier must never expose.");
        }

        // Every member returns a plain bool - never a live process handle,
        // stream, or other capability-bearing type.
        Assert.All(methods, m => Assert.Equal(typeof(bool), m.ReturnType));

        // Every parameter is a plain string - never a network address,
        // credential, or process-execution argument.
        var parameterTypes = methods.SelectMany(m => m.GetParameters()).Select(p => p.ParameterType);
        Assert.All(parameterTypes, t => Assert.Equal(typeof(string), t));
    }

    [Fact]
    public void BranchExists_TrueForARealBranch_FalseForAnUnknownName()
    {
        CommitFile(_root.FullName, "README.md", "hello");
        RunGit(_root.FullName, "branch", "feature/s30");
        var verifier = new LocalGitChangeVerifier(_root.FullName, "main");

        Assert.True(verifier.BranchExists("feature/s30"));
        Assert.False(verifier.BranchExists("no-such-branch"));
    }

    [Fact]
    public void IsDefaultBranch_TrueOnlyForTheConfiguredDefault()
    {
        var verifier = new LocalGitChangeVerifier(_root.FullName, "main");

        Assert.True(verifier.IsDefaultBranch("main"));
        Assert.False(verifier.IsDefaultBranch("feature/s30"));
    }

    [Fact]
    public void CommitExistsOnBranch_TrueWhenReachable_FalseWhenOnADifferentBranchOnly()
    {
        var mainSha = CommitFile(_root.FullName, "README.md", "hello");
        RunGit(_root.FullName, "checkout", "-b", "feature/s30");
        var featureSha = CommitFile(_root.FullName, "feature.txt", "feature content");
        var verifier = new LocalGitChangeVerifier(_root.FullName, "main");

        Assert.True(verifier.CommitExistsOnBranch("feature/s30", mainSha));
        Assert.True(verifier.CommitExistsOnBranch("feature/s30", featureSha));
        Assert.False(verifier.CommitExistsOnBranch("main", featureSha));
    }

    [Fact]
    public void CommitExistsOnBranch_FalseForACommitShaThatDoesNotExistAtAll()
    {
        CommitFile(_root.FullName, "README.md", "hello");
        var verifier = new LocalGitChangeVerifier(_root.FullName, "main");

        Assert.False(verifier.CommitExistsOnBranch("main", "0000000000000000000000000000000000000000"));
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
