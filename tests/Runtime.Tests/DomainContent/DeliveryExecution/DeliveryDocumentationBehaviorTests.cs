using Runtime.DomainContent.DeliveryExecution;
using Xunit;

namespace Runtime.Tests.DomainContent.DeliveryExecution;

// Epic 0005 (ai-engineering-framework repo), Story S32: pure content-
// packaging logic - no repository or process interaction applies to this
// Skill, so these are ordinary unit tests, not real-fixture integration
// tests (the Epic's own Validation Strategy real-fixture requirement
// applies to Stories that touch a repository or run a verification
// command; this one does neither).
public sealed class DeliveryDocumentationBehaviorTests
{
    [Fact]
    public void BothAudiencesSupplied_RecordsBothKeptStructurallySeparate()
    {
        var request = new DocumentationRequest("S32", "New feature: does X.", "Internals: implemented via Y.", ProjectRequiresHumanReadGate: false);
        var behavior = new DeliveryDocumentationBehavior(request);

        var outcome = Assert.IsType<DocumentationOutcome>(behavior.Execute(context: null!));

        Assert.False(outcome.IsEscalated);
        Assert.NotNull(outcome.LogEntry);
        Assert.Equal("New feature: does X.", outcome.LogEntry!.EndUserContent);
        Assert.Equal("Internals: implemented via Y.", outcome.LogEntry.MaintainerContent);
        Assert.NotEqual(outcome.LogEntry.EndUserContent, outcome.LogEntry.MaintainerContent);
    }

    [Fact]
    public void OnlyMaintainerContentSupplied_StillRecords_NeverForcesFabricatedEndUserText()
    {
        var request = new DocumentationRequest("S32", string.Empty, "Internal refactor only, no user-facing effect.", ProjectRequiresHumanReadGate: false);
        var behavior = new DeliveryDocumentationBehavior(request);

        var outcome = Assert.IsType<DocumentationOutcome>(behavior.Execute(context: null!));

        Assert.False(outcome.IsEscalated);
        Assert.Equal(string.Empty, outcome.LogEntry!.EndUserContent);
        Assert.Equal("Internal refactor only, no user-facing effect.", outcome.LogEntry.MaintainerContent);
    }

    [Fact]
    public void NoContentForEitherAudience_Escalates_NeverFabricatesDocumentation()
    {
        var request = new DocumentationRequest("S32", string.Empty, "   ", ProjectRequiresHumanReadGate: false);
        var behavior = new DeliveryDocumentationBehavior(request);

        var outcome = Assert.IsType<DocumentationOutcome>(behavior.Execute(context: null!));

        Assert.True(outcome.IsEscalated);
        Assert.Null(outcome.LogEntry);
        Assert.Contains("cannot fabricate documentation", outcome.EscalationReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProjectReadGateFlag_IsCarriedThroughUnchanged_NeverOverriddenBySkillItself(bool requiresGate)
    {
        var request = new DocumentationRequest("S32", "user text", "maintainer text", requiresGate);
        var behavior = new DeliveryDocumentationBehavior(request);

        var outcome = Assert.IsType<DocumentationOutcome>(behavior.Execute(context: null!));

        Assert.Equal(requiresGate, outcome.LogEntry!.RequiresHumanRead);
    }
}
