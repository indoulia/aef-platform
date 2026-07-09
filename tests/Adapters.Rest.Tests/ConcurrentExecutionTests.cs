using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Runtime.Agent;
using Runtime.Agent.Sessions;

namespace Adapters.Rest.Tests;

// Closes Capability 1's concurrency audit "verified again under a real
// concurrent-load test" gate for real (GA-3 §8) - Capability 2's own
// Testing Plan was sequential-only, so this had never actually run
// through the real REST pipeline before. 20 is a small multiple of a
// typical dev machine's minimum thread-pool worker count, chosen to
// force genuine interleaving of the InMemoryEvidenceSink/
// InMemorySessionStore singletons under WebApplicationFactory's real
// concurrent dispatch - not a production load-test figure.
public class ConcurrentExecutionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int ConcurrentRequestCount = 20;

    private readonly WebApplicationFactory<Program> _factory;

    public ConcurrentExecutionTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ConcurrentRequests_WithoutSharedSession_AllSucceedWithUniqueRequestIds()
    {
        using var client = _factory.CreateClient();

        var tasks = Enumerable.Range(0, ConcurrentRequestCount)
            .Select(_ => client.PostAsJsonAsync("/api/v1/execution", new RestExecutionRequest(null, "start a new feature", "default-profile", null)))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<ExecutionResponse>()));
        Assert.All(bodies, b => Assert.Equal(ExecutionStatus.Completed, b!.Status));
        Assert.Equal(ConcurrentRequestCount, bodies.Select(b => b!.RequestId).Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentRequests_SharingOneSession_NoLostTurnCountUpdates()
    {
        using var client = _factory.CreateClient();
        var sessionResponse = await client.PostAsJsonAsync("/api/v1/sessions", new RestStartSessionRequest(null));
        var session = await sessionResponse.Content.ReadFromJsonAsync<ExecutionSession>();

        var tasks = Enumerable.Range(0, ConcurrentRequestCount)
            .Select(_ => client.PostAsJsonAsync("/api/v1/execution", new RestExecutionRequest(session!.SessionId, "start a new feature", "default-profile", null)))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // No GET /api/v1/sessions/{id} endpoint exists (Capability 2) - the
        // final TurnCount is observed directly from the factory's own DI
        // container, a test-only shortcut, not a claim this is reachable
        // over HTTP today.
        var sessionStore = _factory.Services.GetRequiredService<ISessionStore>();
        var finalSession = sessionStore.Get(session!.SessionId);

        Assert.NotNull(finalSession);
        Assert.Equal(ConcurrentRequestCount, finalSession!.TurnCount);
    }
}
