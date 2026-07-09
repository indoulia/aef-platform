using System.Collections.Concurrent;

namespace Runtime.Context;

public sealed class ExecutionContextManager : IExecutionContextManager
{
    private readonly IContextDisposalPolicy _disposalPolicy;

    // GA-3 Capability 5's real-concurrent-HTTP-request test found this
    // plain Dictionary corrupted under concurrent REST requests (multiple
    // threads calling CreateContext/OnInstanceCompletedOrTerminated on the
    // same singleton ExecutionContextManager) - a genuine, previously-
    // undiscovered race, since nothing called this Runtime with concurrent
    // callers before Capability 2 (REST) existed. Fixed the same way
    // Capability 1 fixed InMemorySessionStore's equivalent race: swap to
    // ConcurrentDictionary, no behavior or contract change otherwise.
    private readonly ConcurrentDictionary<string, RuntimeExecutionContext> _contexts = new();

    public ExecutionContextManager(IContextDisposalPolicy? disposalPolicy = null)
    {
        _disposalPolicy = disposalPolicy ?? new DiscardUnconditionallyPolicy();
    }

    public RuntimeExecutionContext CreateContext(string instanceId, ContextSeed seed)
    {
        var context = new RuntimeExecutionContext(
            instanceId,
            seed.Tier0,
            seed.ProjectProfileId,
            seed.CurrentStepId,
            seed.Tier3SkillContent ?? new Dictionary<string, object>());

        _contexts[instanceId] = context;
        return context;
    }

    public RuntimeExecutionContext? GetContext(string instanceId) =>
        _contexts.TryGetValue(instanceId, out var context) ? context : null;

    // Preservation is implicit: a paused Instance's Context is simply never
    // removed until OnInstanceCompletedOrTerminated fires (I1 §7 — "preserved
    // for the pause's duration only"). No separate paused-set is needed.
    public void OnInstancePaused(string instanceId)
    {
    }

    public void OnInstanceResumed(string instanceId)
    {
    }

    public void OnInstanceCompletedOrTerminated(string instanceId)
    {
        if (_disposalPolicy.ShouldDiscardOnCompletion(instanceId))
        {
            _contexts.TryRemove(instanceId, out _);
        }
    }
}
