using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Runtime.Agent;

namespace Adapters.Rest.Tests;

// Regression test for an execution-discovered adapter translation defect
// (GA-3 Capability 5): REST's [FromBody] model binding deserializes
// payload values as JsonElement, which EngineeringExecutionPipeline's
// `is string name` pattern match never matched - RepositoryName/
// RepositoryRoot silently looked "missing" on every real request. Fixed
// via PayloadNormalization.Normalize (Runtime.Agent). Uses a portable,
// self-created temp directory rather than the external C:\POC\Ferret
// path used for live dogfooding, so this test is safe to run in CI.
public class RepositoryContextPayloadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RepositoryContextPayloadTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostExecution_PayloadWithRepositoryContext_IsRecognizedNotMissing()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("aef-rest-repo-context-test-");
        try
        {
            using var client = _factory.CreateClient();
            var payload = new Dictionary<string, object> { ["RepositoryName"] = "temp-repo", ["RepositoryRoot"] = tempDirectory.FullName };
            var request = new RestExecutionRequest(null, "analyze this repository", "default-profile", payload);

            var response = await client.PostAsJsonAsync("/api/v1/execution", request);
            var body = await response.Content.ReadFromJsonAsync<ExecutionResponse>();

            Assert.DoesNotContain(body!.Evidence.Diagnostics, d => d.Contains("No repository context was supplied"));
            Assert.Equal("temp-repo", body.Evidence.RepositoryAnalyzed);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
