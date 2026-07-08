using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runtime.Agent.Health;

namespace Adapters.Rest.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private sealed class FakeContributor : IRuntimeHealthContributor
    {
        private readonly RuntimeHealthReport _report;
        public FakeContributor(RuntimeHealthReport report) => _report = report;
        public RuntimeHealthReport Check() => _report;
    }

    // RemoveAll is required, not optional: IEnumerable<IRuntimeHealthContributor>
    // resolves every registration, so a plain AddSingleton would run
    // Program.cs's real, network-calling AiProviderReachabilityHealthContributor
    // *alongside* the fake, reintroducing exactly the internet dependency
    // this test suite must not have.
    private WebApplicationFactory<Program> FactoryWithContributor(RuntimeHealthReport report) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRuntimeHealthContributor>();
                services.AddSingleton<IRuntimeHealthContributor>(new FakeContributor(report));
            });
        });

    [Fact]
    public async Task GetHealth_AllContributorsHealthy_ReturnsOk()
    {
        using var factory = FactoryWithContributor(new RuntimeHealthReport("Fake", RuntimeHealthStatus.Healthy, null));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestHealthResponse>();
        Assert.Equal("Healthy", body!.Status);
    }

    [Fact]
    public async Task GetHealth_AContributorIsUnhealthy_ReturnsServiceUnavailable()
    {
        using var factory = FactoryWithContributor(new RuntimeHealthReport("Fake", RuntimeHealthStatus.Unhealthy, "Simulated failure."));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestHealthResponse>();
        Assert.Equal("Unhealthy", body!.Status);
    }

    [Fact]
    public async Task GetHealth_ADegradedContributor_StillReturnsOk()
    {
        using var factory = FactoryWithContributor(new RuntimeHealthReport("Fake", RuntimeHealthStatus.Degraded, "Simulated degradation."));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestHealthResponse>();
        Assert.Equal("Degraded", body!.Status);
    }
}
