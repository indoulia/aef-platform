using System.Diagnostics;

namespace Cli.Tests;

public static class CliExecutableFixture
{
    public static string LocateCliExecutablePath()
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

        var cliExecutablePath = Path.Combine(repositoryRoot.FullName, "src", "Cli", "bin", configurationSegment, targetFrameworkSegment, "AefPlatform.Cli.dll");
        if (!File.Exists(cliExecutablePath))
        {
            throw new FileNotFoundException($"AefPlatform.Cli.dll not found at '{cliExecutablePath}' - ensure the Cli project has been built.", cliExecutablePath);
        }

        return cliExecutablePath;
    }

    public static (int ExitCode, string StandardOutput, string StandardError) Run(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // ArgumentList (not a hand-built Arguments string) lets .NET apply
        // the platform's own correct argument-escaping - a manually
        // quoted "\"{a}\"" string breaks the moment any argument (e.g. a
        // JSON payload) itself contains a double quote, corrupting the
        // whole command line (execution-discovered while adding a
        // --payload-json regression test, GA-3 Capability 5).
        startInfo.ArgumentList.Add(LocateCliExecutablePath());
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }
}
