namespace Runtime.Integration.RepositoryScanning;

// Epic 0008, Story S28 (ADR-0013; ai-engineering-framework repo,
// docs/aep/ARB-Repository-Technology-Domain-Model.md §1.2, INV-2/INV-3):
// the three-valued outcome of Technology Detection, replacing S23's
// scalar `string?` PrimaryStack, which collapsed two different facts -
// "the traversal completed and found nothing" and "the traversal could
// not complete for a reason unrelated to content" - into the same null.
public enum TechnologyDetectionStatus
{
    // The traversal completed and found one or more evidenced
    // technologies. Two or more entries (a Hybrid repository) is the
    // normal case, not an unresolved tie to be broken.
    Detected,

    // The traversal completed, within its bound, and found zero
    // recognized markers - a determinate, confident answer. Never
    // escalated as if ambiguous.
    ConfirmedEmpty,

    // The traversal could not complete for a reason unrelated to what
    // the repository actually contains (e.g. a listing failure partway
    // through). "We do not know" - structurally distinct from a
    // confirmed absence.
    Inconclusive,
}

// One Technology Inventory entry (SCH-PRJ-0002 v2.0.0's `Technologies`
// field, ai-engineering-framework repo): Name is drawn from
// StackDetector's own marker-table vocabulary (INV-8); Basis is a plain,
// human-readable description of the marker(s) observed - never an EVD
// artifact reference (INV-7).
public sealed record TechnologyEvidence(string Name, string Basis);

// The result of Technology Detection for one repository scan.
// Technologies is non-empty only when Status is Detected.
public sealed record TechnologyDetectionResult(TechnologyDetectionStatus Status, IReadOnlyList<TechnologyEvidence> Technologies)
{
    public static TechnologyDetectionResult Detected(IReadOnlyList<TechnologyEvidence> technologies) =>
        new(TechnologyDetectionStatus.Detected, technologies);

    public static readonly TechnologyDetectionResult ConfirmedEmpty =
        new(TechnologyDetectionStatus.ConfirmedEmpty, Array.Empty<TechnologyEvidence>());

    public static readonly TechnologyDetectionResult Inconclusive =
        new(TechnologyDetectionStatus.Inconclusive, Array.Empty<TechnologyEvidence>());
}
