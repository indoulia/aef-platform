using Runtime.DomainContent.Onboarding;
using Runtime.Integration.RepositoryScanning;
using Xunit;

namespace Runtime.Tests.DomainContent.Onboarding;

// Epic 0008 (epics/0008-local-repository-scanner-implementation.md), Story
// S24: AGT-EXE-0007's composition realization wiring the S22/S23/S27
// Integration Adapter Boundary into the already-governed onboarding path,
// per PCT-EXE-0015's amended Input contract - without re-touching
// RepositoryAssessmentBehavior.cs (until Story S28, which does touch it -
// see epics/decisions/0014-...md and the Behavior's own amended Definition
// of Done). Amended by Story S28 (ADR-0013): DeriveDescriptor now derives
// a three-valued TechnologyDetectionResult instead of a scalar
// PrimaryStack.
//
// AC2 (regression) - the existing "already-known RepositoryDescriptor"
// calling convention must remain byte-for-byte identical to constructing
// RepositoryAssessmentBehavior directly: every scenario below mirrors one
// of RepositoryAssessmentBehaviorTests.cs's own scenarios verbatim,
// routed through RepositoryOnboardingAssessmentAgent's descriptor
// constructor instead, and asserts the identical outcome.
//
// AC1/AC3 (new local_repository_root path) - a real, temporary on-disk
// fixture directory is used for the successful-derivation case (never a
// mock/fake), matching S22/S23/S25's own established Validation strategy;
// a nonexistent path proves the unreadable-root case escalates via the
// existing "not readable" message, not a new failure mode.
public sealed class RepositoryOnboardingAssessmentAgentTests : IDisposable
{
    private readonly DirectoryInfo _root;

    public RepositoryOnboardingAssessmentAgentTests()
    {
        _root = Directory.CreateTempSubdirectory("s24-agent-tests-");
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch
        {
            // Best-effort cleanup only; a leftover temp directory is not a test failure.
        }
    }

    private static RepositoryDescriptor ReadableRepository(string name = "sample-repo") =>
        new(name, IsReadable: true, TechnologyDetectionResult.Detected(new[] { new TechnologyEvidence("dotnet", "Sample.csproj at repository root") }));

