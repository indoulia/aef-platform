namespace Runtime.DomainContent.FeatureLifecycle;

public enum RoadmapStatus
{
    AwaitingReview,
    PlanPopulated,
}

public sealed record RoadmapOutcome(RoadmapStatus Status)
{
    public static readonly RoadmapOutcome AwaitingReview = new(RoadmapStatus.AwaitingReview);

    public static readonly RoadmapOutcome PlanPopulated = new(RoadmapStatus.PlanPopulated);
}
