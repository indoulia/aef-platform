using Runtime.Agent;
using Runtime.Agent.Capabilities;

namespace Adapters.Cli.Tests;

public class CliOutputFormatterTests
{
    private static ExecutionResponse SampleResponse() => new(
        "req-1", "session-1", ExecutionStatus.Completed, null, "Done.", "No further action needed.",
        new ExecutionEvidence(Array.Empty<string>(), null, null, null, null),
        new ExecutionMetadata("WFL-DEL-0001", null, TimeSpan.FromSeconds(1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    [Fact]
    public void FormatExecutionResponse_TextMode_ContainsStatusAndPresentedResult()
    {
        var formatted = CliOutputFormatter.FormatExecutionResponse(SampleResponse(), "text");

        Assert.Contains("Completed", formatted);
        Assert.Contains("Done.", formatted);
    }

    [Fact]
    public void FormatExecutionResponse_JsonMode_IsValidJson()
    {
        var formatted = CliOutputFormatter.FormatExecutionResponse(SampleResponse(), "json");

        using var document = System.Text.Json.JsonDocument.Parse(formatted);
        Assert.True(document.RootElement.TryGetProperty("status", out _));
    }

    [Fact]
    public void FormatCapabilities_TextMode_ListsEachCapability()
    {
        var capabilities = new[] { new CapabilityDescriptor("WFL-DEL-0001", "Feature", "desc", Array.Empty<string>(), "Synchronous") };

        var formatted = CliOutputFormatter.FormatCapabilities(capabilities, "text");

        Assert.Contains("WFL-DEL-0001", formatted);
    }

    [Fact]
    public void FormatCapabilities_JsonMode_IsValidJsonArray()
    {
        var capabilities = new[] { new CapabilityDescriptor("WFL-DEL-0001", "Feature", "desc", Array.Empty<string>(), "Synchronous") };

        var formatted = CliOutputFormatter.FormatCapabilities(capabilities, "json");

        using var document = System.Text.Json.JsonDocument.Parse(formatted);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, document.RootElement.ValueKind);
    }
}
