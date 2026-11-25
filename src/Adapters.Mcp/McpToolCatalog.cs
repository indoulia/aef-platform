using System.Text.Json;
using ModelContextProtocol.Protocol;
using Runtime.Agent.Capabilities;

namespace Adapters.Mcp;

// Epic M2-0001 Story S3: translates ICapabilityCatalog's real,
// runtime-discovered CapabilityDescriptor records into MCP Tool listings -
// the same translation-only role RestExecutionRequest/RestHealthResponse
// play for Adapters.Rest. No id, name, or payload key is hand-invented:
// every Tool's Name comes from this file's own approved mapping
// (transcribed from the Epic's own MCP Tool Catalog,
// ai-engineering-framework repo, docs/06-history/2026-07-14-aef-m2-
// epic-m2-0001-mcp-adapter-capability-exposure-plan.md §5); Description
// and required input keys come directly from CapabilityDescriptor itself.
//
// Deliberately builds only a ListToolsResult - no CallToolHandler wiring
// here. Story S4 owns tool invocation, per the Pre-Story S3 Architecture/
// Implementation Review's own recommended S3/S4 split.
public static class McpToolCatalog
{
    // Transcribed from the Epic's own approved MCP Tool Catalog - the
    // friendly name is a presentation decision that document already
    // made, not something CapabilityDescriptor itself carries.
    private static readonly IReadOnlyDictionary<string, string> CapabilityIdToToolName = new Dictionary<string, string>
    {
        ["WFL-DEL-0001"] = "aef_start_feature_lifecycle",
        ["WFL-DEL-0004"] = "aef_start_epic_lifecycle",
        ["SKL-EXE-0018"] = "aef_deliver_implementation",
        ["WFL-DEL-0006"] = "aef_plan_engineering",
        ["WFL-DEL-0007"] = "aef_review_specification",
        ["WFL-DEL-0008"] = "aef_analyze_repository",
        ["REPO-VALIDATION"] = "aef_validate_repository",
        ["PROMPT-GROUNDING"] = "aef_refine_prompt",
        ["CHANGE-CHECK"] = "aef_check_change",
        ["WFL-DEL-0009"] = "aef_plan_migration",
        ["WFL-DEL-0011"] = "aef_plan_maintenance",
        ["WFL-DEL-0012"] = "aef_manage_platform_lifecycle",
        ["WFL-DEL-0005"] = "aef_validate_implementation",
    };

    // The tools a coding agent is given by default; listing all fifteen costs about five times the tokens per session.
    public static readonly IReadOnlySet<string> DefaultToolNames = new HashSet<string> { "aef_refine_prompt", "aef_check_change" };

    public static ListToolsResult BuildListToolsResult(IReadOnlyList<CapabilityDescriptor> capabilities)
    {
        var tools = capabilities.Select(BuildTool).ToList();
        return new ListToolsResult { Tools = tools };
    }

    private static Tool BuildTool(CapabilityDescriptor capability)
    {
        if (!CapabilityIdToToolName.TryGetValue(capability.CapabilityId, out var name))
        {
            throw new InvalidOperationException(
                $"No MCP tool name is recorded for capability '{capability.CapabilityId}' - add it to {nameof(CapabilityIdToToolName)} and to the Epic's own MCP Tool Catalog, never invent one silently.");
        }

        return new Tool
        {
            Name = name,
            Description = capability.Description,
            InputSchema = BuildInputSchema(capability.RequiredPayloadKeys),
        };
    }

    // Every RequiredPayloadKeys entry becomes a required string property -
    // CapabilityDescriptor carries no richer type information than a key
    // name today (Pre-Story S3 Review §5, flagged as a Story S3
    // implementation judgment, not resolved by that review).
    private static JsonElement BuildInputSchema(IReadOnlyList<string> requiredPayloadKeys)
    {
        var properties = new Dictionary<string, object>();
        foreach (var key in requiredPayloadKeys)
        {
            properties[key] = new { type = "string" };
        }

        var schema = new
        {
            type = "object",
            properties,
            required = requiredPayloadKeys,
        };

        return JsonSerializer.SerializeToElement(schema);
    }
}
