using Runtime.Context;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Execution;
using Runtime.Integration.DeliveryExecution;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.Tests.DomainContent.RepositoryValidation;

public class RepositoryValidationSkillBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(RepositoryExecutionContext? repositoryContext)
    {
        var content = repositoryContext is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["RepositoryContext"] = repositoryContext };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "RepositoryValidation", content);
    }

    [Fact]
    public void Execute_NoRepositoryContextSupplied_Escalates()
    {
        var behavior = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(0, string.Empty, string.Empty)));

        var outcome = (ValidationOutcome)behavior.Execute(ContextWith(null))!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void Execute_UnrecognizedStack_Escalates()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(0, string.Empty, string.Empty)));

        var outcome = (ValidationOutcome)behavior.Execute(ContextWith(repoContext))!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void Execute_DotnetStackDetected_DelegatesAndRecordsOutcome()
    {
        var technologyProfile = new Runtime.DomainContent.Onboarding.TechnologyProfileDraft("TPR-0001", new[] { new TechnologyEvidence("dotnet", "marker file(s) found at: repository root") });
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, technologyProfile);
        var behavior = new RepositoryValidationSkillBehavior(new StubVerificationRunner(new VerificationRunResult(0, "passed", string.Empty)));

        var outcome = (ValidationOutcome)behavior.Execute(ContextWith(repoContext))!;

        Assert.False(outcome.IsEscalated);
        Assert.Equal("test-repo", outcome.LogEntry!.StoryReferenceId);
        Assert.True(outcome.LogEntry.Passed);
    }

    private sealed class StubVerificationRunner : IVerificationRunner
    {
        private readonly VerificationRunResult _result;

        public StubVerificationRunner(VerificationRunResult result) => _result = result;

        public VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments) => _result;
    }
}
