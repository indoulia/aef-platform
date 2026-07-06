using Runtime.DomainContent.Onboarding;
using Xunit;

namespace Runtime.Tests.DomainContent.Onboarding;

// S19 (epics/0006-repository-discovery-and-canonical-onboarding.md), hardened
// by the CU-3 Hardening Sprint (docs/aep/CU-3-Hardening-Sprint-Plan.md,
// Story H1) after Ferret's real onboarding found the original Adopt/Defer
// and Id-generation logic was a hard-coded stub (epics/decisions/
// 0007-ferret-first-production-onboarding-findings.md, F1/F2). Covers both
// original S19 acceptance criteria plus H1's evidence-derived reasoning and
// sequential-Id acceptance criteria.
public class RepositoryAssessmentBehaviorTests
{
    private static RepositoryDescriptor ReadableRepository(string name = "sample-repo") =>
        new(name, IsReadable: true, PrimaryStack: "dotnet");

    [Fact]
    public void ReadableRepositoryWithDeterminedStack_ProducesDraftTechnologyAndProjectProfiles()
    {
        var evidence = new AdoptionEvidence(
            CandidateStandardIds: new[] { "STD-GOV-0002", "STD-GOV-0003", "STD-QUA-0001" },
            KnownSatisfiedStandardIds: new[] { "STD-GOV-0002" });
        var behavior = new RepositoryAssessmentBehavior(ReadableRepository(), evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Assessed, outcome.Status);
        Assert.NotNull(outcome.TechnologyProfile);
        Assert.Equal("Draft", outcome.TechnologyProfile!.Status);
        Assert.Equal("dotnet", outcome.TechnologyProfile.Stack);
        Assert.NotNull(outcome.ProjectProfile);
        Assert.Equal("Draft", outcome.ProjectProfile!.Status);
        Assert.Null(outcome.EscalationReason);
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
        var descriptor = new RepositoryDescriptor("locked-repo", IsReadable: false, PrimaryStack: null);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(descriptor, evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
        Assert.Null(outcome.TechnologyProfile);
        Assert.Null(outcome.ProjectProfile);
    }

    [Fact]
    public void ReadableRepositoryWithUndeterminedStack_Escalates_NeverGuesses()
    {
        var descriptor = new RepositoryDescriptor("mystery-repo", IsReadable: true, PrimaryStack: null);
        var evidence = new AdoptionEvidence(CandidateStandardIds: new[] { "STD-GOV-0002" }, KnownSatisfiedStandardIds: Array.Empty<string>());
        var behavior = new RepositoryAssessmentBehavior(descriptor, evidence);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
    }
}
