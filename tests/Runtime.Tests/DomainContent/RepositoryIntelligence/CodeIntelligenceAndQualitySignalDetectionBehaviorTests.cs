using Runtime.Context;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.RepositoryIntelligence;

public class CodeIntelligenceAndQualitySignalDetectionBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(CodeIntelligenceRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["CodeIntelligenceRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "RepoIntel-CodeIntelligence", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsUnknownFinding()
    {
        var behavior = new CodeIntelligenceAndQualitySignalDetectionBehavior();

        var result = (CodeIntelligenceResult)behavior.Execute(ContextWith(null))!;

        Assert.Single(result.Findings);
        Assert.Equal(FindingClass.Unknown, result.Findings[0].Class);
    }

    [Fact]
    public void Execute_NoTechnologyProfile_ReturnsUnknownForTestConvention()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new CodeIntelligenceAndQualitySignalDetectionBehavior();

        var result = (CodeIntelligenceResult)behavior.Execute(ContextWith(new CodeIntelligenceRequest("scope", repoContext)))!;

        Assert.Equal(FindingClass.Unknown, result.Findings[0].Class);
        Assert.Equal(2, result.Findings.Count);
    }

    [Fact]
    public void Execute_RepositoryWithTestsDirectory_ReturnsPassSpotChecked()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "tests"));
            var technologyProfile = new Runtime.DomainContent.Onboarding.TechnologyProfileDraft("TPR-0001", new[] { new Runtime.Integration.RepositoryScanning.TechnologyEvidence("dotnet", "marker file(s) found at: repository root") });
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, technologyProfile);
            var behavior = new CodeIntelligenceAndQualitySignalDetectionBehavior();

            var result = (CodeIntelligenceResult)behavior.Execute(ContextWith(new CodeIntelligenceRequest("scope", repoContext)))!;

            Assert.Equal(FindingClass.Pass, result.Findings[0].Class);
            Assert.True(result.Findings[0].SpotChecked);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_RepositoryWithoutTestsDirectory_ReturnsWarningSpotChecked()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var technologyProfile = new Runtime.DomainContent.Onboarding.TechnologyProfileDraft("TPR-0001", new[] { new Runtime.Integration.RepositoryScanning.TechnologyEvidence("dotnet", "marker file(s) found at: repository root") });
            var repoContext = new RepositoryExecutionContext(root, "test-repo", true, false, null, null, technologyProfile);
            var behavior = new CodeIntelligenceAndQualitySignalDetectionBehavior();

            var result = (CodeIntelligenceResult)behavior.Execute(ContextWith(new CodeIntelligenceRequest("scope", repoContext)))!;

            Assert.Equal(FindingClass.Warning, result.Findings[0].Class);
            Assert.True(result.Findings[0].SpotChecked);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
