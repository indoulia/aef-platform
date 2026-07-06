using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.Onboarding;

// The executable realization of SKL-EXE-0015
// (framework/execution/skills/repository-assessment-and-profile-selection.md)'s
// Behavior: given a repository's assessed characteristics and real Adopt/
// Defer evidence, produce Draft Technology and Project Profiles with a
// sequential, taxonomy-conformant Id, or escalate rather than guess. Never
// calls IRegistry.Register - it holds no reference to a Registry at all,
// so Registration (Story S20's own step) is structurally impossible here.
// Hardened by the CU-3 Hardening Sprint (docs/aep/CU-3-Hardening-Sprint-Plan.md,
// Story H1) after Ferret's real onboarding found the Adopt/Defer output and
// Id generation were a hard-coded stub, not evidence-derived
// (epics/decisions/0007-ferret-first-production-onboarding-findings.md,
// F1/F2).
public sealed class RepositoryAssessmentBehavior : IBehavior
{
    private readonly RepositoryDescriptor _repository;
    private readonly AdoptionEvidence _evidence;

    public RepositoryAssessmentBehavior(RepositoryDescriptor repository, AdoptionEvidence evidence)
    {
        _repository = repository;
        _evidence = evidence;
    }

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

        if (_evidence.CandidateStandardIds.Count == 0)
        {
            return AssessmentOutcome.Escalated($"No candidate Standards were supplied for assessment - cannot produce a conforming Project Profile (minimum one Adoption List entry, per SCH-PRJ-0001) without guessing which Standards to consider.");
        }

        var sequenceId = (_evidence.ExistingProjectArtifactCount + 1).ToString("D4");

        var technologyProfile = new TechnologyProfileDraft($"TPR-PRJ-{sequenceId}", _repository.PrimaryStack);

        var adopted = _evidence.CandidateStandardIds.Where(id => _evidence.KnownSatisfiedStandardIds.Contains(id)).ToList();
        var deferred = _evidence.CandidateStandardIds.Where(id => !_evidence.KnownSatisfiedStandardIds.Contains(id)).ToList();

        var projectProfile = new ProjectProfileDraft($"PPR-PRJ-{sequenceId}", _repository.Name, Adopted: adopted, Deferred: deferred);

        return AssessmentOutcome.Assessed(technologyProfile, projectProfile);
    }
}
