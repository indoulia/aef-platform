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
        // AddOrUpdate's updateValueFactory runs atomically against the
        // dictionary's current value (retried internally if another
        // thread wins a race), closing the read-then-write gap the
        // previous TryGetValue-then-index-write pair left open (GA-3
        // Capability 1 concurrency audit): two overlapping Touch calls on
        // the same session id could otherwise lose one call's TurnCount
        // increment. addValueFactory only runs for an unknown id,
        // preserving the original throw-on-unknown-session behavior.
        return _sessions.AddOrUpdate(
            sessionId,
            addValueFactory: _ => throw new KeyNotFoundException($"No session exists for id '{sessionId}'."),
            updateValueFactory: (_, existing) => existing with
            {
                LastActivityAt = DateTimeOffset.UtcNow,
                LastProjectProfileId = projectProfileId ?? existing.LastProjectProfileId,
                LastPayload = payload ?? existing.LastPayload,
                TurnCount = existing.TurnCount + 1,
            });
    }
}
