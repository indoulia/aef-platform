using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;

namespace Runtime.Tests.DomainContent.ConversationManagement;

public class ConversationIntakeAndRoutingBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(RoutingRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["RoutingRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Intake", content);
    }

    [Theory]
    [InlineData("Implement the new feature for X", "WFL-DEL-0001")]
    [InlineData("Start the epic for repository onboarding", "WFL-DEL-0004")]
    [InlineData("Deliver the fix for the bug", "SKL-EXE-0018")]
    [InlineData("Please analyze the repository", "WFL-DEL-0008")]
    [InlineData("Plan the specification for a new capability", "WFL-DEL-0006")]
    [InlineData("Validate the build", "REPO-VALIDATION")]
    public void Execute_KnownKeyword_RoutesToTheMatchingCapability(string userRequest, string expectedCapabilityId)
    {
        var behavior = new ConversationIntakeAndRoutingBehavior();
        var context = ContextWith(new RoutingRequest(userRequest, ConversationContext.New("conv-1")));

        var result = (RoutingResult)behavior.Execute(context)!;

        Assert.Equal(RoutingOutcome.Routed, result.Outcome);
        Assert.Equal(expectedCapabilityId, result.TargetCapabilityId);
    }

    [Fact]
    public void Execute_UnknownRequest_NeedsClarification()
    {
        var behavior = new ConversationIntakeAndRoutingBehavior();
        var context = ContextWith(new RoutingRequest("What time is it?", ConversationContext.New("conv-1")));

        var result = (RoutingResult)behavior.Execute(context)!;

        Assert.Equal(RoutingOutcome.NeedsClarification, result.Outcome);
        Assert.NotEmpty(result.ClarificationQuestions!);
    }

    [Fact]
    public void Execute_NoRoutingRequestSupplied_NeedsClarification()
    {
        var behavior = new ConversationIntakeAndRoutingBehavior();
        var context = ContextWith(null);

        var result = (RoutingResult)behavior.Execute(context)!;

        Assert.Equal(RoutingOutcome.NeedsClarification, result.Outcome);
    }
}
