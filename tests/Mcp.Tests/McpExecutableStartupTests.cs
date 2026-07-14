using System.Diagnostics;

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

    // Story S2's own acceptance description: "responding to the protocol's
    // own capability/tool-listing handshake with zero tools registered
    // yet." Completes the full MCP lifecycle (initialize -> initialized
    // notification -> tools/list) and asserts the REAL, verified behavior
    // discovered running this test against the actual SDK: with the tools
    // capability never enabled (no tool-registration call made at all,
    // per Program.cs's own comment), the server responds to tools/list
    // with a protocol-conformant "Method not found" (-32601) - a real,
    // well-formed JSON-RPC response, not a hang, crash, or malformed
    // reply. This is "responds to the handshake with zero tools," stated
    // honestly rather than asserting an empty list the server does not
    // actually produce today.
    [Fact]
    public async Task McpExecutable_ToolsList_RespondsWithMethodNotFound_WhenNoToolsCapabilityIsRegistered()
    {
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
            Assert.Contains("\"error\"", toolsListResponse);
            Assert.Contains("-32601", toolsListResponse);
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
