using System.Collections.Concurrent;

namespace Runtime.Agent.Sessions;

// Process-lifetime only, never persisted to disk or any store - satisfies
// "no persistent memory / no long-term knowledge storage" literally, while
// still delivering conversation/session support. Session state is pure
// caller convenience: it never influences routing or dispatch.
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, ExecutionSession> _sessions = new();

    public ExecutionSession Create(string? projectProfileId)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ExecutionSession(Guid.NewGuid().ToString("N"), now, now, projectProfileId, null, TurnCount: 0);
        _sessions[session.SessionId] = session;
        return session;
    }

    public ExecutionSession? Get(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public ExecutionSession Touch(string sessionId, string? projectProfileId, IReadOnlyDictionary<string, object>? payload)
    {
        if (!_sessions.TryGetValue(sessionId, out var existing))
        {
            throw new KeyNotFoundException($"No session exists for id '{sessionId}'.");
        }

        var updated = existing with
        {
            LastActivityAt = DateTimeOffset.UtcNow,
            LastProjectProfileId = projectProfileId ?? existing.LastProjectProfileId,
            LastPayload = payload ?? existing.LastPayload,
            TurnCount = existing.TurnCount + 1,
        };
        _sessions[sessionId] = updated;
        return updated;
    }
}
