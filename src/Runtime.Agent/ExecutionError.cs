namespace Runtime.Agent;

// Populated whenever ExecutionResponse.Status != ExecutionStatus.Completed.
public sealed record ExecutionError(string Reason, IReadOnlyList<string> Diagnostics);
