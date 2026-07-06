namespace Runtime.DomainContent.Onboarding;

public enum RegistrationStatus
{
    Registered,
    TerminalFailure,
}

// SKL-EXE-0016's two declared outputs (framework/execution/skills/
// project-registration-and-workspace-initialization.md's Behavior): a
// registered Project+Technology Profile pair with an initialized workspace
// and a pinned framework sync record, or a Terminal Failure on an Id
// collision - registration never silently overwrites.
public sealed record RegistrationOutcome(
    RegistrationStatus Status,
    ProjectProfileDraft? ProjectProfile,
    TechnologyProfileDraft? TechnologyProfile,
    IReadOnlyList<string>? WorkspaceCategories,
    FrameworkSyncRecord? FrameworkSync,
    string? EscalationReason)
{
    public static RegistrationOutcome Registered(ProjectProfileDraft projectProfile, TechnologyProfileDraft technologyProfile, FrameworkSyncRecord frameworkSync) =>
        new(RegistrationStatus.Registered, projectProfile, technologyProfile, WorkspaceInitialization.Categories, frameworkSync, EscalationReason: null);

    public static RegistrationOutcome Failed(string reason) =>
        new(RegistrationStatus.TerminalFailure, null, null, null, null, reason);
}
