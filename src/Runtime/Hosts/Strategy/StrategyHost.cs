using System.Collections.Concurrent;
using Kernel.Evidence;
using Kernel.Governance;
using Kernel.Supervision;
using Runtime.Context;
using Runtime.Hosts.Agent;

namespace Runtime.Hosts.Strategy;

// The concrete specialization for Strategy Instances (I3 §14, I1 §10 case 1).
public sealed class StrategyHost
{
    private readonly IInstanceSupervisor _supervisor;
    private readonly IExecutionContextManager _contextManager;
    private readonly IAgentAllocator? _agentAllocator;
    private readonly IEvidenceSink _evidenceSink;

    // Same real, demonstrated race as ExecutionContextManager's own fix
    // (GA-3 Capability 5's concurrent-HTTP-request test) - StrategyHost is
    // a singleton within RuntimeHandle, so concurrent REST requests can
    // call Activate/TransitionSession on the same instance concurrently.
    private readonly ConcurrentDictionary<string, StrategyInstanceHandle> _instances = new();

    public StrategyHost(
        IInstanceSupervisor supervisor,
        IExecutionContextManager contextManager,
        IAgentAllocator? agentAllocator = null,
        IEvidenceSink? evidenceSink = null)
    {
        _supervisor = supervisor;
        _contextManager = contextManager;
        _agentAllocator = agentAllocator;
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
    }

    public StrategyActivationResult Activate(ActivationRequest request, ContextSeed seed)
    {
        var created = _supervisor.CreateInstance(request);
        if (created.Outcome != InstanceCreationOutcome.Created)
        {
            return StrategyActivationResult.Rejected(created.Reason ?? "Activation was not created.");
        }

        var context = _contextManager.CreateContext(created.Handle!.InstanceId, seed);
        var handle = new StrategyInstanceHandle(created.Handle.InstanceId, context, this);
        _instances[handle.InstanceId] = handle;
        return StrategyActivationResult.Created(handle);
    }

    internal AgentActivationResult AllocateStep(string agentDefinitionId, ContextSeed seed)
    {
        if (_agentAllocator is null)
        {
            return AgentActivationResult.Rejected("No Agent allocator is registered to allocate this step.");
        }

        return _agentAllocator.AllocateStep(agentDefinitionId, seed);
    }

    internal void TransitionSession(string instanceId, SessionState from, SessionState to)
    {
        // Checkpointing (I1 §6): preserve the Context for the pause's duration
        // only, via the same Execution Context Manager preservation mechanism
        // S5 already provides — no separate Checkpoint store is introduced.
        switch (to)
        {
            case SessionState.PausedApproval:
            case SessionState.PausedClarification:
            case SessionState.PausedTerminal:
                _contextManager.OnInstancePaused(instanceId);
                break;
            case SessionState.Running:
                _contextManager.OnInstanceResumed(instanceId);
                break;
            case SessionState.Completed:
            case SessionState.Terminated:
                _supervisor.DestroyInstance(instanceId);
                _contextManager.OnInstanceCompletedOrTerminated(instanceId);
                _instances.TryRemove(instanceId, out _);
                break;
        }

        _evidenceSink.Record(new EvidenceRecord(
            Contract: "StrategyHost",
            Decision: $"{from}->{to}",
            SubjectId: instanceId,
            Reason: null,
            Timestamp: DateTimeOffset.UtcNow));
    }
}
