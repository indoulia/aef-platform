using AefPlatform.Host;
using Microsoft.Extensions.Logging.Abstractions;
using Runtime.Agent.Security;

namespace Host.Tests;

public class TrustedDefaultSecurityEvaluatorTests
{
    private static TrustedDefaultSecurityEvaluator CreateEvaluator() =>
        new(NullLogger<TrustedDefaultSecurityEvaluator>.Instance);

    [Fact]
    public void Evaluate_EmptyContext_Allows()
    {
        var evaluator = CreateEvaluator();

        var decision = evaluator.Evaluate(new SecurityContext(null, null));

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.DenialReason);
    }

    [Fact]
    public void Evaluate_ContextWithClaims_Allows()
    {
        var evaluator = CreateEvaluator();
        var context = new SecurityContext("user-1", new Dictionary<string, string> { ["role"] = "engineer" });

        var decision = evaluator.Evaluate(context);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Evaluate_NullClaims_StillAllows()
    {
        var evaluator = CreateEvaluator();
        var context = new SecurityContext("user-1", null);

        var decision = evaluator.Evaluate(context);

        Assert.True(decision.IsAllowed);
    }
}
