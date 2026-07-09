using Runtime.Context;
using Runtime.DomainContent.RepositoryMigration;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryMigration;

public class MigrationAssessmentAndTransformationClassificationBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(TransformationClassificationRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["TransformationClassificationRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Migration-Assessment", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_RequiresClarification()
    {
        var behavior = new MigrationAssessmentAndTransformationClassificationBehavior();

        var result = (TransformationClassificationResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("Requires Clarification", result.TransformationClassification);
        Assert.Contains(result.OpenItems, i => i.Contains("no transformation classification request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_UpgradeGoal_ClassifiesAsTechnologyFrameworkUpgrade()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new MigrationAssessmentAndTransformationClassificationBehavior();

        var result = (TransformationClassificationResult)behavior.Execute(
            ContextWith(new TransformationClassificationRequest("Upgrade this repository to .NET 10", repoContext)))!;

        Assert.Equal("Technology/Framework Upgrade", result.TransformationClassification);
    }

    [Fact]
    public void Execute_ReplaceLibraryGoal_ClassifiesAsLibraryApiReplacement()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new MigrationAssessmentAndTransformationClassificationBehavior();

        var result = (TransformationClassificationResult)behavior.Execute(
            ContextWith(new TransformationClassificationRequest("Replace the logging library with a newer package", repoContext)))!;

        Assert.Equal("Library/API Replacement", result.TransformationClassification);
    }

    [Fact]
    public void Execute_AmbiguousGoal_RequiresClarification_NeverGuesses()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new MigrationAssessmentAndTransformationClassificationBehavior();

        var result = (TransformationClassificationResult)behavior.Execute(
            ContextWith(new TransformationClassificationRequest("Make it better", repoContext)))!;

        Assert.Equal("Requires Clarification", result.TransformationClassification);
        Assert.Contains(result.OpenItems, i => i.Contains("Technology/Framework Upgrade"));
    }

    [Fact]
    public void Execute_TargetState_AlwaysRequiresHumanInput_NeverFabricated()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new MigrationAssessmentAndTransformationClassificationBehavior();

        var result = (TransformationClassificationResult)behavior.Execute(
            ContextWith(new TransformationClassificationRequest("Upgrade this repository to .NET 10", repoContext)))!;

        Assert.Contains(result.OpenItems, i => i.Contains("Target State", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_CurrentStateWithTechnologyProfile_ReflectsDetectedStack()
    {
        var technologyProfile = new Runtime.DomainContent.Onboarding.TechnologyProfileDraft("TPR-0001", new[] { new Runtime.Integration.RepositoryScanning.TechnologyEvidence("dotnet", "marker file(s) found at: repository root") });
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, technologyProfile);
        var behavior = new MigrationAssessmentAndTransformationClassificationBehavior();

        var result = (TransformationClassificationResult)behavior.Execute(
            ContextWith(new TransformationClassificationRequest("Upgrade this repository to .NET 10", repoContext)))!;

        Assert.Contains("dotnet", result.CurrentState);
    }

    [Fact]
    public void Execute_CurrentStateWithoutTechnologyProfile_ReportsUnknown_NeverFabricated()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new MigrationAssessmentAndTransformationClassificationBehavior();

        var result = (TransformationClassificationResult)behavior.Execute(
            ContextWith(new TransformationClassificationRequest("Upgrade this repository to .NET 10", repoContext)))!;

        Assert.Contains("No Technology Profile", result.CurrentState);
    }
}
