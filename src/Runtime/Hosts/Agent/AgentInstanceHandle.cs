using Runtime.Context;

namespace Runtime.Hosts.Agent;

// The exactly-two-calls surface I3 §12 fixes for a running Agent Instance:
// invoke-a-declared-Skill and signal-a-capability-gap. There is, deliberately,
// no third method here of any kind that would let an Agent invoke another
// Agent — restating I1 §17's "Agent cannot invoke Agent" as an absent call
// surface, not a runtime check.
public sealed class AgentInstanceHandle
{
    private readonly AgentHost _host;

    public string InstanceId { get; }

    public RuntimeExecutionContext Context { get; }

    internal AgentInstanceHandle(string instanceId, RuntimeExecutionContext context, AgentHost host)
    {
        InstanceId = instanceId;
        Context = context;
        _host = host;
    }

    public SkillInvocationResult InvokeSkill(string skillId) => _host.InvokeSkill(Context, skillId);

    public void SignalGap(string description) => _host.SignalGap(InstanceId, description);
}
