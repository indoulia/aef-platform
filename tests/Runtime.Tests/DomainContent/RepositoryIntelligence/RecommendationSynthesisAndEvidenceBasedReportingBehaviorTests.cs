using Runtime.Context;
using Runtime.DomainContent.RepositoryIntelligence;

namespace Runtime.Tests.DomainContent.RepositoryIntelligence;

public class RecommendationSynthesisAndEvidenceBasedReportingBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(CombinedIntelligenceFindings? combined)
    {
        var content = combined is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["CombinedIntelligenceFindings"] = combined };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "RepoIntel-Synthesis", content);
    }

    [Fact]
    public void Execute_NoCombinedFindingsSupplied_ReturnsEmptyReportWithoutApproval()
    {
        var behavior = new RecommendationSynthesisAndEvidenceBasedReportingBehavior();

        var result = (RecommendationSynthesisResult)behavior.Execute(ContextWith(null))!;

        Assert.False(result.RequiresHumanApproval);
        Assert.Null(result.ReviewInstance);
    }

    [Fact]
    public void Execute_UnderstandingOnlyWithNoFailFindings_DoesNotRequireApproval()
    {
        var understanding = new RepositoryUnderstandingResult(new[] { new IntelligenceFinding("RAU-0001", "Understanding", FindingClass.Pass, "found", "src") }, "answer");
        var behavior = new RecommendationSynthesisAndEvidenceBasedReportingBehavior();

        var result = (RecommendationSynthesisResult)behavior.Execute(ContextWith(new CombinedIntelligenceFindings(understanding, null, null)))!;

        Assert.False(result.RequiresHumanApproval);
        Assert.Null(result.ReviewInstance);
        Assert.Equal("Understanding", result.ReportInstance.ReportType);
    }

    [Fact]
    public void Execute_DiagnosticsPresent_RequiresApprovalWithRootCauseReviewInstance()
    {
        var diagnostics = DiagnosticsResult.NoEvidenceSupplied();
        var behavior = new RecommendationSynthesisAndEvidenceBasedReportingBehavior();

        var result = (RecommendationSynthesisResult)behavior.Execute(ContextWith(new CombinedIntelligenceFindings(null, null, diagnostics)))!;

        Assert.True(result.RequiresHumanApproval);
        Assert.NotNull(result.ReviewInstance);
        Assert.Equal("Root Cause Conclusion", result.ReviewInstance!.ReviewTarget);
        Assert.Equal("Root Cause Analysis", result.ReportInstance.ReportType);
    }

    [Fact]
    public void Execute_FailFindingWithoutDiagnostics_RequiresApprovalWithRiskClassificationReviewInstance()
    {
        var codeIntelligence = new CodeIntelligenceResult(new[] { new IntelligenceFinding("CIQ-0001", "Code Intelligence", FindingClass.Fail, "failed", "evidence") });
        var behavior = new RecommendationSynthesisAndEvidenceBasedReportingBehavior();

        var result = (RecommendationSynthesisResult)behavior.Execute(ContextWith(new CombinedIntelligenceFindings(null, codeIntelligence, null)))!;

        Assert.True(result.RequiresHumanApproval);
        Assert.Equal("Risk Classification", result.ReviewInstance!.ReviewTarget);
    }
}
