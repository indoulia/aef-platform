using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Runtime.Agent;

namespace Adapters.Rest.Tests;

public class SessionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SessionsEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostSessions_ReturnsCreatedWithANonEmptySessionId()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sessions", new RestStartSessionRequest(null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecutionSession>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.SessionId));
    }
}
