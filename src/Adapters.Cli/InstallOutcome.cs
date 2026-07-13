namespace Adapters.Cli;

public sealed record InstallOutcome(bool Succeeded, string? MaterializedPath, string? FailureReason);
