using Runtime.Context;
using Runtime.DomainContent.RepositoryMaintenance;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryMaintenance;

public class RepositoryHealthAssessmentBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(RepositoryHealthAssessmentRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["RepositoryHealthAssessmentRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Maintenance-Health", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsNoFindingsWithOpenItem()
    {
        var behavior = new RepositoryHealthAssessmentBehavior();

        var result = (RepositoryHealthAssessmentResult)behavior.Execute(ContextWith(null))!;

        Assert.Empty(result.Findings);
        Assert.Contains(result.OpenItems, i => i.Contains("no repository health assessment request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_AllEightDimensions_ClassifiedUnknown_NeverFabricated()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new RepositoryHealthAssessmentBehavior();

        var result = (RepositoryHealthAssessmentResult)behavior.Execute(
            ContextWith(new RepositoryHealthAssessmentRequest("Update NuGet packages", repoContext)))!;

        Assert.Equal(8, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal("UNKNOWN", f.Classification));
        Assert.All(result.Findings, f => Assert.False(string.IsNullOrWhiteSpace(f.Rationale)));
    }

    [Fact]
    public void Execute_EveryDimensionAppearsExactlyOnce()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new RepositoryHealthAssessmentBehavior();

        var result = (RepositoryHealthAssessmentResult)behavior.Execute(
            ContextWith(new RepositoryHealthAssessmentRequest("Update NuGet packages", repoContext)))!;

        var expectedDimensions = new[]
        {
            "Dependency health", "SDK health", "Package health", "Configuration health",
            "Project structure health", "Build health", "Documentation health", "Code hygiene",
        };
        Assert.Equal(expectedDimensions.OrderBy(d => d), result.Findings.Select(f => f.Dimension).OrderBy(d => d));
    }

    [Fact]
    public void Execute_WithTechnologyProfile_RationaleReflectsDetectedStack()
    {
        var technologyProfile = new Runtime.DomainContent.Onboarding.TechnologyProfileDraft("TPR-0001", new[] { new Runtime.Integration.RepositoryScanning.TechnologyEvidence("dotnet", "marker file(s) found at: repository root") });
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, technologyProfile);
        var behavior = new RepositoryHealthAssessmentBehavior();

        var result = (RepositoryHealthAssessmentResult)behavior.Execute(
            ContextWith(new RepositoryHealthAssessmentRequest("Update NuGet packages", repoContext)))!;

        Assert.All(result.Findings, f => Assert.Contains("dotnet", f.Rationale));
    }

    [Fact]
    public void Execute_WithoutTechnologyProfile_RationaleReportsNoProfile_NeverFabricated()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new RepositoryHealthAssessmentBehavior();

        var result = (RepositoryHealthAssessmentResult)behavior.Execute(
            ContextWith(new RepositoryHealthAssessmentRequest("Update NuGet packages", repoContext)))!;

        Assert.All(result.Findings, f => Assert.Contains("No Technology Profile", f.Rationale));
    }
}
