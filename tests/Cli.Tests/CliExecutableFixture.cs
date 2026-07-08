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
        var startInfo = new ProcessStartInfo("dotnet", string.Join(' ', new[] { $"\"{LocateCliExecutablePath()}\"" }.Concat(args.Select(a => $"\"{a}\""))))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }
}
