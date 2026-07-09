using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Adapters.Rest.Tests;

// GA-3 §5's Contract Compatibility row requires a real contract test, not
// just the /v1 URL segment. Verified against the real, live-serialized
// JSON (not assumptions) - see docs/superpowers/specs/2026-07-08-ga3-
// capability5-operational-readiness-design.md Decision 1. Property-name
// presence AND JsonValueKind per property, closing the type-change gap
// the design's own review flagged - not a full JSON-schema validator.
public class ContractCompatibilityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContractCompatibilityTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ExecutionResponse_Contract_HasExpectedShapeAndTypes()
    {
        using var client = _factory.CreateClient();
        var request = new RestExecutionRequest(null, "start a new feature", "default-profile", null);

        var response = await client.PostAsJsonAsync("/api/v1/execution", request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertProperty(root, "requestId", JsonValueKind.String);
        AssertProperty(root, "sessionId", JsonValueKind.Null);
        AssertProperty(root, "status", JsonValueKind.Number);
        AssertProperty(root, "error", JsonValueKind.Null);
        AssertProperty(root, "presentedResult", JsonValueKind.String);
        AssertProperty(root, "suggestedNextStep", JsonValueKind.String);
        AssertProperty(root, "evidence", JsonValueKind.Object);
        AssertProperty(root, "metadata", JsonValueKind.Object);

        var evidence = root.GetProperty("evidence");
        AssertProperty(evidence, "diagnostics", JsonValueKind.Array);

        var metadata = root.GetProperty("metadata");
        AssertProperty(metadata, "executedCapabilityId", JsonValueKind.String);
        AssertProperty(metadata, "duration", JsonValueKind.String);
        AssertProperty(metadata, "startedAt", JsonValueKind.String);
        AssertProperty(metadata, "completedAt", JsonValueKind.String);
    }

    [Fact]
    public async Task CapabilityDescriptor_Contract_HasExpectedShapeAndTypes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/capabilities");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = document.RootElement[0];

        AssertProperty(first, "capabilityId", JsonValueKind.String);
        AssertProperty(first, "displayName", JsonValueKind.String);
        AssertProperty(first, "description", JsonValueKind.String);
        AssertProperty(first, "requiredPayloadKeys", JsonValueKind.Array);
        AssertProperty(first, "executionMode", JsonValueKind.String);
    }

    [Fact]
    public async Task ExecutionSession_Contract_HasExpectedShapeAndTypes()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/sessions", new RestStartSessionRequest(null));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertProperty(root, "sessionId", JsonValueKind.String);
        AssertProperty(root, "createdAt", JsonValueKind.String);
        AssertProperty(root, "lastActivityAt", JsonValueKind.String);
        AssertProperty(root, "lastProjectProfileId", JsonValueKind.Null);
        AssertProperty(root, "lastPayload", JsonValueKind.Null);
        AssertProperty(root, "turnCount", JsonValueKind.Number);
    }

    private static void AssertProperty(JsonElement element, string propertyName, JsonValueKind expectedKind)
    {
        Assert.True(element.TryGetProperty(propertyName, out var property), $"Expected property '{propertyName}' was not found.");
        Assert.Equal(expectedKind, property.ValueKind);
    }
}
