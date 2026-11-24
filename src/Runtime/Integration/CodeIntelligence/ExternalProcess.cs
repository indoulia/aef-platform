using System.ComponentModel;
using System.Diagnostics;

namespace Runtime.Integration.CodeIntelligence;

public sealed record ProcessOutput(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool NotFound);

public delegate ProcessOutput ProcessRunner(string workingDirectory, string fileName, IReadOnlyList<string> arguments, TimeSpan timeout);

public static class ExternalProcess
{
    // Same discipline as LocalGitOperations: ArgumentList, both streams drained concurrently, stdin never inherited.
    public static ProcessOutput Run(string workingDirectory, string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or DirectoryNotFoundException)
        {
            return new ProcessOutput(-1, string.Empty, ex.Message, TimedOut: false, NotFound: true);
        }

        if (process is null)
        {
            return new ProcessOutput(-1, string.Empty, string.Empty, TimedOut: false, NotFound: true);
        }

        using (process)
        {
            process.StandardInput.Close();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort: it may have exited between the timeout and the kill.
                }

                return new ProcessOutput(-1, string.Empty, string.Empty, TimedOut: true, NotFound: false);
            }

            return new ProcessOutput(process.ExitCode, standardOutputTask.GetAwaiter().GetResult(), standardErrorTask.GetAwaiter().GetResult(), false, false);
        }
    }
}
