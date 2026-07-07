using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.RepositoryScanning;

namespace Runtime.DomainContent.Onboarding;

// AGT-EXE-0007's composition realization: wires the S22/S23/S27
// Integration Adapter Boundary (LocalFileSystemRepositoryScanner,
// StackDetector) into SKL-EXE-0015's already-governed Behavior
// (RepositoryAssessmentBehavior), per PCT-EXE-0015's amended Input
// contract - repository_name plus either an already-known
// is_readable/technology_detection_outcome/technologies triple, or a
// local_repository_root to derive them from (Epic 0008, Story S24;
// amended by Story S28, ADR-0013, to derive a three-valued
// TechnologyDetectionResult instead of a scalar PrimaryStack).
//
// Never re-implements SKL-EXE-0015's own reasoning and never touches
// RepositoryAssessmentBehavior.cs's own Execute logic from here: both
// constructors below end by handing off to that Behavior unchanged. The
// RepositoryDescriptor-accepting constructor is a pure pass-through -
// today's existing calling convention - so its behavior is byte-for-byte
// identical to constructing RepositoryAssessmentBehavior directly
// (acceptance criterion 2).
public sealed class RepositoryOnboardingAssessmentAgent : IBehavior
{
    private readonly RepositoryAssessmentBehavior _behavior;

    // Case (a): an already-known RepositoryDescriptor - today's existing
    // calling convention, unchanged.
    public RepositoryOnboardingAssessmentAgent(RepositoryDescriptor repository, AdoptionEvidence evidence)
    {
        _behavior = new RepositoryAssessmentBehavior(repository, evidence);
    }

    // Case (b): PCT-EXE-0015's local_repository_root field - derive
    // is_readable/technology_detection_outcome/technologies via the
    // S22/S23/S27 local-filesystem adapter, then delegate to the same
    // Behavior.
    public RepositoryOnboardingAssessmentAgent(string repositoryName, string localRepositoryRoot, AdoptionEvidence evidence)
        : this(DeriveDescriptor(repositoryName, localRepositoryRoot), evidence)
    {
    }

    // Technology detection requires reading the root's entries, which is
    // meaningless (and needlessly exercises the adapter) once the root
    // itself is already known not to be readable - so it is skipped in
    // that case, leaving TechnologyDetection null, which is exactly the
    // input RepositoryAssessmentBehavior needs to reach its own existing
    // "not readable" escalation (acceptance criterion 3).
    private static RepositoryDescriptor DeriveDescriptor(string repositoryName, string localRepositoryRoot)
    {
        var scanner = new LocalFileSystemRepositoryScanner(localRepositoryRoot);
        var isReadable = scanner.IsRootReadable();
        var technologyDetection = isReadable ? StackDetector.DetectTechnologies(scanner) : null;

        return new RepositoryDescriptor(repositoryName, isReadable, technologyDetection);
    }

    public object? Execute(RuntimeExecutionContext context) => _behavior.Execute(context);
}
