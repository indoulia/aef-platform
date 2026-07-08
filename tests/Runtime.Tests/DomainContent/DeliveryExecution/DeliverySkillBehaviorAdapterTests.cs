using Runtime.Context;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.Tests.DomainContent.DeliveryExecution;

public class DeliverySkillBehaviorAdapterTests
{
    private static RuntimeExecutionContext ContextWith(string key, object? value)
    {
        var content = value is null ? new Dictionary<string, object>() : new Dictionary<string, object> { [key] = value };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Implementation", content);
    }

    [Fact]
    public void ImplementationAdapter_NoRequestSupplied_Escalates()
    {
        var behavior = new DeliveryImplementationSkillBehavior();

        var outcome = (ImplementationOutcome)behavior.Execute(ContextWith("Other", "x"))!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void ImplementationAdapter_NoRepositoryRootSupplied_Escalates()
    {
        var behavior = new DeliveryImplementationSkillBehavior();
        var request = new ImplementationRequest("story-1", "feature/x", new[] { "abc123" }, "plan");
        var context = ContextWith("ImplementationRequest", request);

        var outcome = (ImplementationOutcome)behavior.Execute(context)!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void ImplementationAdapter_ValidRequest_DelegatesToDeliveryImplementationBehavior()
    {
        var behavior = new DeliveryImplementationSkillBehavior(_ => new AlwaysVerifiedVerifier());
        var request = new ImplementationRequest("story-1", "feature/x", new[] { "abc123" }, "plan");
        var content = new Dictionary<string, object>
        {
            ["ImplementationRequest"] = request,
            ["RepositoryRoot"] = "C:\\fake-repo",
        };
        var context = new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Implementation", content);

        var outcome = (ImplementationOutcome)behavior.Execute(context)!;

        Assert.False(outcome.IsEscalated);
        Assert.Equal("story-1", outcome.LogEntry!.StoryReferenceId);
    }

    [Fact]
    public void ValidationAdapter_NoRequestSupplied_Escalates()
    {
        var behavior = new DeliveryValidationSkillBehavior();

        var outcome = (ValidationOutcome)behavior.Execute(ContextWith("Other", "x"))!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void DocumentationAdapter_NoRequestSupplied_Escalates()
    {
        var behavior = new DeliveryDocumentationSkillBehavior();

        var outcome = (DocumentationOutcome)behavior.Execute(ContextWith("Other", "x"))!;

        Assert.True(outcome.IsEscalated);
    }

    [Fact]
    public void DocumentationAdapter_ValidRequest_DelegatesToDeliveryDocumentationBehavior()
    {
        var behavior = new DeliveryDocumentationSkillBehavior();
        var request = new DocumentationRequest("story-1", "end user content", "maintainer content", false);

        var outcome = (DocumentationOutcome)behavior.Execute(ContextWith("DocumentationRequest", request))!;

        Assert.False(outcome.IsEscalated);
        Assert.Equal("story-1", outcome.LogEntry!.StoryReferenceId);
    }

    private sealed class AlwaysVerifiedVerifier : IIsolatedChangeVerifier
    {
        public bool BranchExists(string branchName) => true;
        public bool IsDefaultBranch(string branchName) => false;
        public bool CommitExistsOnBranch(string branchName, string commitSha) => true;
    }
}
