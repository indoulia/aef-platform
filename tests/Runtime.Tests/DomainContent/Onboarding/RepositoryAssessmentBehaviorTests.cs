using Runtime.DomainContent.Onboarding;
using Runtime.Integration.RepositoryScanning;
using Xunit;

namespace Runtime.Tests.DomainContent.Onboarding;

// S19 (epics/0006-repository-discovery-and-canonical-onboarding.md), hardened
// by the CU-3 Hardening Sprint (docs/aep/CU-3-Hardening-Sprint-Plan.md,
// Story H1) after Ferret's real onboarding found the original Adopt/Defer
// and Id-generation logic was a hard-coded stub (epics/decisions/
// 0007-ferret-first-production-onboarding-findings.md, F1/F2). Amended by
// Epic 0008 Story S28 (ADR-0013, docs/aep/ARB-Repository-Technology-Domain-Model.md):
// RepositoryDescriptor.TechnologyDetection replaces the scalar PrimaryStack,
// and this Behavior now branches on three outcomes - Detected, Confirmed-
// Empty (a new, non-escalating "confirmed-no-technology" terminal state),
// and Inconclusive (escalates, distinct from a confirmed absence).
public class RepositoryAssessmentBehaviorTests
{
    private static RepositoryDescriptor ReadableRepository(string name = "sample-repo") =>
        new(name, IsReadable: true, TechnologyDetectionResult.Detected(new[] { new TechnologyEvidence("dotnet", "Sample.csproj at repository root") }));

    private static RepositoryDescriptor HybridRepository(string name = "hybrid-repo") =>
        new(name, IsReadable: true, TechnologyDetectionResult.Detected(new[]
        {
            new TechnologyEvidence("dotnet", "Sample.csproj at repository root"),
            new TechnologyEvidence("node", "package.json at repository root"),
        }));

    [Fact]
    public void ReadableRepositoryWithDetectedTechnology_ProducesDraftTechnologyAndProjectProfiles()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" },
            KnownSatisfiedStandardIds: new[] { "STD-GOV-0002" });
        var behavior = new RepositoryAssessmentBehavior(ReadableRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Assessed, outcome.Status);
        Assert.NotNull(outcome.TechnologyProfile);
        Assert.Equal("Draft", outcome.TechnologyProfile!.Status);
        Assert.Equal(new[] { "dotnet" }, outcome.TechnologyProfile.Technologies.Select(t => t.Name));
        Assert.NotNull(outcome.ProjectProfile);
        Assert.Equal("Draft", outcome.ProjectProfile!.Status);
        Assert.Null(outcome.EscalationReason);
    }

    // Story S28's own central case: a Hybrid repository (more than one
    // evidenced technology) is assessed exactly like a single-technology
    // repository - one Technology Profile, carrying every entry, never
    // forced down to one and never escalated.
    [Fact]
    public void ReadableRepositoryWithHybridTechnologyInventory_ProducesOneTechnologyProfileWithBothEntries()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002" },
            KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(HybridRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Assessed, outcome.Status);
        Assert.Equal(new[] { "dotnet", "node" }, outcome.TechnologyProfile!.Technologies.Select(t => t.Name).OrderBy(n => n));
        Assert.NotNull(outcome.ProjectProfile);
    }

    [Fact]
    public void KnownSatisfiedStandards_AreAdopted_RemainderAreDeferred_EvidenceDerivedNotHardCoded()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" },
            KnownSatisfiedStandardIds: new[] { "STD-GOV-0002" });
        var behavior = new RepositoryAssessmentBehavior(ReadableRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(new[] { "STD-GOV-0002" }, outcome.ProjectProfile!.Adopted);
        Assert.Equal(new[] { "STD-GOV-0003", "STD-QUA-0001" }, outcome.ProjectProfile.Deferred);
    }

    [Fact]
    public void NoKnownSatisfiedEvidence_AllCandidatesAreDeferred_NeverFabricatesAnAdoption()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" },
            KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(ReadableRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Empty(outcome.ProjectProfile!.Adopted);
        Assert.Equal(new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" }, outcome.ProjectProfile.Deferred);
    }

    [Fact]
    public void NoCandidateStandardsSupplied_Escalates_NeverProducesAnEmptyAdoptionList()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: Array.Empty<string>(),
            KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(ReadableRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
        Assert.Null(outcome.ProjectProfile);
        Assert.Null(outcome.TechnologyProfile);
    }

    [Fact]
    public void FirstAssessment_NoExistingArtifactCount_EmitsSequentialIdOne()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002" },
            KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(ReadableRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal("TPR-PRJ-0001", outcome.TechnologyProfile!.Id);
        Assert.Equal("PPR-PRJ-0001", outcome.ProjectProfile!.Id);
    }

    [Fact]
    public void SubsequentAssessment_ExistingArtifactCount_EmitsNextSequentialId_NeverNameSuffixed()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002" },
            KnownSatisfiedStandardIds: Array.Empty<string>(),
            ExistingProjectArtifactCount: 5);
        var behavior = new RepositoryAssessmentBehavior(ReadableRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal("TPR-PRJ-0006", outcome.TechnologyProfile!.Id);
        Assert.Equal("PPR-PRJ-0006", outcome.ProjectProfile!.Id);
    }

    [Fact]
    public void UnreadableRepository_Escalates_NeverGuesses()
    {
        var descriptor = new RepositoryDescriptor("locked-repo", IsReadable: false, TechnologyDetection: null);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(descriptor, evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
        Assert.Null(outcome.TechnologyProfile);
        Assert.Null(outcome.ProjectProfile);
    }

    // Renamed from ...WithUndeterminedStack...: this is now specifically
    // the Inconclusive case (the scan itself could not complete) - escalates,
    // exactly as an undetermined stack once did, but for a now-precisely-
    // named reason (INV-3).
    [Fact]
    public void ReadableRepositoryWithInconclusiveScan_Escalates_NeverGuesses()
    {
        var descriptor = new RepositoryDescriptor("mystery-repo", IsReadable: true, TechnologyDetectionResult.Inconclusive);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(descriptor, evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
    }

    // Story S28's other central case: a repository whose scan completed
    // and found zero technologies is a confirmed, confident answer - it
    // is never escalated as if ambiguous, and no Draft profile of either
    // kind is produced (docs/aep/ARB-Repository-Technology-Domain-Model.md
    // §6, Known Limitation #1).
    [Fact]
    public void ReadableRepositoryWithConfirmedEmptyTechnologyInventory_ReachesConfirmedNoTechnology_NeverEscalatesAsAmbiguous()
    {
        var descriptor = new RepositoryDescriptor("docs-only-repo", IsReadable: true, TechnologyDetectionResult.ConfirmedEmpty);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(descriptor, evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.ConfirmedNoTechnology, outcome.Status);
        Assert.Null(outcome.EscalationReason);
        Assert.Null(outcome.TechnologyProfile);
        Assert.Null(outcome.ProjectProfile);
    }

    // The confirmed-no-technology outcome is reached regardless of whether
    // candidate Standards were supplied - no Standards-adoption decision
    // is made in this case at all (PCT-EXE-0015 v2.0.0 Postconditions).
    [Fact]
    public void ConfirmedEmptyTechnologyInventory_ReachesConfirmedNoTechnology_EvenWithNoCandidateStandards()
    {
        var descriptor = new RepositoryDescriptor("docs-only-repo", IsReadable: true, TechnologyDetectionResult.ConfirmedEmpty);
        var evidence = new AdoptionEvidence(CandidateStandardIds: Array.Empty<string>(), KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(descriptor, evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.ConfirmedNoTechnology, outcome.Status);
    }
}
