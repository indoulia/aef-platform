namespace Runtime.Context;

public sealed class ExecutionContextManager : IExecutionContextManager
{
    private readonly IContextDisposalPolicy _disposalPolicy;
    private readonly Dictionary<string, RuntimeExecutionContext> _contexts = new();

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
            _contexts.Remove(instanceId);
        }
    }
}
