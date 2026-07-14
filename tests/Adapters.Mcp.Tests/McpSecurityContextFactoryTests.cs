using ModelContextProtocol.Protocol;
using Runtime.Agent.Security;

namespace Adapters.Mcp.Tests;

// Epic M2-0001 Story S5: unit-level coverage of the pure translation logic
// (SecurityContext construction, SecurityDecision -> CallToolResult),
// independent of any subprocess-level integration test - the same
// test-pyramid split McpToolCatalogTests/McpToolInvokerTests already use.
// The denial path is exercised with a test-double ISecurityEvaluator, per
// the Pre-Story S5 Review: the production TrustedDefaultSecurityEvaluator
// never denies, so only a test-double can prove this path is real, wired
// code rather than a dead branch.
public class McpSecurityContextFactoryTests
{
    private sealed class AlwaysAllowEvaluator : ISecurityEvaluator
    {
        public SecurityDecision Evaluate(SecurityContext context) => new(IsAllowed: true, DenialReason: null);
    }

    private sealed class AlwaysDenyEvaluator : ISecurityEvaluator
    {
        public string? LastDenialReason { get; set; } = "Access denied for test.";

        public SecurityDecision Evaluate(SecurityContext context) => new(IsAllowed: false, DenialReason: LastDenialReason);
    }

    [Fact]
    public void CreateForCurrentProcess_UsesTheCurrentOsUser_AsPrincipalId()
    {
        var context = McpSecurityContextFactory.CreateForCurrentProcess();

        Assert.Equal(Environment.UserName, context.PrincipalId);
        Assert.Null(context.Claims);
    }

    [Fact]
    public void EvaluateOrDeny_ReturnsNull_WhenEvaluatorAllows()
    {
        var result = McpSecurityContextFactory.EvaluateOrDeny(new AlwaysAllowEvaluator(), McpSecurityContextFactory.CreateForCurrentProcess());

        Assert.Null(result);
    }

    [Fact]
    public void EvaluateOrDeny_ReturnsIsErrorResult_WhenEvaluatorDenies()
    {
        var evaluator = new AlwaysDenyEvaluator { LastDenialReason = "You shall not pass." };

        var result = McpSecurityContextFactory.EvaluateOrDeny(evaluator, McpSecurityContextFactory.CreateForCurrentProcess());

        Assert.NotNull(result);
        Assert.True(result!.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("You shall not pass.", text.Text);
    }

    [Fact]
    public void EvaluateOrDeny_UsesFallbackMessage_WhenDenialReasonIsNull()
    {
        var evaluator = new AlwaysDenyEvaluator { LastDenialReason = null };

        var result = McpSecurityContextFactory.EvaluateOrDeny(evaluator, McpSecurityContextFactory.CreateForCurrentProcess());

        Assert.NotNull(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result!.Content));
        Assert.Equal("Access denied.", text.Text);
    }
}
