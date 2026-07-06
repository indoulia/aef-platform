namespace Runtime.DomainContent.Onboarding;

public enum AssessmentStatus
{
    Assessed,
    Escalated,
}

// SKL-EXE-0015's two declared outputs (framework/execution/skills/
// repository-assessment-and-profile-selection.md's Behavior): both Draft
// profiles, or an explicit escalation - never a guess.
public sealed record AssessmentOutcome(AssessmentStatus Status, TechnologyProfileDraft? TechnologyProfile, ProjectProfileDraft? ProjectProfile, string? EscalationReason)
{
    public static AssessmentOutcome Assessed(TechnologyProfileDraft technologyProfile, ProjectProfileDraft projectProfile) =>
        new(AssessmentStatus.Assessed, technologyProfile, projectProfile, EscalationReason: null);

    public static AssessmentOutcome Escalated(string reason) =>
        new(AssessmentStatus.Escalated, null, null, reason);
}
