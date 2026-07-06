using Kernel.Evidence;
using Kernel.Governance;
using Kernel.Supervision;
using Runtime.Context;

namespace Runtime.Hosts.Agent;

// The concrete, implementation-level specialization of the Runtime Instance
// Supervisor and Execution Context Manager, scoped to Agent Instances (I3 §12).
public sealed class AgentHost
{
    private readonly IInstanceSupervisor _supervisor;
    private readonly IExecutionContextManager _contextManager;
    private readonly ISkillInvoker? _skillInvoker;
    private readonly IEvidenceSink _evidenceSink;

    public AgentHost(
        IInstanceSupervisor supervisor,
        IExecutionContextManager contextManager,
        ISkillInvoker? skillInvoker = null,
        IEvidenceSink? evidenceSink = null)
    {
        _supervisor = supervisor;
        _contextManager = contextManager;
        _skillInvoker = skillInvoker;
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
    }

    public AgentActivationResult Activate(ActivationRequest request, ContextSeed seed)
    {
        var created = _supervisor.CreateInstance(request);
        if (created.Outcome != InstanceCreationOutcome.Created)
        {
            return AgentActivationResult.Rejected(created.Reason ?? "Activation was not created.");
        }

        var context = _contextManager.CreateContext(created.Handle!.InstanceId, seed);
        return AgentActivationResult.Created(new AgentInstanceHandle(created.Handle.InstanceId, context, this));
    }

    public void Destroy(string instanceId)
    {
        _supervisor.DestroyInstance(instanceId);
        _contextManager.OnInstanceCompletedOrTerminated(instanceId);
    }

    internal SkillInvocationResult InvokeSkill(RuntimeExecutionContext callerContext, string skillId)
    {
        if (_skillInvoker is null)
        {
            var reason = "No Skill Host is registered to invoke this Skill.";
            SignalGap(callerContext.InstanceId, reason);
            return SkillInvocationResult.CapabilityGap(reason);
        }

        return _skillInvoker.Invoke(callerContext, skillId);
    }

    internal void SignalGap(string instanceId, string description) =>
        _evidenceSink.Record(new EvidenceRecord(
            Contract: "AgentHost",
            Decision: "CapabilityGap",
            SubjectId: instanceId,
            Reason: description,
            Timestamp: DateTimeOffset.UtcNow));
}
