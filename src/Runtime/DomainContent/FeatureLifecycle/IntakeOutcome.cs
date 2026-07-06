namespace Runtime.DomainContent.FeatureLifecycle;

public enum IntakeStatus
{
    PendingClarification,
    Resolved,
}

public sealed record IntakeOutcome(IntakeStatus Status, string? UnresolvedQuestion)
{
    public static IntakeOutcome PendingClarification(string question) => new(IntakeStatus.PendingClarification, question);

    public static readonly IntakeOutcome Resolved = new(IntakeStatus.Resolved, UnresolvedQuestion: null);
}
