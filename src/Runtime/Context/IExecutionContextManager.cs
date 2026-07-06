namespace Runtime.Context;

public interface IExecutionContextManager
{
    RuntimeExecutionContext CreateContext(string instanceId, ContextSeed seed);

    RuntimeExecutionContext? GetContext(string instanceId);

    void OnInstancePaused(string instanceId);

    void OnInstanceResumed(string instanceId);

    void OnInstanceCompletedOrTerminated(string instanceId);
}
