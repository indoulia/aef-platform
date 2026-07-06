namespace Runtime.DomainContent.Onboarding;

// Evidence-derived input to SKL-EXE-0015's Adopt/Defer and Id-sequencing
// logic (CU-3 Hardening Sprint, docs/aep/CU-3-Hardening-Sprint-Plan.md,
// Story H1) - replaces the prior hard-coded stub
// (epics/decisions/0007-ferret-first-production-onboarding-findings.md,
// F1/F2). CandidateStandardIds is the universe of Standards under
// consideration for this assessment - supplied by the invoking Agent (e.g.
// from a live Catalog query), never invented by this Skill.
// KnownSatisfiedStandardIds is the subset the assessor has real evidence
// for. ExistingProjectArtifactCount drives sequential, taxonomy-conformant
// Id assignment (docs/discovery/taxonomy/02-id-standard.md §3), defaulting
// to 0 for a project's first assessment.
public sealed record AdoptionEvidence(
    IReadOnlyCollection<string> CandidateStandardIds,
    IReadOnlyCollection<string> KnownSatisfiedStandardIds,
    int ExistingProjectArtifactCount = 0);
