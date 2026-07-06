namespace Runtime.DomainContent.EpicLifecycle;

public enum EpicRoadmapStatus
{
    AwaitingReview,
    PlanPopulated,
}

// Epic-scoped mirror of Runtime.DomainContent.FeatureLifecycle.RoadmapOutcome
// - see EpicIntakeOutcome's own comment for why this is a fresh type rather
// than a shared one.
public sealed record EpicRoadmapOutcome(EpicRoadmapStatus Status)
{
    public static readonly EpicRoadmapOutcome AwaitingReview = new(EpicRoadmapStatus.AwaitingReview);

    public static readonly EpicRoadmapOutcome PlanPopulated = new(EpicRoadmapStatus.PlanPopulated);
}
