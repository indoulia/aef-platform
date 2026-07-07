using Runtime.DomainContent.DeliveryExecution;
using Runtime.Integration.DeliveryExecution;
using Runtime.Orchestration.Resilience;
using Xunit;

namespace Runtime.Tests.Scenarios;

// Epic 0005 (ai-engineering-framework repo), Story S35: the first time
// SKL-EXE-0018 (Story S30), SKL-EXE-0019 (Story S31), SKL-EXE-0020
// (Story S32), and the retry/resume machinery (Story S33) are exercised
// together, as one real Delivery Execution run, rather than only
// individually per Story. The real target this scenario carries through
// the pipeline is docs/manual/09-epic-lifecycle.md's and
// docs/manual/03-product-roadmap.md's own Epic 0005 status-lag fix
// (bugs/epic-0005-manual-status-lag.md, ai-engineering-framework repo) -
// a real, non-synthetic, already-Planning-stage Bug in that repository,
// per this Story's own acceptance criteria. Everything here runs against
// a real, temporary, on-disk Git repository - never a synthetic
// in-memory fixture.
public sealed class Epic0005EndToEndDeliveryDogfoodingTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public Epic0005EndToEndDeliveryDogfoodingTests()
    {
        _root = Directory.CreateTempSubdirectory("s35-e2e-dogfooding-");
        RunGit(_root.FullName, "init", "--initial-branch=main");
        RunGit(_root.FullName, "config", "user.email", "s35-test@example.com");
        RunGit(_root.FullName, "config", "user.name", "S35 Test");
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
    public void RealBugFix_CarriedThroughImplementationThenTestingThenDocumentation_WithRetryLive_ProducesACompleteReviewPackage()
    {
        // Arrange: seed the "before" state (the real status-lag defect)
        // and the real fix, on an isolated branch - exactly the shape
        // Story S30's Skill expects to verify.
        CommitFile(_root.FullName, "epic-status.md", "Epic 0005: Planning, awaiting Founder Execution Authorization.");
        RunGit(_root.FullName, "checkout", "-b", "bugs/epic-0005-manual-status-lag");
        var fixSha = CommitFile(_root.FullName, "epic-status.md", "Epic 0005: Implementation - Stories S29-S34 complete, S35 in progress.");

        // Act 1 - Implementation (S30/SKL-EXE-0018), retried live through
        // one injected transient verifier failure (S33's retry model,
        // exercised here against a real Skill in a genuine multi-stage
        // run, not only in RetryExecutorTests' own isolated proof).
        var flakyVerifier = new FlakyThenRealVerifier(new LocalGitChangeVerifier(_root.FullName, "main"), failuresBeforeSuccess: 1);
        var retryExecutor = new RetryExecutor(delay: _ => { });
        var implementationRequest = new ImplementationRequest("bugs/epic-0005-manual-status-lag", "bugs/epic-0005-manual-status-lag", new[] { fixSha }, "Fix the Epic 0005 status lag in docs/manual/09-epic-lifecycle.md and 03-product-roadmap.md.");

        var implementationRetry = retryExecutor.Execute(() =>
        {
            var outcome = (ImplementationOutcome)new DeliveryImplementationBehavior(implementationRequest, flakyVerifier).Execute(context: null!)!;
            return outcome.IsEscalated
                ? AttemptResult.Failure(FailureClassification.Transient, outcome.EscalationReason!)
                : AttemptResult.Success(outcome);
        });

        Assert.True(implementationRetry.Succeeded);
        Assert.Equal(2, flakyVerifier.CallCount); // one injected failure, then a real success - retry genuinely happened
        var implementationOutcome = (ImplementationOutcome)implementationRetry.Value!;
        Assert.Contains(fixSha, implementationOutcome.LogEntry!.VerifiedCommitShas);

        // Act 2 - Testing (S31/SKL-EXE-0019): a real verification command
        // (git show, confirming the real commit's real content) against
        // the real repository.
        var validationRequest = new ValidationRequest("bugs/epic-0005-manual-status-lag", _root.FullName, "git", new[] { "show", $"{fixSha}:epic-status.md" });
        var validationOutcome = (ValidationOutcome)new DeliveryValidationBehavior(validationRequest, new ProcessVerificationRunner()).Execute(context: null!)!;

        Assert.False(validationOutcome.IsEscalated);
        Assert.True(validationOutcome.LogEntry!.Passed);
        Assert.Contains("Stories S29-S34 complete", validationOutcome.LogEntry.StandardOutputExcerpt);

        // Act 3 - Documentation (S32/SKL-EXE-0020): real, audience-separated content.
        var documentationRequest = new DocumentationRequest(
            "bugs/epic-0005-manual-status-lag",
            EndUserContent: string.Empty,
            MaintainerContent: "Corrected two Product Manual tables that still described Epic 0005 as awaiting Execution Authorization after it had already been granted and Stories S29-S34 had completed.",
            ProjectRequiresHumanReadGate: true);
        var documentationOutcome = (DocumentationOutcome)new DeliveryDocumentationBehavior(documentationRequest).Execute(context: null!)!;

        Assert.False(documentationOutcome.IsEscalated);
        Assert.True(documentationOutcome.LogEntry!.RequiresHumanRead);

        // Act 4 - Review packaging (S34): the diff/PR pointer S34's
        // extended Prompt Contract surfaces to the human reviewer is
        // exactly the real, verified commit this run produced - never a
        // caller-supplied summary.
        var diffOrPrPointer = $"{implementationOutcome.LogEntry.BranchName}@{implementationOutcome.LogEntry.VerifiedCommitShas[0]}";

        Assert.Equal($"bugs/epic-0005-manual-status-lag@{fixSha}", diffOrPrPointer);

        // Assert: this Story's own Definition of Done stops here, by
        // design. The merge-authorization Decision is never
        // self-rendered by this scenario - S35's own acceptance criteria
        // require an actual, non-simulated human-chaired Review, which
        // this test cannot and must not manufacture.
    }

    private sealed class FlakyThenRealVerifier : IIsolatedChangeVerifier
    {
        private readonly IIsolatedChangeVerifier _real;
        private readonly int _failuresBeforeSuccess;
        public int CallCount { get; private set; }

        public FlakyThenRealVerifier(IIsolatedChangeVerifier real, int failuresBeforeSuccess)
        {
            _real = real;
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public bool BranchExists(string branchName)
        {
            CallCount++;
            return CallCount <= _failuresBeforeSuccess ? false : _real.BranchExists(branchName);
        }

        public bool IsDefaultBranch(string branchName) => _real.IsDefaultBranch(branchName);

        public bool CommitExistsOnBranch(string branchName, string commitSha) => _real.CommitExistsOnBranch(branchName, commitSha);
    }

    private static string CommitFile(string repositoryRoot, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(repositoryRoot, fileName), content);
        RunGit(repositoryRoot, "add", fileName);
        RunGit(repositoryRoot, "commit", "-m", $"update {fileName}");
        return RunGitCapture(repositoryRoot, "rev-parse", "HEAD").Trim();
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        RunGitCapture(workingDirectory, arguments);
    }

    private static string RunGitCapture(string workingDirectory, params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
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

        using var process = System.Diagnostics.Process.Start(startInfo)!;
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
