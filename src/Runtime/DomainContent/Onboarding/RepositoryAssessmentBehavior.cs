using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.Onboarding;

// The executable realization of SKL-EXE-0015
// (framework/execution/skills/repository-assessment-and-profile-selection.md)'s
// Behavior: given a repository's assessed characteristics and real Adopt/
// Defer evidence, produce Draft Technology and Project Profiles with a
// sequential, taxonomy-conformant Id, reach a confirmed-no-technology
// terminal state, or escalate rather than guess. Never calls
// IRegistry.Register - it holds no reference to a Registry at all, so
// Registration (Story S20's own step) is structurally impossible here.
// Hardened by the CU-3 Hardening Sprint (docs/aep/CU-3-Hardening-Sprint-Plan.md,
// Story H1) after Ferret's real onboarding found the Adopt/Defer output and
// Id generation were a hard-coded stub, not evidence-derived
// (epics/decisions/0007-ferret-first-production-onboarding-findings.md,
// F1/F2). Amended by ADR-0013 / Epic 0008 Story S28 (docs/aep/ARB-Repository-Technology-Domain-Model.md):
// this class - unlike S24's deliberately non-invasive change - is
// explicitly in scope to branch on TechnologyDetection's three-valued
// outcome instead of a scalar PrimaryStack (a disclosed departure from
// S24's own "never re-touch this file" pattern, per epics/decisions/0014-...md,
// since this redesign changes the shape of the fact itself, not merely
// who supplies it).
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

        var detection = _repository.TechnologyDetection;

        if (detection is null || detection.Status == TechnologyDetectionStatus.Inconclusive)
        {
            return AssessmentOutcome.Escalated($"Repository '{_repository.Name}'s technology scan was inconclusive - the traversal could not complete for a reason unrelated to its actual content, so this cannot be assessed without guessing.");
        }

        if (detection.Status == TechnologyDetectionStatus.ConfirmedEmpty)
        {
            // A confirmed, confident absence - never escalated as if
            // ambiguous, and never satisfied by fabricating a Technology
            // Profile entry to force a Draft through (docs/aep/ARB-Repository-Technology-Domain-Model.md
            // §6, Known Limitation #1). No Standards-adoption decision is
            // made in this case either - checked and returned before the
            // candidate-Standards guard below.
            return AssessmentOutcome.ConfirmedNoTechnology;
        }

        if (_evidence.CandidateStandardIds.Count == 0)
        {
            return AssessmentOutcome.Escalated($"No candidate Standards were supplied for assessment - cannot produce a conforming Project Profile (minimum one Adoption List entry, per SCH-PRJ-0001) without guessing which Standards to consider.");
        }

        var sequenceId = (_evidence.ExistingProjectArtifactCount + 1).ToString("D4");

        var technologyProfile = new TechnologyProfileDraft($"TPR-PRJ-{sequenceId}", detection.Technologies);

        var adopted = _evidence.CandidateStandardIds.Where(id => _evidence.KnownSatisfiedStandardIds.Contains(id)).ToList();
        var deferred = _evidence.CandidateStandardIds.Where(id => !_evidence.KnownSatisfiedStandardIds.Contains(id)).ToList();

        var projectProfile = new ProjectProfileDraft($"PPR-PRJ-{sequenceId}", _repository.Name, Adopted: adopted, Deferred: deferred);

        return AssessmentOutcome.Assessed(technologyProfile, projectProfile);
    }
}
