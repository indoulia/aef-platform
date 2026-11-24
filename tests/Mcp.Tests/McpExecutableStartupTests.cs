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

            // Story S7 adds 2 infrastructure tools (aef_health/aef_version)
            // alongside the 11 capability-derived tools - updated here,
            // not left broken, since this Story intentionally changes the
            // total count this test already asserted.
            Assert.Equal(expectedCapabilities.Count + 2, actualTools.Count);

            foreach (var capability in expectedCapabilities)
            {
                var expectedName = ExpectedToolNames[capability.CapabilityId];
                Assert.True(actualTools.TryGetValue(expectedName, out var actualTool), $"Expected tool '{expectedName}' was not present in tools/list.");
                Assert.Equal(capability.Description, actualTool.GetProperty("description").GetString());
                Assert.True(actualTool.TryGetProperty("inputSchema", out _), $"Tool '{expectedName}' has no inputSchema.");
            }

            Assert.True(actualTools.ContainsKey("aef_health"));
            Assert.True(actualTools.ContainsKey("aef_version"));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    // Epic M2-0001 Story S7: real subprocess round trips for aef_health/
    // aef_version, mirroring RestAdapterEndpointExtensions' own Health()/
    // Version() endpoints.
    [Fact]
    public async Task McpExecutable_ToolsCall_ForHealth_ReturnsAWellFormedNonErrorResult()
    {
        using var process = StartMcpProcess();
        try
        {
            await InitializeAsync(process);
            using var document = await CallToolAsync(process, "aef_health");

            Assert.True(document.RootElement.TryGetProperty("result", out var result));
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var text = result.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.Contains("Healthy", text, StringComparison.OrdinalIgnoreCase);
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
    public async Task McpExecutable_ToolsCall_ForVersion_ReturnsANonEmptyVersionString()
    {
        using var process = StartMcpProcess();
        try
        {
            await InitializeAsync(process);
            using var document = await CallToolAsync(process, "aef_version");

            Assert.True(document.RootElement.TryGetProperty("result", out var result));
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var text = result.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.False(string.IsNullOrWhiteSpace(text));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static async Task InitializeAsync(Process process)
    {
        const string initializeRequest = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"McpExecutableStartupTests","version":"1.0.0"}}}""";
        await process.StandardInput.WriteLineAsync(initializeRequest);
        await process.StandardInput.FlushAsync();
        var initializeResponse = await ReadLineWithTimeoutAsync(process.StandardOutput, TimeSpan.FromSeconds(30));
        Assert.NotNull(initializeResponse);

        const string initializedNotification = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";
        await process.StandardInput.WriteLineAsync(initializedNotification);
        await process.StandardInput.FlushAsync();
    }

    private static async Task<JsonDocument> CallToolAsync(Process process, string toolName, string argumentsJson = "{}")
    {
        var request = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + argumentsJson + "}}";
        await process.StandardInput.WriteLineAsync(request);
        await process.StandardInput.FlushAsync();

        var response = await ReadLineWithTimeoutAsync(process.StandardOutput, TimeSpan.FromSeconds(30));
        Assert.NotNull(response);
        return JsonDocument.Parse(response);
    }

    // Epic M2-0001 Story S4: the Feature Lifecycle start-trigger tool
    // declares no required payload keys, making it the simplest real,
    // in-scope, end-to-end round trip to verify - fixed routing literal
    // ("feature") -> real Runtime dispatch -> a well-formed CallToolResult.
    [Fact]
    public async Task McpExecutable_ToolsCall_ForInScopeTool_ReturnsWellFormedResult()
    {
        using var process = StartMcpProcess();
        try
        {
            await InitializeAsync(process);
            using var document = await CallToolAsync(process, "aef_start_feature_lifecycle");

            Assert.True(document.RootElement.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("content", out var content));
            Assert.True(content.GetArrayLength() > 0);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "AefPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return directory.FullName;
    }

    // Story S8 sweep finding: git children inherited this server's stdin pipe and hung until timeout.
    [Fact]
    public async Task McpExecutable_ToolsCall_ForRepositoryTool_AgainstARealGitRepository_RespondsWithoutHanging()
    {
        using var process = StartMcpProcess();
        try
        {
            await InitializeAsync(process);
            var arguments = JsonSerializer.Serialize(new { RepositoryName = "aef-platform", RepositoryRoot = LocateRepositoryRoot() });
            using var document = await CallToolAsync(process, "aef_analyze_repository", arguments);

            var result = document.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            Assert.Contains("Completed via WFL-DEL-0008.", result.GetProperty("content")[0].GetProperty("text").GetString());
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    // Deferred tools (Pre-Story S4 Review §2.2) must produce a clear,
    // honest "not yet invocable" result - never a crash, never a silent
    // success, never indistinguishable from a genuinely unknown tool name.
    [Fact]
    public async Task McpExecutable_ToolsCall_ForDeferredTool_ReturnsIsErrorTrueWithClearMessage()
    {
        using var process = StartMcpProcess();
        try
        {
            await InitializeAsync(process);
            using var document = await CallToolAsync(process, "aef_review_specification");

            Assert.True(document.RootElement.TryGetProperty("result", out var result));
            Assert.True(result.GetProperty("isError").GetBoolean());
            var text = result.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.Contains("deferred", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    // Open item from the Pre-Story S4 Review (§1/§4): the SDK's own
    // unhandled-exception behavior for WithCallToolHandler was not
    // documented anywhere found. Verified here directly against the real
    // process, not assumed: calling a name never advertised via tools/list
    // reaches McpToolInvoker.BuildExecutionRequest's own "not a Story S4
    // in-scope tool" throw - the process must not crash, and the client
    // must receive some well-formed response (a JSON-RPC error or an
    // IsError result), not a hang or a dropped connection.
    [Fact]
    public async Task McpExecutable_ToolsCall_ForUnknownToolName_DoesNotCrashTheProcess()
    {
        using var process = StartMcpProcess();
        try
        {
            await InitializeAsync(process);
            using var document = await CallToolAsync(process, "not_a_real_tool");

            Assert.False(process.HasExited);
            Assert.True(document.RootElement.TryGetProperty("result", out _) || document.RootElement.TryGetProperty("error", out _));
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
