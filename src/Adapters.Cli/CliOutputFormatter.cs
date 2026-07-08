using System.Text.Json;
using Runtime.Agent;
using Runtime.Agent.Capabilities;

namespace Adapters.Cli;

public static class CliOutputFormatter
{
    // camelCase to match the REST Adapter's own JSON convention (ASP.NET
    // Core's Minimal API defaults) - a caller scripting against both
    // channels sees the same property casing either way.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string FormatExecutionResponse(ExecutionResponse response, string output)
    {
        if (output == "json")
        {
            return JsonSerializer.Serialize(response, JsonOptions);
        }

        var lines = new List<string>
        {
            $"Status: {response.Status}",
        };

        if (response.SessionId is not null)
        {
            lines.Add($"SessionId: {response.SessionId}");
        }

        if (response.PresentedResult is not null)
        {
            lines.Add(response.PresentedResult);
        }

        if (response.SuggestedNextStep is not null)
        {
            lines.Add($"Next step: {response.SuggestedNextStep}");
        }

        if (response.Error is not null)
        {
            lines.Add($"Error: {response.Error.Reason}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatCapabilities(IReadOnlyList<CapabilityDescriptor> capabilities, string output)
    {
        if (output == "json")
        {
            return JsonSerializer.Serialize(capabilities, JsonOptions);
        }

        return string.Join(Environment.NewLine, capabilities.Select(c => $"{c.CapabilityId} - {c.DisplayName}: {c.Description}"));
    }
}
