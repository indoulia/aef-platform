using System.Diagnostics;
using System.Net.Http;

namespace Host.Tests;

// Regression test for an execution-discovered deployment defect (GA-3
// Capability 5): WebApplication.CreateBuilder's default ContentRootPath
// is the process's current working directory, not the executable's own
// directory, so appsettings.json's "Ai:OpenAi" section silently failed
// to bind - and the process crashed at first AI-provider use - whenever
// AefPlatform.Host.dll was launched from anywhere other than its own
// output directory. Fixed by pinning ContentRootPath to
// AppContext.BaseDirectory. WebApplicationFactory-based tests elsewhere
// in this suite never caught this, since WebApplicationFactory resolves
// its own content root differently - only a real, separate process
// launch reproduces it.
public class HostExecutableStartupTests
{
    private static string LocateHostExecutablePath()
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

        var hostExecutablePath = Path.Combine(repositoryRoot.FullName, "src", "Host", "bin", configurationSegment, targetFrameworkSegment, "AefPlatform.Host.dll");
        if (!File.Exists(hostExecutablePath))
        {
            throw new FileNotFoundException($"AefPlatform.Host.dll not found at '{hostExecutablePath}' - ensure the Host project has been built.", hostExecutablePath);
        }

        return hostExecutablePath;
    }

    private static async Task<bool> BootsSuccessfullyAsync(string workingDirectory, int port)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(LocateHostExecutablePath());
        startInfo.EnvironmentVariables["RestAdapter__Port"] = port.ToString();

        using var process = Process.Start(startInfo)!;
        try
        {
            // A generous deadline: this test can run alongside the rest of
            // the solution's test suite (many concurrent processes), where
            // process startup is measurably slower than in isolation.
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    return false;
                }

                try
                {
                    var response = await httpClient.GetAsync($"http://localhost:{port}/api/v1/health");
                    return true; // any HTTP response (200 or 503) means the process booted and is serving.
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(250);
                }
            }

            return false;
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
    public async Task Host_BootsSuccessfully_WhenLaunchedFromItsOwnOutputDirectory()
    {
        var hostDirectory = Path.GetDirectoryName(LocateHostExecutablePath())!;

        Assert.True(await BootsSuccessfullyAsync(hostDirectory, 5091));
    }

    [Fact]
    public async Task Host_BootsSuccessfully_WhenLaunchedFromAnArbitraryWorkingDirectory()
    {
        // The exact scenario that used to crash: a working directory that
        // is neither the Host's own output directory nor anywhere
        // appsettings.json happens to also exist.
        var arbitraryDirectory = Path.GetTempPath();

        Assert.True(await BootsSuccessfullyAsync(arbitraryDirectory, 5092));
    }
}
