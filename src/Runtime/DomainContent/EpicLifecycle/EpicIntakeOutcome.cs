namespace Runtime.DomainContent.EpicLifecycle;

public enum EpicIntakeStatus
{
    PendingClarification,
    Resolved,
}

// Epic-scoped mirror of Runtime.DomainContent.FeatureLifecycle.IntakeOutcome
// - a fresh, independent type per this Epic's own explicit boundary ("a new,
// Epic-scoped Agent/Skill pair"), not a shared/reused type from the
// FeatureLifecycle module.
public sealed record EpicIntakeOutcome(EpicIntakeStatus Status, string? UnresolvedQuestion)
{
    public static EpicIntakeOutcome PendingClarification(string question) => new(EpicIntakeStatus.PendingClarification, question);

    public static readonly EpicIntakeOutcome Resolved = new(EpicIntakeStatus.Resolved, UnresolvedQuestion: null);
}
