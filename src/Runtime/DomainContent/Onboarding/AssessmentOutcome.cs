namespace Runtime.DomainContent.Onboarding;

public enum AssessmentStatus
{
    Assessed,

    // Epic 0008, Story S28 (ADR-0013): a confirmed, confident absence of
    // any evidenced technology - never an escalation, never a guess, and
    // never carrying either Draft profile (docs/aep/ARB-Repository-Technology-Domain-Model.md
    // §6, Known Limitation #1 - no Technology Profile can conform with an
    // empty Technologies list, so none is produced).
    ConfirmedNoTechnology,

    Escalated,
}

// SKL-EXE-0015's declared outputs (framework/execution/skills/
// repository-assessment-and-profile-selection.md's Behavior): both Draft
// profiles, a confirmed-no-technology terminal state, or an explicit
// escalation - never a guess.
public sealed record AssessmentOutcome(AssessmentStatus Status, TechnologyProfileDraft? TechnologyProfile, ProjectProfileDraft? ProjectProfile, string? EscalationReason)
{
    public static AssessmentOutcome Assessed(TechnologyProfileDraft technologyProfile, ProjectProfileDraft projectProfile) =>
        new(AssessmentStatus.Assessed, technologyProfile, projectProfile, EscalationReason: null);

    public static readonly AssessmentOutcome ConfirmedNoTechnology =
        new(AssessmentStatus.ConfirmedNoTechnology, null, null, EscalationReason: null);

    public static AssessmentOutcome Escalated(string reason) =>
        new(AssessmentStatus.Escalated, null, null, reason);
}
