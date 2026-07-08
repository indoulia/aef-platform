using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Adapters.Rest.Tests;

public class VersionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VersionEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetVersion_ReturnsOkWithNonEmptyVersion()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestVersionResponse>();
        Assert.False(string.IsNullOrEmpty(body!.Version));
    }
}
