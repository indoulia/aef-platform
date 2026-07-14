using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Runtime.Agent.Health;

namespace Adapters.Mcp;

// Epic M2-0001 Story S7: aef_health/aef_version, mirroring
// RestAdapterEndpointExtensions' own Health()/Version() methods exactly.
// RuntimeHealthAggregator/IRuntimeHealthContributor are reused unmodified
// (Runtime.Agent.Health, already reachable from Adapters.Mcp). No health
// contributor is registered for this channel today, mirroring src/Cli's
// own existing precedent (Cli registers none either) - aef_health
// therefore reports Healthy honestly because no real check exists yet,
// not because one was performed and passed; a disclosed limitation, not
// a defect, per this program's own "record gaps, don't force them"
// discipline.
public static class McpInfrastructureTools
{
    public static IReadOnlyList<Tool> Tools { get; } = new List<Tool>
    {
        new()
        {
            Name = "aef_health",
            Description = "Reports AEF Runtime health status.",
            InputSchema = EmptyInputSchema(),
        },
        new()
        {
            Name = "aef_version",
            Description = "Reports the AEF Platform's own compiled version.",
            InputSchema = EmptyInputSchema(),
        },
    };

    public static bool IsInfrastructureTool(string toolName) => toolName is "aef_health" or "aef_version";

    // Same threshold RestAdapterEndpointExtensions.Health() already uses:
    // only Unhealthy is an error (503 there, IsError here) - Degraded
    // still reports success with the real status text in the body/content,
    // never silently upgraded to Healthy or downgraded to an error.
    public static CallToolResult BuildHealthResult(RuntimeHealthReport report)
    {
        var text = report.Reason is null ? report.Status.ToString() : $"{report.Status}: {report.Reason}";
        return new CallToolResult
        {
            IsError = report.Status == RuntimeHealthStatus.Unhealthy,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }

    // Mirrors RestAdapterEndpointExtensions.GetInformationalVersion()
    // exactly - not Adapters.Cli's PlatformVersion.Resolve, whose SemVer
    // build-metadata stripping exists only for Cli's own `install
    // --version` comparison need, which this channel has no equivalent
    // of.
    public static CallToolResult BuildVersionResult(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        return new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock> { new TextContentBlock { Text = version } },
        };
    }

    private static JsonElement EmptyInputSchema() =>
        JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, required = Array.Empty<string>() });
}
