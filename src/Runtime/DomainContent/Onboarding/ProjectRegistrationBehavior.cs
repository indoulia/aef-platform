using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.Onboarding;

// The executable realization of SKL-EXE-0016 (framework/execution/skills/
// project-registration-and-workspace-initialization.md)'s Behavior: given
// S19's Draft profiles, confirm no Id collision, initialize
// .ai-workspace/'s six categories, and record a pinned-version framework
// sync - or escalate as a Terminal Failure on collision, never silently
// overwriting an existing registration.
public sealed class ProjectRegistrationBehavior : IBehavior
{
    private readonly ProjectRegistrationRequest _request;

    public ProjectRegistrationBehavior(ProjectRegistrationRequest request) => _request = request;

    public object? Execute(RuntimeExecutionContext context)
    {
        if (_request.ExistingRegisteredIds.Contains(_request.ProjectProfile.Id))
        {
            return RegistrationOutcome.Failed(
                $"Registration conflict: Id '{_request.ProjectProfile.Id}' is already registered - cannot overwrite an existing Project Profile.");
        }

        var frameworkSync = new FrameworkSyncRecord(_request.PinnedFrameworkVersion);
        return RegistrationOutcome.Registered(_request.ProjectProfile, _request.TechnologyProfile, frameworkSync);
    }
}
