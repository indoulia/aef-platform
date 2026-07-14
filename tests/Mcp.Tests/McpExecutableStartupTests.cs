using System.Diagnostics;
using System.Text.Json;
using Runtime.Agent.Capabilities;

namespace Mcp.Tests;

// Epic M2-0001 Story S2: proves AefPlatform.Mcp actually boots and speaks
// MCP over stdio, mirroring Host.Tests/HostExecutableStartupTests.cs's own
// idiom (launch the real, compiled executable as a subprocess; a real
// process launch is what previously caught a ContentRootPath defect no
// in-process test caught) - adapted from an HTTP health-check poll to a
// stdio JSON-RPC round trip, since this channel has no listening port.
public class McpExecutableStartupTests
{
    private static string LocateMcpExecutablePath()
    {
        var testBinDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configurationSegment = testBinDirectory.Parent!.Name;
        var targetFrameworkSegment = testBinDirectory.Name;

        var repositoryRoot = testBinDirectory;
        while (repositoryRoot is not null && !File.Exists(Path.Combine(repositoryRoot.FullName, "AefPlatform.sln")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        if (repositoryRoot is null)
        {
            throw new InvalidOperationException("Could not locate AefPlatform.sln walking up from the test's base directory.");
        }

        var mcpExecutablePath = Path.Combine(repositoryRoot.FullName, "src", "Mcp", "bin", configurationSegment, targetFrameworkSegment, "AefPlatform.Mcp.dll");
        if (!File.Exists(mcpExecutablePath))
        {
            throw new FileNotFoundException($"AefPlatform.Mcp.dll not found at '{mcpExecutablePath}' - ensure the Mcp project has been built.", mcpExecutablePath);
        }

        return mcpExecutablePath;
    }

    private static async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, TimeSpan timeout)
    {
        var readTask = reader.ReadLineAsync();
        var completed = await Task.WhenAny(readTask, Task.Delay(timeout));
        return completed == readTask ? await readTask : null;
    }

    private static Process StartMcpProcess()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(LocateMcpExecutablePath()),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(LocateMcpExecutablePath());

        return Process.Start(startInfo)!;
    }

    private static async Task<string?> SendInitializeRequestAndReadResponseAsync()
    {
        using var process = StartMcpProcess();
        try
        {
            const string initializeRequest = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"McpExecutableStartupTests","version":"1.0.0"}}}""";
            await process.StandardInput.WriteLineAsync(initializeRequest);
            await process.StandardInput.FlushAsync();

            return await ReadLineWithTimeoutAsync(process.StandardOutput, TimeSpan.FromSeconds(30));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    public async Task McpExecutable_RespondsToInitializeHandshake_OverStdio()
    {
        var response = await SendInitializeRequestAndReadResponseAsync();

        Assert.NotNull(response);
        Assert.Contains("\"result\"", response);
    }

    // Epic M2-0001 Story S3: "one MCP tool is registered per
    // CapabilityDescriptor... transcribed from the existing
    // CapabilityDescriptor record, never hand-invented" - asserted here
    // against the SAME live catalog (EngineeringAgentCapabilityCatalog)
    // Program.cs's own ListToolsHandler reads from, not a hard-coded
    // count that could silently drift out of sync with it. Supersedes
    // Story S2's own "-32601 Method not found" test - that was the
    // correct, honest behavior for zero tools registered; Story S3
    // intentionally changes it, so the old assertion is replaced, not
    // left contradicting the new one.
    // Transcribed from the Epic's own approved MCP Tool Catalog
    // (docs/06-history/2026-07-14-aef-m2-epic-m2-0001-mcp-adapter-
    // capability-exposure-plan.md §5, ai-engineering-framework repo) -
    // the same mapping Program.cs's own ListToolsHandler must apply. Kept
    // here, not derived from CapabilityDescriptor itself, since the
    // friendly name is a presentation decision the Epic already made, not
    // something CapabilityDescriptor carries.
    private static readonly IReadOnlyDictionary<string, string> ExpectedToolNames = new Dictionary<string, string>
    {
        ["WFL-DEL-0001"] = "aef_start_feature_lifecycle",
        ["WFL-DEL-0004"] = "aef_start_epic_lifecycle",
        ["SKL-EXE-0018"] = "aef_deliver_implementation",
        ["WFL-DEL-0006"] = "aef_plan_engineering",
        ["WFL-DEL-0007"] = "aef_review_specification",
        ["WFL-DEL-0008"] = "aef_analyze_repository",
        ["REPO-VALIDATION"] = "aef_validate_repository",
        ["WFL-DEL-0009"] = "aef_plan_migration",
        ["WFL-DEL-0011"] = "aef_plan_maintenance",
        ["WFL-DEL-0012"] = "aef_manage_platform_lifecycle",
        ["WFL-DEL-0005"] = "aef_validate_implementation",
    };

    [Fact]
    public async Task McpExecutable_ToolsList_ReturnsOneToolPerCatalogCapability()
    {
        var expectedCapabilities = new EngineeringAgentCapabilityCatalog().Discover();
        Assert.Equal(ExpectedToolNames.Count, expectedCapabilities.Count);

        using var process = StartMcpProcess();
        try
        {
            const string initializeRequest = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"McpExecutableStartupTests","version":"1.0.0"}}}""";
            await process.StandardInput.WriteLineAsync(initializeRequest);
            await process.StandardInput.FlushAsync();
            var initializeResponse = await ReadLineWithTimeoutAsync(process.StandardOutput, TimeSpan.FromSeconds(30));
            Assert.NotNull(initializeResponse);

            const string initializedNotification = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";
            await process.StandardInput.WriteLineAsync(initializedNotification);
            await process.StandardInput.FlushAsync();

            const string toolsListRequest = """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""";
            await process.StandardInput.WriteLineAsync(toolsListRequest);
            await process.StandardInput.FlushAsync();

            var toolsListResponse = await ReadLineWithTimeoutAsync(process.StandardOutput, TimeSpan.FromSeconds(30));
            Assert.NotNull(toolsListResponse);

            // Parsed, structured comparison - not raw substring matching -
            // since System.Text.Json's default encoder escapes characters
            // like '&' (present in real capability descriptions), which a
            // literal Contains() check against the un-escaped source text
            // would incorrectly fail on.
            using var document = JsonDocument.Parse(toolsListResponse);
            var root = document.RootElement;
            Assert.True(root.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("tools", out var toolsElement));

            var actualTools = toolsElement.EnumerateArray()
                .ToDictionary(tool => tool.GetProperty("name").GetString()!, tool => tool);

            Assert.Equal(expectedCapabilities.Count, actualTools.Count);

            foreach (var capability in expectedCapabilities)
            {
                var expectedName = ExpectedToolNames[capability.CapabilityId];
                Assert.True(actualTools.TryGetValue(expectedName, out var actualTool), $"Expected tool '{expectedName}' was not present in tools/list.");
                Assert.Equal(capability.Description, actualTool.GetProperty("description").GetString());
                Assert.True(actualTool.TryGetProperty("inputSchema", out _), $"Tool '{expectedName}' has no inputSchema.");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
