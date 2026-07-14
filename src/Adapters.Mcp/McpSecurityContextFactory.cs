using ModelContextProtocol.Protocol;
using Runtime.Agent.Security;

namespace Adapters.Mcp;

// Epic M2-0001 Story S5: security context mapping for the stdio channel,
// per the Pre-Story S5 Architecture/Implementation Review (ai-engineering-
// framework repo, docs/06-history/2026-07-14-aef-m2-epic-m2-0001-pre-s5-
// architecture-implementation-review.md).
//
// stdio provides no transport-level credential by MCP specification
// design (Implementations using stdio "SHOULD NOT" follow the OAuth 2.1
// authorization flow, and should retrieve credentials from the
// environment instead) - not an implementation gap. Environment.UserName
// mirrors the exact precedent src/Cli/Program.cs's own RunExecute already
// established for this codebase's other local-process channel.
public static class McpSecurityContextFactory
{
    public static SecurityContext CreateForCurrentProcess() => new(Environment.UserName, Claims: null);

    // Returns null when the call may proceed; returns the CallToolResult
    // to send back (IsError: true) when it may not - the same IsError
    // convention McpToolInvoker.BuildCallToolResult already established in
    // Story S4, never a JSON-RPC protocol error.
    public static CallToolResult? EvaluateOrDeny(ISecurityEvaluator evaluator, SecurityContext context)
    {
        var decision = evaluator.Evaluate(context);
        if (decision.IsAllowed)
        {
            return null;
        }

        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock> { new TextContentBlock { Text = decision.DenialReason ?? "Access denied." } },
        };
    }
}
