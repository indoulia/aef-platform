using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;

namespace Runtime.Tests.DomainContent.ConversationManagement;

public class ConversationHandoffAndFollowupBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(HandoffRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["HandoffRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Handoff", content);
    }

    [Fact]
    public void Execute_NoHandoffRequestSupplied_ReturnsFallbackMessage()
    {
        var behavior = new ConversationHandoffAndFollowupBehavior();

        var result = (HandoffResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("No handoff payload was supplied.", result.PresentedResult);
        Assert.Null(result.SuggestedNextStep);
    }

    [Fact]
    public void Execute_NeedsClarificationRouting_PresentsTheClarificationQuestions()
    {
        var behavior = new ConversationHandoffAndFollowupBehavior();
        var routing = RoutingResult.NeedsClarification(new[] { "Which capability?" });
        var context = ContextWith(new HandoffRequest(routing, null));

        var result = (HandoffResult)behavior.Execute(context)!;

        Assert.Contains("Which capability?", result.PresentedResult);
        Assert.NotNull(result.SuggestedNextStep);
    }

    [Fact]
    public void Execute_RoutedWithNoDispatchOutcome_ReturnsFallbackMessage()
    {
        var behavior = new ConversationHandoffAndFollowupBehavior();
        var routing = RoutingResult.Routed("WFL-DEL-0001");
        var context = ContextWith(new HandoffRequest(routing, null));

        var result = (HandoffResult)behavior.Execute(context)!;

        Assert.Equal("Routing succeeded but no dispatch outcome was supplied.", result.PresentedResult);
    }

    [Fact]
    public void Execute_SuccessfulDispatch_PresentsCompletion()
    {
        var behavior = new ConversationHandoffAndFollowupBehavior();
        var routing = RoutingResult.Routed("WFL-DEL-0001");
        var outcome = new DispatchOutcomeSummary(true, "WFL-DEL-0001", Array.Empty<string>());
        var context = ContextWith(new HandoffRequest(routing, outcome));

        var result = (HandoffResult)behavior.Execute(context)!;

        Assert.Contains("Completed via WFL-DEL-0001", result.PresentedResult);
        Assert.Equal("No further action needed.", result.SuggestedNextStep);
    }

    [Fact]
    public void Execute_FailedDispatch_PresentsFailureDiagnostics()
    {
        var behavior = new ConversationHandoffAndFollowupBehavior();
        var routing = RoutingResult.Routed("WFL-DEL-0001");
        var outcome = new DispatchOutcomeSummary(false, "WFL-DEL-0001", new[] { "Agent not registered." });
        var context = ContextWith(new HandoffRequest(routing, outcome));

        var result = (HandoffResult)behavior.Execute(context)!;

        Assert.Contains("Agent not registered.", result.PresentedResult);
        Assert.Equal("Review the diagnostics and retry.", result.SuggestedNextStep);
    }
}
