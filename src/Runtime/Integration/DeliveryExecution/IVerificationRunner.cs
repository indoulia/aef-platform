namespace Runtime.Integration.DeliveryExecution;

// The generic Integration Adapter Boundary contract for re-running a
// captured verification procedure against real, isolated change output
// (ai-engineering-framework repo, Epic 0005, Story S31) - realizing
// CEI-DEL-07's Self-Validation-on-Own-Output Practice. Unlike
// IRepositoryScanner (Epic 0008, deliberately read-only/no-process-
// execution by its own Story S22 scope), this adapter's entire purpose is
// invoking a real verification command against isolated change output -
// process execution is this capability's job, not something it must
// avoid. Still narrowly scoped: it can only run a command and observe its
// real exit code/output, never merge, push, or otherwise mutate anything.
public interface IVerificationRunner
{
    VerificationRunResult Run(string workingDirectory, string command, IReadOnlyList<string> arguments);
}

// Real, observed process output only - never a caller's own claim about
// whether verification passed.
public sealed record VerificationRunResult(int ExitCode, string StandardOutput, string StandardError);
