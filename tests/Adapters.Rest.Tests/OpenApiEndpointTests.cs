using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Adapters.Rest.Tests;

public class OpenApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetSwaggerJson_ReturnsOkValidJson()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("paths", out _));
    }
}
