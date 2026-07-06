namespace Runtime.Orchestration;

public enum GateResumeOutcome
{
    Resumed,
    Terminated,
    Rejected,
}

public sealed record GateResumeResult(GateResumeOutcome Outcome, string? Reason)
{
    public static readonly GateResumeResult Resumed = new(GateResumeOutcome.Resumed, Reason: null);

    public static readonly GateResumeResult Terminated = new(GateResumeOutcome.Terminated, Reason: null);

    public static GateResumeResult Rejected(string reason) => new(GateResumeOutcome.Rejected, reason);
}
