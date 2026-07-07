using System.Diagnostics;

namespace Runtime.Integration.DeliveryExecution;

// The sole concrete binding of IVerificationRunner. Uses
// ProcessStartInfo.ArgumentList exclusively (never a concatenated
// command-line string), the same injection-safety discipline
// LocalGitChangeVerifier already applies (Story S30), so a verification
// argument can never be interpreted as a second shell command.
public sealed class ProcessVerificationRunner : IVerificationRunner
{
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
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new VerificationRunResult(process.ExitCode, standardOutput, standardError);
        }
    }
}
