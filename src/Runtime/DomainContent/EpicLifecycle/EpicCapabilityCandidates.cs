namespace Runtime.DomainContent.EpicLifecycle;

// The real docs/aep/I5-Capability-Unit-Architecture-and-Roadmap-Evolution.md
// Sec.2 CU-3/CU-4 candidate table, wired for S18's end-to-end dogfooding run
// (deferred from S16 by design - see features/s16-*.md's own scope note).
// The dependency marker Id and CU-3/CU-4 tie-break are both recorded in
// epics/decisions/0005-s18-dogfooding-wiring-decisions.md.
public static class EpicCapabilityCandidates
{
    // ORC-EXE-0001's Active registration is used as the "CU-1 satisfied"
    // marker: CU-2 (which registers ORC-EXE-0001) cannot exist without CU-1
    // already being built, so ORC-EXE-0001 Active is valid transitive
    // evidence for CU-1, without requiring CU-1 itself to register content
    // it never specified (Epic 0001/0002 registered only disclaimed,
    // must-never-cited-as-precedent synthetic fixtures per
    // epics/decisions/0004-walking-skeleton-trivial-content-selection.md).
    private const string Cu1SatisfiedMarkerId = "ORC-EXE-0001";

    public static readonly IReadOnlyList<CandidateItem> I5Table = new[]
    {
        new CandidateItem("CU-3", "Repository Discovery & Canonical Onboarding", Cu1SatisfiedMarkerId),
        new CandidateItem("CU-4", "Knowledge & Governance Substrate", Cu1SatisfiedMarkerId),
    };
}
