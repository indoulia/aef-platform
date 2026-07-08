using System.Diagnostics;

namespace Runtime.Integration.DeliveryExecution;

// The sole concrete binding of IVerificationRunner. Uses
// ProcessStartInfo.ArgumentList exclusively (never a concatenated
// command-line string), the same injection-safety discipline
// LocalGitChangeVerifier already applies (Story S30), so a verification
// argument can never be interpreted as a second shell command.
public sealed class ProcessVerificationRunner : IVerificationRunner
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(10);

    public VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrEmpty(command))
        {
            throw new ArgumentException("command must not be null or empty.", nameof(command));
        }

        var startInfo = new ProcessStartInfo(command)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start verification command '{command}' - is it installed and on PATH?");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // A structured, deliberate failure - never an incidental
            // framework exception - for the "the command itself does not
            // exist" case, distinct from "the command ran and failed"
            // (a normal, non-zero VerificationRunResult, not an
            // exception at all).
            throw new InvalidOperationException($"Failed to start verification command '{command}' - is it installed and on PATH?", ex);
        }

        using (process)
        {
            // Reading one stream to completion before starting the other
            // deadlocks as soon as the un-drained stream fills its OS pipe
            // buffer - a real build/test command routinely produces large,
            // concurrent output on both stdout and stderr.
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
            {
                TryKill(process);
                throw new TimeoutException($"Verification command '{command}' did not exit within {ProcessTimeout}.");
            }

            var standardOutput = standardOutputTask.GetAwaiter().GetResult();
            var standardError = standardErrorTask.GetAwaiter().GetResult();

            return new VerificationRunResult(process.ExitCode, standardOutput, standardError);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort only - the process may have exited between the
            // timeout check and this call.
        }
    }
}
