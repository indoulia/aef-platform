namespace Runtime.Agent;

public interface IExecutionProgressObserver
{
    void OnProgress(ExecutionProgressEvent progressEvent);
}
