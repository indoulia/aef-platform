using Runtime.Agent.Capabilities;

namespace Adapters.Mcp.Tests;

// Epic M2-0001 Story S3: unit-level coverage of the pure translation logic
// (CapabilityDescriptor -> MCP Tool), independent of the subprocess-level
// integration test in Mcp.Tests - the same test-pyramid split
// Adapters.Rest.Tests already uses for its own translation types.
public class McpToolCatalogTests
{
    [Fact]
    public void BuildListToolsResult_ProducesOneToolPerCapability_WithTranscribedNameAndDescription()
    {
        var capabilities = new[]
        {
            new CapabilityDescriptor("WFL-DEL-0006", "Engineering Planning", "Runs Discovery and Drafting.", new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
            new CapabilityDescriptor("WFL-DEL-0007", "Specification Review", "Runs an independent review.", new[] { "SpecificationReviewRequest" }, "Synchronous"),
        };

        var result = McpToolCatalog.BuildListToolsResult(capabilities);

        Assert.Equal(2, result.Tools.Count);

        var planningTool = Assert.Single(result.Tools, t => t.Name == "aef_plan_engineering");
        Assert.Equal("Runs Discovery and Drafting.", planningTool.Description);

        var reviewTool = Assert.Single(result.Tools, t => t.Name == "aef_review_specification");
        Assert.Equal("Runs an independent review.", reviewTool.Description);
    }

    [Fact]
    public void BuildListToolsResult_InputSchema_RequiresEveryPayloadKey()
    {
        var capabilities = new[]
        {
            new CapabilityDescriptor("WFL-DEL-0006", "Engineering Planning", "Runs Discovery and Drafting.", new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        };

        var result = McpToolCatalog.BuildListToolsResult(capabilities);
        var tool = Assert.Single(result.Tools);

        var required = tool.InputSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "RepositoryName", "RepositoryRoot" }, required);

        var properties = tool.InputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("RepositoryName", out _));
        Assert.True(properties.TryGetProperty("RepositoryRoot", out _));
    }

    [Fact]
    public void BuildListToolsResult_ThrowsRatherThanInventingAName_ForAnUnmappedCapabilityId()
    {
        var capabilities = new[]
        {
            new CapabilityDescriptor("WFL-DEL-9999", "Unknown Future Capability", "Not yet on the Epic's own MCP Tool Catalog.", Array.Empty<string>(), "Synchronous"),
        };

        Assert.Throws<InvalidOperationException>(() => McpToolCatalog.BuildListToolsResult(capabilities));
    }
}
