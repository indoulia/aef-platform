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

        // On Windows npm, yarn, pnpm and mvn are .cmd shims, which CreateProcess cannot launch directly.
        var batchShim = ResolveWindowsBatchShim(command);

        var startInfo = new ProcessStartInfo(batchShim is null ? command : "cmd.exe")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Never inherit the host's stdin: under the MCP stdio server that pipe hangs the child.
            RedirectStandardInput = true,
        };

        if (batchShim is not null)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(batchShim);
        }

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
            process.StandardInput.Close();

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

    // Returns the full path of <command>.cmd/.bat on PATH when no .exe/.com of that name precedes it; otherwise null.
    private static string? ResolveWindowsBatchShim(string command)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(command) || command.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            return null;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, command + ".exe")) || File.Exists(Path.Combine(directory, command + ".com")))
                {
                    return null;
                }

                foreach (var extension in new[] { ".cmd", ".bat" })
                {
                    var candidate = Path.Combine(directory, command + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry is skipped, not fatal.
            }
        }

        return null;
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
