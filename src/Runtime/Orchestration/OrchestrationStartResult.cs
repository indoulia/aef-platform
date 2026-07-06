using Runtime.Hosts.Agent;
using Runtime.Hosts.Strategy;

namespace Runtime.Orchestration;

public enum OrchestrationStartOutcome
{
    Started,
    Rejected,
}

public sealed record OrchestrationStartResult(
    OrchestrationStartOutcome Outcome,
    StrategyInstanceHandle? Strategy,
    AgentInstanceHandle? FirstStepAgent,
    string? Reason)
{
    public static OrchestrationStartResult Started(StrategyInstanceHandle strategy, AgentInstanceHandle firstStepAgent) =>
        new(OrchestrationStartOutcome.Started, strategy, firstStepAgent, Reason: null);

    public static OrchestrationStartResult Rejected(string reason) =>
        new(OrchestrationStartOutcome.Rejected, Strategy: null, FirstStepAgent: null, reason);
}
