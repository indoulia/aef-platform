namespace Runtime.DomainContent.EngineeringSpecificationReview;

public sealed record SpecificationReviewRequest(object SpecificationInstance, int ReviewIteration);

public sealed record EngineeringReviewFinding(string Area, string Severity, string Reason, string Evidence, string Recommendation);

public sealed record EngineeringReviewInstance(string ReviewTarget, int ReviewIteration, IReadOnlyList<EngineeringReviewFinding> Findings);

public sealed record SpecificationReviewOutcome
{
    public bool IsEscalated { get; }
    public string? EscalationReason { get; }
    public EngineeringReviewInstance? ReviewInstance { get; }
    public string? FinalRecommendation { get; }

    private SpecificationReviewOutcome(bool isEscalated, string? escalationReason, EngineeringReviewInstance? reviewInstance, string? finalRecommendation)
    {
        IsEscalated = isEscalated;
        EscalationReason = escalationReason;
        ReviewInstance = reviewInstance;
        FinalRecommendation = finalRecommendation;
    }

    public static SpecificationReviewOutcome Completed(EngineeringReviewInstance reviewInstance, string finalRecommendation) =>
        new(false, null, reviewInstance, finalRecommendation);

    public static SpecificationReviewOutcome Escalated(string reason) => new(true, reason, null, null);
}

internal sealed record AiSpecificationReviewResponse(
    AiReviewInstanceDto? Review_Instance,
    string? Final_Recommendation,
    string? Status,
    string? Failure_Reason);

internal sealed record AiReviewInstanceDto(string Review_Target, int Review_Iteration, List<AiReviewFindingDto> Findings);

internal sealed record AiReviewFindingDto(string Area, string Severity, string Reason, string Evidence, string Recommendation);
