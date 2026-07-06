using Runtime.Context;
using Runtime.Hosts.Agent;

namespace Runtime.Hosts.Strategy;

// The exactly-one-call surface I3 §14 fixes for a running Strategy Instance:
// allocate-a-step-to-an-Agent. There is, deliberately, no method here that
// would let a Strategy invoke a Skill directly — restating I1 §17's
// "Strategy cannot invoke Skill directly" as an absent call surface. The
// remaining methods realize the Session Lifecycle state machine (I1 §8).
public sealed class StrategyInstanceHandle
{
    private readonly StrategyHost _host;

    public string InstanceId { get; }

    public RuntimeExecutionContext Context { get; }

    public SessionState State { get; internal set; }

    internal StrategyInstanceHandle(string instanceId, RuntimeExecutionContext context, StrategyHost host)
    {
        InstanceId = instanceId;
        Context = context;
        _host = host;
        State = SessionState.Running;
    }

    public AgentActivationResult AllocateStep(string agentDefinitionId, ContextSeed seed) =>
        _host.AllocateStep(agentDefinitionId, seed);

    public bool PauseForApproval() => TransitionTo(SessionState.PausedApproval);

    public bool PauseForClarification() => TransitionTo(SessionState.PausedClarification);

    public bool PauseForTerminalFailure() => TransitionTo(SessionState.PausedTerminal);

    public bool Resume() => TransitionTo(SessionState.Running);

    public bool Complete() => TransitionTo(SessionState.Completed);

    public bool Terminate() => TransitionTo(SessionState.Terminated);

    private bool TransitionTo(SessionState target)
    {
        if (!SessionTransitions.IsAllowed(State, target))
        {
            return false;
        }

        _host.TransitionSession(InstanceId, State, target);
        State = target;
        return true;
    }
}
