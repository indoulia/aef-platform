namespace Runtime.DomainContent.Onboarding;

// Input to SKL-EXE-0016: the Draft profiles S19 produced, the project's own
// already-registered Ids (per-project-scoped, no central AEF-side registry -
// docs/discovery/composition/04-project-onboarding.md §1.2), and the AEF
// version to pin the sync at.
public sealed record ProjectRegistrationRequest(
    ProjectProfileDraft ProjectProfile,
    TechnologyProfileDraft TechnologyProfile,
    IReadOnlyCollection<string> ExistingRegisteredIds,
    string PinnedFrameworkVersion);
