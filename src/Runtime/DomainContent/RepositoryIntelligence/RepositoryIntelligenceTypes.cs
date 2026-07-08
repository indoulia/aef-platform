// Shared across all four Skills in this folder, mirroring
// PCT-EXE-0030/0031/0032/0033's own Input/Output shapes exactly.
using Runtime.Execution;

namespace Runtime.DomainContent.RepositoryIntelligence;

public enum FindingClass { Pass, Warning, Fail, Unknown }

// Findings-Manifest-by-stable-Id shape (SCH-DEL-0005 §"Required sections").
public sealed record IntelligenceFinding(string Id, string Area, FindingClass Class, string Summary, string Evidence, bool SpotChecked = false);

// PCT-EXE-0030
public sealed record RepositoryUnderstandingRequest(string RequestScope, RepositoryExecutionContext RepositoryContext);
public sealed record RepositoryUnderstandingResult(IReadOnlyList<IntelligenceFinding> Findings, string Answer);

// PCT-EXE-0031
public sealed record CodeIntelligenceRequest(string RequestScope, RepositoryExecutionContext RepositoryContext);
public sealed record CodeIntelligenceResult(IReadOnlyList<IntelligenceFinding> Findings);

// PCT-EXE-0032
public sealed record EvidenceChainStep(string Step, IReadOnlyList<string> EvdIds, string InferentialLink);
public sealed record AlternativeHypothesis(string Hypothesis, string SupportingEvidence);
public sealed record DiagnosticsRequest(string ReportedProblem, IReadOnlyList<string> EvidenceSources);
public sealed record DiagnosticsResult(
    IReadOnlyList<EvidenceChainStep> EvidenceChain,
    int ConfidenceScore,
    string ConfidenceJustification,
    IReadOnlyList<AlternativeHypothesis> AlternativeHypotheses,
    IReadOnlyList<string> ConflictingEvidence)
{
    public static DiagnosticsResult NoEvidenceSupplied() => new(
        Array.Empty<EvidenceChainStep>(), 0,
        "No evidence sources were supplied - a root cause cannot be assessed without evidence, per PCT-EXE-0032's own precondition.",
        Array.Empty<AlternativeHypothesis>(), Array.Empty<string>());
}

// PCT-EXE-0033
public sealed record CombinedIntelligenceFindings(
    RepositoryUnderstandingResult? Understanding,
    CodeIntelligenceResult? CodeIntelligence,
    DiagnosticsResult? Diagnostics);
public sealed record PendingReviewInstance(string ReviewTarget, string Summary);
public sealed record RecommendationReport(
    string ReportType,
    string AssessedRepositoryScope,
    IReadOnlyList<IntelligenceFinding> Findings,
    IReadOnlyList<string> ProvenanceNotes);
public sealed record RecommendationSynthesisResult(RecommendationReport ReportInstance, bool RequiresHumanApproval, PendingReviewInstance? ReviewInstance);
