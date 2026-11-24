using System.Text.Json;
using ModelContextProtocol.Protocol;
using Runtime.Agent;

namespace Adapters.Mcp;

// Epic M2-0001 Story S4: translates an MCP tools/call request into a real
// ExecutionRequest and back, the same translation-only role
// McpToolCatalog plays for tools/list. Two decisions this file encodes,
// per the Pre-Story S4 Architecture/Implementation Review (ai-engineering-
// framework repo, docs/06-history/2026-07-14-aef-m2-epic-m2-0001-pre-s4-
// architecture-implementation-review.md):
//
// 1. UserRequest is a FIXED, per-tool routing literal, verified against
//    SKL-EXE-0021's live keyword table to match exactly the intended
//    capability and no other - never derived from the caller's own
//    arguments. This is an implementation constraint of the existing
//    Runtime dispatch (every Execute call re-routes through that keyword
//    table), not a new direct-dispatch path.
// 2. Scope is exactly the 8 capabilities reachable without a Runtime-
//    typed payload object. aef_deliver_implementation/
//    aef_review_specification/aef_validate_implementation are recognized
//    explicitly (so a caller gets a clear "deferred" message, not a
//    generic "unknown tool" one) but never invoked - no adapter-layer
//    workaround is attempted for them.
public static class McpToolInvoker
{
    private sealed record ToolInvocationSpec(string CapabilityId, string RoutingLiteral);

    // Every routing literal below was checked against the live keyword
    // table (Runtime/DomainContent/ConversationManagement/
    // ConversationIntakeAndRoutingBehavior.cs) to contain no substring
    // mapped, earlier in that table, to a different capability.
    private static readonly IReadOnlyDictionary<string, ToolInvocationSpec> InScopeTools = new Dictionary<string, ToolInvocationSpec>
    {
        ["aef_start_feature_lifecycle"] = new("WFL-DEL-0001", "feature"),
        ["aef_start_epic_lifecycle"] = new("WFL-DEL-0004", "epic"),
        ["aef_plan_engineering"] = new("WFL-DEL-0006", "plan"),
        ["aef_analyze_repository"] = new("WFL-DEL-0008", "analyze"),
        ["aef_validate_repository"] = new("REPO-VALIDATION", "validate"),
        ["aef_plan_migration"] = new("WFL-DEL-0009", "migrate"),
        ["aef_plan_maintenance"] = new("WFL-DEL-0011", "maintenance"),
        ["aef_manage_platform_lifecycle"] = new("WFL-DEL-0012", "platform lifecycle"),
    };

    // Named explicitly - per the Pre-Story S4 Review §2.2 - so a caller of
    // one of these three gets a clear, honest "deferred" message rather
    // than an indistinguishable "unknown tool" one. Never invoked.
    private static readonly IReadOnlyCollection<string> DeferredTools = new[]
    {
        "aef_deliver_implementation",
        "aef_review_specification",
        "aef_validate_implementation",
    };

    public static bool IsDeferred(string toolName) => DeferredTools.Contains(toolName);

    public static ExecutionRequest BuildExecutionRequest(string toolName, IDictionary<string, JsonElement>? arguments)
    {
        if (!InScopeTools.TryGetValue(toolName, out var spec))
        {
            var reason = IsDeferred(toolName)
                ? $"'{toolName}' is a recognized but deferred tool (Pre-Story S4 Review §2.2) - it requires a Runtime-typed payload object no JSON-based channel can construct today."
                : $"'{toolName}' is not a Story S4 in-scope tool.";
            throw new InvalidOperationException(reason);
        }

        var rawPayload = arguments?.ToDictionary(pair => pair.Key, pair => (object)pair.Value);
        var normalized = PayloadNormalization.Normalize(rawPayload);

        var payload = normalized is null ? new Dictionary<string, object>() : new Dictionary<string, object>(normalized);

        string? sessionId = null;
        if (payload.Remove("SessionId", out var sessionValue) && sessionValue is string sessionString)
        {
            sessionId = sessionString;
        }

        var projectProfileId = string.Empty;
        if (payload.Remove("ProjectProfileId", out var profileValue) && profileValue is string profileString)
        {
            projectProfileId = profileString;
        }

        return new ExecutionRequest(Guid.NewGuid().ToString("N"), sessionId, spec.RoutingLiteral, projectProfileId, payload);
    }

    private static string Section(string heading, IReadOnlyList<string>? lines) =>
        lines is null || lines.Count == 0 ? string.Empty : $"\n\n{heading}:\n" + string.Join("\n", lines.Select(line => "- " + line));

    public static CallToolResult BuildCallToolResult(ExecutionResponse response)
    {
        var isError = response.Status == ExecutionStatus.Failed;
        var text = isError
            ? response.Error?.Reason ?? "Execution failed."
            : response.PresentedResult ?? response.SuggestedNextStep ?? "Execution completed.";

        // The handoff text alone ("Completed via ...") tells a calling agent nothing; append the evidence.
        var evidence = response.Evidence;
        if (!string.IsNullOrEmpty(evidence.Document))
        {
            text += "\n\n" + evidence.Document;
        }
        else
        {
            text += Section("Findings", evidence.Diagnostics);
            text += Section("Results", evidence.ValidationResults);
        }

        return new CallToolResult
        {
            IsError = isError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }
}
