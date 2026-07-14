using System.Reflection;
using ModelContextProtocol.Protocol;
using Runtime.Agent.Health;

namespace Adapters.Mcp.Tests;

// Epic M2-0001 Story S7: unit-level coverage of the pure translation logic
// for aef_health/aef_version, mirroring RestAdapterEndpointExtensions'
// own Health()/Version() methods exactly - same RuntimeHealthAggregator,
// same Unhealthy-only error threshold (Degraded still reports success,
// same as REST's own 200-with-Degraded-body behavior), same
// AssemblyInformationalVersionAttribute resolution REST's own
// GetInformationalVersion() already uses (not Adapters.Cli's
// PlatformVersion.Resolve, whose SemVer-build-metadata stripping is
// specific to Cli's own `install --version` comparison need, irrelevant
// here).
public class McpInfrastructureToolsTests
{
    [Theory]
    [InlineData(RuntimeHealthStatus.Healthy, false)]
    [InlineData(RuntimeHealthStatus.Degraded, false)]
    [InlineData(RuntimeHealthStatus.Unhealthy, true)]
    public void BuildHealthResult_OnlyUnhealthyMapsToIsErrorTrue_MirroringRestsOwn503Threshold(RuntimeHealthStatus status, bool expectedIsError)
    {
        var report = new RuntimeHealthReport("Aggregate", status, status == RuntimeHealthStatus.Healthy ? null : "some reason");

        var result = McpInfrastructureTools.BuildHealthResult(report);

        Assert.Equal(expectedIsError, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(status.ToString(), text.Text);
    }

    [Fact]
    public void BuildVersionResult_ReportsTheAssemblysInformationalVersion()
    {
        var result = McpInfrastructureTools.BuildVersionResult(Assembly.GetExecutingAssembly());

        Assert.False(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.NotEmpty(text.Text);
    }

    [Fact]
    public void IsInfrastructureTool_RecognizesExactlyHealthAndVersion()
    {
        Assert.True(McpInfrastructureTools.IsInfrastructureTool("aef_health"));
        Assert.True(McpInfrastructureTools.IsInfrastructureTool("aef_version"));
        Assert.False(McpInfrastructureTools.IsInfrastructureTool("aef_plan_engineering"));
        Assert.False(McpInfrastructureTools.IsInfrastructureTool("not_a_real_tool"));
    }

    [Fact]
    public void Tools_ListsExactlyHealthAndVersion_WithNoRequiredInput()
    {
        Assert.Equal(2, McpInfrastructureTools.Tools.Count);
        Assert.Contains(McpInfrastructureTools.Tools, t => t.Name == "aef_health");
        Assert.Contains(McpInfrastructureTools.Tools, t => t.Name == "aef_version");
    }
}
