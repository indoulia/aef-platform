using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Context;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Xunit;

namespace Runtime.Tests.DomainContent.EngineeringSpecificationReview;

public class SpecificationIndependentReviewBehaviorTests
{
    private sealed class StubProvider : IAiProvider
    {
        private readonly string _rawText;
        public StubProvider(string rawText) => _rawText = rawText;
        public string Name => "Stub";
        public Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletionResult(_rawText, new AiUsage(1, 1, 2), "Stub", "test-model"));
    }

    private static RuntimeExecutionContext ContextWith(object request) =>
        new("test-instance", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Review",
            new Dictionary<string, object> { ["SpecificationReviewRequest"] = request });

    [Fact]
    public void ReturnsParsedReviewOnSuccess()
    {
        var raw = """
        {"review_instance":{"review_target":"SPEC-1","review_iteration":1,"findings":[{"area":"Business Goal","severity":"Minor","reason":"vague metric","evidence":"line 4","recommendation":"quantify"}]},"final_recommendation":"Approved with Comments","status":"review_complete","failure_reason":null}
        """;
        var service = new AiExecutionService(new StubProvider(raw), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationIndependentReviewBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith(new SpecificationReviewRequest("SPEC-1 content", 1))) as SpecificationReviewOutcome;

        Assert.NotNull(result);
        Assert.False(result!.IsEscalated);
        Assert.Equal("Approved with Comments", result.FinalRecommendation);
        Assert.Single(result.ReviewInstance!.Findings);
    }

    [Fact]
    public void EscalatesOnMalformedAiResponse()
    {
        var service = new AiExecutionService(new StubProvider("not json"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationIndependentReviewBehavior(service, new EngineeringPromptCatalog());

        var result = behavior.Execute(ContextWith(new SpecificationReviewRequest("SPEC-1 content", 1))) as SpecificationReviewOutcome;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
        Assert.NotNull(result.EscalationReason);
    }

    [Fact]
    public void EscalatesWhenNoRequestIsSupplied()
    {
        var service = new AiExecutionService(new StubProvider("{}"), timeout: TimeSpan.FromSeconds(5), delay: _ => { });
        var behavior = new SpecificationIndependentReviewBehavior(service, new EngineeringPromptCatalog());
        var emptyContext = new RuntimeExecutionContext("x", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Review", new Dictionary<string, object>());

        var result = behavior.Execute(emptyContext) as SpecificationReviewOutcome;

        Assert.NotNull(result);
        Assert.True(result!.IsEscalated);
    }
}
