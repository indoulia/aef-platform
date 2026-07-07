using Runtime.DomainContent.Onboarding;
using Runtime.Integration.RepositoryScanning;
using Xunit;

namespace Runtime.Tests.DomainContent.Onboarding;

// S20 (epics/0006-repository-discovery-and-canonical-onboarding.md) - the
// Project Registration and Workspace Initialization Skill's Behavior.
// Covers both S20 acceptance criteria: a non-colliding Draft PPR/TPR pair
// registers with an initialized workspace and pinned sync; a colliding Id
// escalates as a Terminal Failure rather than silently overwriting.
// TechnologyProfileDraft's shape amended per ADR-0013 / Epic 0008 Story
// S28 - this Story's own logic is otherwise untouched, confirmed by
// inspection (no production code here reads .Technologies).
public class ProjectRegistrationBehaviorTests
{
    private static ProjectProfileDraft SampleProjectProfile(string id = "PPR-PRJ-sample-repo") =>
        new(id, "sample-repo", Adopted: new[] { "STD-GOV-0001" }, Deferred: new[] { "STD-QUA-0001" });

    private static TechnologyProfileDraft SampleTechnologyProfile(string id = "TPR-PRJ-sample-repo") =>
        new(id, new[] { new TechnologyEvidence("dotnet", "Sample.csproj at repository root") });

    [Fact]
    public void NonCollidingProfiles_Registers_WithWorkspaceAndPinnedSync()
    {
        var request = new ProjectRegistrationRequest(
            SampleProjectProfile(),
            SampleTechnologyProfile(),
            ExistingRegisteredIds: Array.Empty<string>(),
            PinnedFrameworkVersion: "2.3.0");
        var behavior = new ProjectRegistrationBehavior(request);

        var outcome = (RegistrationOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(RegistrationStatus.Registered, outcome.Status);
        Assert.Equal("PPR-PRJ-sample-repo", outcome.ProjectProfile!.Id);
        Assert.Equal("TPR-PRJ-sample-repo", outcome.TechnologyProfile!.Id);
        Assert.Equal(6, outcome.WorkspaceCategories!.Count);
        Assert.Contains("discovery", outcome.WorkspaceCategories);
        Assert.Contains("investigations", outcome.WorkspaceCategories);
        Assert.Contains("reviews", outcome.WorkspaceCategories);
        Assert.Contains("reports", outcome.WorkspaceCategories);
        Assert.Contains("sessions", outcome.WorkspaceCategories);
        Assert.Contains("generated", outcome.WorkspaceCategories);
        Assert.Equal("2.3.0", outcome.FrameworkSync!.PinnedVersion);
        Assert.Null(outcome.EscalationReason);
    }

    [Fact]
    public void CollidingId_EscalatesAsTerminalFailure_NeverOverwrites()
    {
        var request = new ProjectRegistrationRequest(
            SampleProjectProfile(),
            SampleTechnologyProfile(),
            ExistingRegisteredIds: new[] { "PPR-PRJ-sample-repo" },
            PinnedFrameworkVersion: "2.3.0");
        var behavior = new ProjectRegistrationBehavior(request);

        var outcome = (RegistrationOutcome)behavior.Execute(context: null!)!;

        Assert.Equal(RegistrationStatus.TerminalFailure, outcome.Status);
        Assert.NotNull(outcome.EscalationReason);
        Assert.Null(outcome.ProjectProfile);
        Assert.Null(outcome.TechnologyProfile);
        Assert.Null(outcome.WorkspaceCategories);
        Assert.Null(outcome.FrameworkSync);
    }
}
