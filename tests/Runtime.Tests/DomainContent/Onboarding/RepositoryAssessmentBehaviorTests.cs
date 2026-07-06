using Runtime.DomainContent.Onboarding;
using Xunit;

namespace Runtime.Tests.DomainContent.Onboarding;

// S19 (epics/0006-repository-discovery-and-canonical-onboarding.md) - the
// Repository Assessment and Profile Selection Skill's Behavior. Covers both
// S19 acceptance criteria: a readable, determined-stack repository produces
// Draft TPR+PPR without registering anything; an ambiguous/unreadable
// target escalates rather than guessing.
public class RepositoryAssessmentBehaviorTests
{
    [Fact]
    public void ReadableRepositoryWithDeterminedStack_ProducesDraftTechnologyAndProjectProfiles()
    {
        var descriptor = new RepositoryDescriptor("sample-repo", IsReadable: true, PrimaryStack: "dotnet");
        var behavior = new RepositoryAssessmentBehavior(descriptor);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Assessed, outcome.Status);
        Assert.NotNull(outcome.TechnologyProfile);
        Assert.Equal("Draft", outcome.TechnologyProfile!.Status);
        Assert.Equal("dotnet", outcome.TechnologyProfile.Stack);
        Assert.NotNull(outcome.ProjectProfile);
        Assert.Equal("Draft", outcome.ProjectProfile!.Status);
        Assert.NotEmpty(outcome.ProjectProfile.Adopted);
        Assert.NotEmpty(outcome.ProjectProfile.Deferred);
        Assert.Null(outcome.EscalationReason);
    }

    [Fact]
    public void UnreadableRepository_Escalates_NeverGuesses()
    {
        var descriptor = new RepositoryDescriptor("locked-repo", IsReadable: false, PrimaryStack: null);
        var behavior = new RepositoryAssessmentBehavior(descriptor);

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
        var behavior = new RepositoryAssessmentBehavior(descriptor);

        var outcome = (AssessmentOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(AssessmentStatus.Escalated, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
    }
}