    // Mirrors RepositoryAssessmentBehaviorTests.ReadableRepositoryWithDetectedTechnology_ProducesDraftTechnologyAndProjectProfiles
    [Fact]
    public void DirectDescriptorConvention_ReadableRepositoryWithDetectedTechnology_ProducesDraftTechnologyAndProjectProfiles_IdenticalToBehaviorDirectly()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" },
            KnownSatisfiedStandardIds: new[] { "STD-GOV-0002" });

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(ReadableRepository(), evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(ReadableRepository(), evidence).Execute(context: null!)!;

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.TechnologyProfile!.Id, actual.TechnologyProfile!.Id);
        Assert.Equal(expected.TechnologyProfile.Technologies.Select(t => t.Name), actual.TechnologyProfile.Technologies.Select(t => t.Name));
        Assert.Equal(expected.ProjectProfile!.Id, actual.ProjectProfile!.Id);
        Assert.Equal(expected.ProjectProfile.Name, actual.ProjectProfile.Name);
        Assert.Equal(expected.ProjectProfile.Adopted, actual.ProjectProfile.Adopted);
        Assert.Equal(expected.ProjectProfile.Deferred, actual.ProjectProfile.Deferred);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
    }

    // Mirrors RepositoryAssessmentBehaviorTests.KnownSatisfiedStandards_AreAdopted_RemainderAreDeferred_EvidenceDerivedNotHardCoded
    [Fact]
    public void DirectDescriptorConvention_KnownSatisfiedStandards_AreAdopted_RemainderAreDeferred_IdenticalToBehaviorDirectly()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" },
            KnownSatisfiedStandardIds: new[] { "STD-GOV-0002" });

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(ReadableRepository(), evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(ReadableRepository(), evidence).Execute(context: null!)!;

        Assert.Equal(expected.ProjectProfile!.Adopted, actual.ProjectProfile!.Adopted);
        Assert.Equal(expected.ProjectProfile.Deferred, actual.ProjectProfile.Deferred);
    }

    // Mirrors RepositoryAssessmentBehaviorTests.NoCandidateStandardsSupplied_Escalates_NeverProducesAnEmptyAdoptionList
    [Fact]
    public void DirectDescriptorConvention_NoCandidateStandardsSupplied_Escalates_IdenticalToBehaviorDirectly()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: Array.Empty<string>(),
            KnownSatisfiedStandardIds: Array.Empty<string>());

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(ReadableRepository(), evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(ReadableRepository(), evidence).Execute(context: null!)!;

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
    }

    // Mirrors RepositoryAssessmentBehaviorTests.UnreadableRepository_Escalates_NeverGuesses
    [Fact]
    public void DirectDescriptorConvention_UnreadableRepository_Escalates_IdenticalToBehaviorDirectly()
    {
        var descriptor = new RepositoryDescriptor("locked-repo", IsReadable: false, TechnologyDetection: null);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(descriptor, evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(descriptor, evidence).Execute(context: null!)!;

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
        Assert.Null(actual.TechnologyProfile);
        Assert.Null(actual.ProjectProfile);
    }

    // Mirrors RepositoryAssessmentBehaviorTests.ReadableRepositoryWithInconclusiveScan_Escalates_NeverGuesses
    [Fact]
    public void DirectDescriptorConvention_ReadableRepositoryWithInconclusiveScan_Escalates_IdenticalToBehaviorDirectly()
    {
        var descriptor = new RepositoryDescriptor("mystery-repo", IsReadable: true, TechnologyDetectionResult.Inconclusive);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(descriptor, evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(descriptor, evidence).Execute(context: null!)!;

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
    }

    // AC1: local_repository_root supplied, is_readable/technologies are not -
    // derives a real RepositoryDescriptor via the S22/S23/S27 adapter (a
    // real, temporary on-disk fixture directory containing a .csproj
    // marker) and calls RepositoryAssessmentBehavior with a Detected result.
    [Fact]
    public void LocalRepositoryRoot_ReadableWithDotnetMarker_DerivesDescriptorAndProducesDraftProfiles()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");

        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003" },
            KnownSatisfiedStandardIds: new[] { "STD-GOV-0002" });

        var agent = new RepositoryOnboardingAssessmentAgent("sample-repo", _root.FullName, evidence);
        var outcome = (AssessmentOutcome)agent.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Assessed, outcome.Status);
        Assert.NotNull(outcome.TechnologyProfile);
        Assert.Equal(new[] { "dotnet" }, outcome.TechnologyProfile!.Technologies.Select(t => t.Name));
        Assert.NotNull(outcome.ProjectProfile);
        Assert.Equal(new[] { "STD-GOV-0002" }, outcome.ProjectProfile!.Adopted);
        Assert.Equal(new[] { "STD-GOV-0003" }, outcome.ProjectProfile.Deferred);
        Assert.Null(outcome.EscalationReason);
    }

    // Story S28's real-world motivating case, exercised end-to-end through
    // the Agent: a repository with markers for two different stacks
    // derives a Hybrid Technology Inventory and is Assessed, never
    // escalated as an unresolved tie.
    [Fact]
    public void LocalRepositoryRoot_ReadableWithTwoDifferentMarkers_DerivesHybridInventory_AssessedNotEscalated()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "Sample.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root.FullName, "package.json"), "{}");

        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002" },
            KnownSatisfiedStandardIds: Array.Empty<string>());

        var agent = new RepositoryOnboardingAssessmentAgent("hybrid-repo", _root.FullName, evidence);
        var outcome = (AssessmentOutcome)agent.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Assessed, outcome.Status);
        Assert.Equal(new[] { "dotnet", "node" }, outcome.TechnologyProfile!.Technologies.Select(t => t.Name).OrderBy(n => n));
    }

    // AC3: local_repository_root resolves to an unreadable/nonexistent path -
    // the escalation path is the existing "not readable" escalation
    // (AssessmentOutcome.Escalated, the exact message RepositoryAssessmentBehavior
    // already produces), not a new failure mode.
    [Fact]
    public void LocalRepositoryRoot_UnreadablePath_EscalatesWithExistingNotReadableMessage()
    {
        var nonexistentRoot = Path.Combine(_root.FullName, "does-not-exist");
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(
            new RepositoryDescriptor("sample-repo", IsReadable: false, TechnologyDetection: null), evidence).Execute(context: null!)!;

        var agent = new RepositoryOnboardingAssessmentAgent("sample-repo", nonexistentRoot, evidence);
        var actual = (AssessmentOutcome)agent.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, actual.Status);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
        Assert.Equal("Repository 'sample-repo' is not readable - cannot assess without guessing.", actual.EscalationReason);
        Assert.Null(actual.TechnologyProfile);
        Assert.Null(actual.ProjectProfile);
    }

    // Renamed from ...NoRecognizedMarker...EscalatesWithExistingUndeterminedStackMessage:
    // a readable root with no recognized marker is now a confirmed,
    // non-escalating "confirmed-no-technology" outcome - never an
    // escalation, per Story S28 / ADR-0013 (INV-3). This is the one
    // acceptance-behavior *change*, not merely a rename: pre-S28, this
    // exact fixture escalated; post-S28, it reaches ConfirmedNoTechnology.
    [Fact]
    public void LocalRepositoryRoot_ReadableWithNoRecognizedMarker_ReachesConfirmedNoTechnology_NeverEscalatesAsAmbiguous()
    {
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());

        var agent = new RepositoryOnboardingAssessmentAgent("sample-repo", _root.FullName, evidence);
        var outcome = (AssessmentOutcome)agent.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.ConfirmedNoTechnology, outcome.Status);
        Assert.Null(outcome.EscalationReason);
        Assert.Null(outcome.TechnologyProfile);
        Assert.Null(outcome.ProjectProfile);
    }
}
