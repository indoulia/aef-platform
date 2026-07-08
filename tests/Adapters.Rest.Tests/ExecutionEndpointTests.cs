using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Runtime.Agent;
using Runtime.Agent.Security;

namespace Adapters.Rest.Tests;

public class ExecutionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ExecutionEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostExecution_RoutesToADeterministicCapability_ReturnsOkCompleted()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecutionResponse>();
        Assert.NotNull(body);
        Assert.Equal(ExecutionStatus.Completed, body!.Status);
    }

    [Fact]
    public async Task PostExecution_UnknownSessionId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest("does-not-exist", "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostExecution_EmptyUserRequest_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest(null, "", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostExecution_MalformedJson_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{ this is not valid json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/execution", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostExecution_UnsupportedMediaType_ReturnsUnsupportedMediaType()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("plain text body", System.Text.Encoding.UTF8, "text/plain");

        var response = await client.PostAsync("/api/v1/execution", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private sealed class SpyDenyAllSecurityEvaluator : ISecurityEvaluator
    {
        public int CallCount { get; private set; }

        public SecurityDecision Evaluate(SecurityContext context)
        {
            CallCount++;
            return new SecurityDecision(IsAllowed: false, DenialReason: "Denied by test double.");
        }
    }

    [Fact]
    public async Task PostExecution_SecurityDenies_ReturnsForbiddenAndNeverCallsRuntimeAgent()
    {
        var spyEvaluator = new SpyDenyAllSecurityEvaluator();
        using var factoryWithDenial = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISecurityEvaluator>(spyEvaluator);
            });
        });
        using var client = factoryWithDenial.CreateClient();
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, spyEvaluator.CallCount);
    }
}
