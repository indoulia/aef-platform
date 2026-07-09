using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Runtime.Agent.Capabilities;

namespace Adapters.Rest.Tests;

public class CapabilitiesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CapabilitiesEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetCapabilities_ReturnsTheHandAuthoredElevenCapabilities()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/capabilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<CapabilityDescriptor>>();
        Assert.NotNull(body);
        Assert.Equal(11, body!.Count);
    }
}
