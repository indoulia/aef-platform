using Runtime.DomainContent.Onboarding;
using Xunit;

namespace Runtime.Tests.DomainContent.Onboarding;

// Epic 0008 (epics/0008-local-repository-scanner-implementation.md), Story
// S24: AGT-EXE-0007's composition realization wiring the S22/S23
// Integration Adapter Boundary into the already-governed onboarding path,
// per PCT-EXE-0015 v1.2.0's amended Input contract - without re-touching
// RepositoryAssessmentBehavior.cs at all.
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
        new(name, IsReadable: true, PrimaryStack: "dotnet");

    // Mirrors RepositoryAssessmentBehaviorTests.ReadableRepositoryWithDeterminedStack_ProducesDraftTechnologyAndProjectProfiles
    [Fact]
    public void DirectDescriptorConvention_ReadableRepositoryWithDeterminedStack_ProducesDraftTechnologyAndProjectProfiles_IdenticalToBehaviorDirectly()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" },
            KnownSatisfiedStandardIds: new[] { "STD-GOV-0002" });

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(ReadableRepository(), evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(ReadableRepository(), evidence).Execute(context: null!)!;

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.TechnologyProfile!.Id, actual.TechnologyProfile!.Id);
        Assert.Equal(expected.TechnologyProfile.Stack, actual.TechnologyProfile.Stack);
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
        var descriptor = new RepositoryDescriptor("locked-repo", IsReadable: false, PrimaryStack: null);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(descriptor, evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(descriptor, evidence).Execute(context: null!)!;

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
        Assert.Null(actual.TechnologyProfile);
        Assert.Null(actual.ProjectProfile);
    }

    // Mirrors RepositoryAssessmentBehaviorTests.ReadableRepositoryWithUndeterminedStack_Escalates_NeverGuesses
    [Fact]
    public void DirectDescriptorConvention_ReadableRepositoryWithUndeterminedStack_Escalates_IdenticalToBehaviorDirectly()
    {
        var descriptor = new RepositoryDescriptor("mystery-repo", IsReadable: true, PrimaryStack: null);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());

        var expected = (AssessmentOutcome)new RepositoryAssessmentBehavior(descriptor, evidence).Execute(context: null!)!;
        var actual = (AssessmentOutcome)new RepositoryOnboardingAssessmentAgent(descriptor, evidence).Execute(context: null!)!;

        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
    }

    // AC1: local_repository_root supplied, is_readable/primary_stack are not -
    // derives a real RepositoryDescriptor via the S22/S23 adapter (a real,
    // temporary on-disk fixture directory containing a .csproj marker) and
    // calls RepositoryAssessmentBehavior unmodified.
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
        Assert.Equal("dotnet", outcome.TechnologyProfile!.Stack);
        Assert.NotNull(outcome.ProjectProfile);
        Assert.Equal(new[] { "STD-GOV-0002" }, outcome.ProjectProfile!.Adopted);
        Assert.Equal(new[] { "STD-GOV-0003" }, outcome.ProjectProfile.Deferred);
        Assert.Null(outcome.EscalationReason);
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
            new RepositoryDescriptor("sample-repo", IsReadable: false, PrimaryStack: null), evidence).Execute(context: null!)!;

        var agent = new RepositoryOnboardingAssessmentAgent("sample-repo", nonexistentRoot, evidence);
        var actual = (AssessmentOutcome)agent.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, actual.Status);
        Assert.Equal(expected.EscalationReason, actual.EscalationReason);
        Assert.Equal("Repository 'sample-repo' is not readable - cannot assess without guessing.", actual.EscalationReason);
        Assert.Null(actual.TechnologyProfile);
        Assert.Null(actual.ProjectProfile);
    }

    // AC1's "skip stack-detection when root is unreadable" judgment call:
    // a readable-but-empty root (no recognized marker) still escalates via
    // the existing "undetermined stack" message - never a new failure mode.
    [Fact]
    public void LocalRepositoryRoot_ReadableWithNoRecognizedMarker_EscalatesWithExistingUndeterminedStackMessage()
    {
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());

        var agent = new RepositoryOnboardingAssessmentAgent("sample-repo", _root.FullName, evidence);
        var outcome = (AssessmentOutcome)agent.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, outcome.Status);
        Assert.Equal("Repository 'sample-repo' has no determinable primary technology stack - cannot select a Technology Profile without guessing.", outcome.EscalationReason);
    }
}
