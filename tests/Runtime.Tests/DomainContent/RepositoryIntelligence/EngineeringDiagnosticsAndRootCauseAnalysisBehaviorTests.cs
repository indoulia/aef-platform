using Runtime.Context;
using Runtime.DomainContent.RepositoryIntelligence;

namespace Runtime.Tests.DomainContent.RepositoryIntelligence;

public class EngineeringDiagnosticsAndRootCauseAnalysisBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(DiagnosticsRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["DiagnosticsRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "RepoIntel-Diagnostics", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsNoEvidenceSupplied()
    {
        var behavior = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior();

        var result = (DiagnosticsResult)behavior.Execute(ContextWith(null))!;

        Assert.Empty(result.EvidenceChain);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Fact]
    public void Execute_EmptyEvidenceSources_ReturnsNoEvidenceSupplied()
    {
        var behavior = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior();

        var result = (DiagnosticsResult)behavior.Execute(ContextWith(new DiagnosticsRequest("NullReferenceException on startup", Array.Empty<string>())))!;

        Assert.Empty(result.EvidenceChain);
    }

    [Fact]
    public void Execute_TwoEvidenceSources_BuildsChainAndAlternativeHypothesesWithLowConfidence()
    {
        var behavior = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior();
        var request = new DiagnosticsRequest("NullReferenceException on startup", new[] { "log excerpt A", "exception stack trace B" });

        var result = (DiagnosticsResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal(2, result.EvidenceChain.Count);
        Assert.Equal(2, result.AlternativeHypotheses.Count);
        Assert.Equal(2, result.ConfidenceScore);
        Assert.NotEmpty(result.ConfidenceJustification);
        Assert.Empty(result.ConflictingEvidence);
    }
}
