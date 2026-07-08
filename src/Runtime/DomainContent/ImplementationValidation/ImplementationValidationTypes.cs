using Runtime.DomainContent.EngineeringSpecificationReview;

namespace Runtime.DomainContent.ImplementationValidation;

public sealed record ConformanceFinding(string Area, string Classification, string Rationale);

public sealed record SpecificationConformanceValidationRequest(object? SpecificationInstance, object ImplementationLog, object TestPlanAndResults);

public sealed record SpecificationConformanceValidationResult
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public IReadOnlyList<ConformanceFinding>? ConformanceFindings { get; }
    public IReadOnlyList<string>? UncoveredSpecificationSections { get; }

    private SpecificationConformanceValidationResult(bool isEscalated, string? escalationReason, IReadOnlyList<ConformanceFinding>? conformanceFindings, IReadOnlyList<string>? uncoveredSpecificationSections)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ConformanceFindings = conformanceFindings;
        UncoveredSpecificationSections = uncoveredSpecificationSections;
    }

    public static SpecificationConformanceValidationResult Completed(IReadOnlyList<ConformanceFinding> findings, IReadOnlyList<string> uncoveredSections) =>
        new(false, null, findings, uncoveredSections);

    public static SpecificationConformanceValidationResult Escalated(string reason) => new(true, reason, null, null);
}

public sealed record ArchitectureStandardsConformanceReviewRequest(object TechnologyProfile, object Documentation, object? SpecificationInstance);

public sealed record ArchitectureStandardsConformanceReviewResult
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public IReadOnlyList<ConformanceFinding>? ConformanceFindings { get; }

    private ArchitectureStandardsConformanceReviewResult(bool isEscalated, string? escalationReason, IReadOnlyList<ConformanceFinding>? conformanceFindings)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ConformanceFindings = conformanceFindings;
    }

    public static ArchitectureStandardsConformanceReviewResult Completed(IReadOnlyList<ConformanceFinding> findings) => new(false, null, findings);

    public static ArchitectureStandardsConformanceReviewResult Escalated(string reason) => new(true, reason, null);
}

public sealed record ValidationFindingsSynthesisRequest(IReadOnlyList<ConformanceFinding> ConformanceFindings);

public sealed record ValidationFindingsSynthesisResult
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public EngineeringReviewInstance? ReviewInstance { get; }
    public string? MergeReadinessSignal { get; }

    private ValidationFindingsSynthesisResult(bool isEscalated, string? escalationReason, EngineeringReviewInstance? reviewInstance, string? mergeReadinessSignal)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ReviewInstance = reviewInstance;
        MergeReadinessSignal = mergeReadinessSignal;
    }

    public static ValidationFindingsSynthesisResult Completed(EngineeringReviewInstance reviewInstance, string mergeReadinessSignal) =>
        new(false, null, reviewInstance, mergeReadinessSignal);

    public static ValidationFindingsSynthesisResult Escalated(string reason) => new(true, reason, null, null);
}

internal sealed record AiConformanceFindingDto(string Area, string Classification, string Rationale);

internal sealed record AiConformanceValidationResponse(List<AiConformanceFindingDto> Conformance_Findings, List<string>? Uncovered_Specification_Sections);

internal sealed record AiArchitectureReviewResponse(List<AiConformanceFindingDto> Conformance_Findings);

internal sealed record AiSynthesisResponse(AiSynthesisReviewInstanceDto Review_Instance, string Merge_Readiness_Signal);

internal sealed record AiSynthesisReviewInstanceDto(string Review_Target, List<AiSynthesisFindingDto> Findings);

internal sealed record AiSynthesisFindingDto(string Area, string Severity, string Reason, string Evidence, string Recommendation);
