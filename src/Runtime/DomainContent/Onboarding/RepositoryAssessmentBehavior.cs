using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.Onboarding;

// The executable realization of SKL-EXE-0015
// (framework/execution/skills/repository-assessment-and-profile-selection.md)'s
// Behavior: given a repository's assessed characteristics, produce Draft
// Technology and Project Profiles, or escalate rather than guess. Never
// calls IRegistry.Register - it holds no reference to a Registry at all,
// so Registration (Story S20's own step) is structurally impossible here.
public sealed class RepositoryAssessmentBehavior : IBehavior
{
    private readonly RepositoryDescriptor _repository;

    public RepositoryAssessmentBehavior(RepositoryDescriptor repository) => _repository = repository;

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!_repository.IsReadable)
        {
            return AssessmentOutcome.Escalated($"Repository '{_repository.Name}' is not readable - cannot assess without guessing.");
        }

        if (string.IsNullOrEmpty(_repository.PrimaryStack))
        {
            return AssessmentOutcome.Escalated($"Repository '{_repository.Name}' has no determinable primary technology stack - cannot select a Technology Profile without guessing.");
        }

        var technologyProfile = new TechnologyProfileDraft($"TPR-PRJ-{_repository.Name}", _repository.PrimaryStack);
        var projectProfile = new ProjectProfileDraft(
            $"PPR-PRJ-{_repository.Name}",
            _repository.Name,
            Adopted: new[] { "STD-GOV-0001", "STD-GOV-0003" },
            Deferred: new[] { "STD-QUA-0001" });

        return AssessmentOutcome.Assessed(technologyProfile, projectProfile);
    }
}
