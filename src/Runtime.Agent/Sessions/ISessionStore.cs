namespace Runtime.Agent.Sessions;

public interface ISessionStore
{
    ExecutionSession Create(string? projectProfileId);
    ExecutionSession? Get(string sessionId);
    ExecutionSession Touch(string sessionId, string? projectProfileId, IReadOnlyDictionary<string, object>? payload);
}
