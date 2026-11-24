using System.Text.Json;
using Runtime.Agent;

namespace Adapters.Mcp.Tests;

// Epic M2-0001 Story S4: unit-level coverage of the pure translation logic
// (CallToolRequestParams.Arguments -> ExecutionRequest, ExecutionResponse ->
// CallToolResult), independent of the subprocess-level integration test in
// Mcp.Tests - the same test-pyramid split McpToolCatalogTests already uses
// for Story S3's own translation layer.
public class McpToolInvokerTests
{
    private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public void BuildExecutionRequest_UsesFixedRoutingLiteral_AsUserRequest()
    {
        var request = McpToolInvoker.BuildExecutionRequest("aef_plan_engineering", new Dictionary<string, JsonElement>
        {
            ["RepositoryName"] = Json("Ferret"),
            ["RepositoryRoot"] = Json(@"C:\POC\Ferret"),
            ["Goal"] = Json("Plan the next increment."),
        });

        Assert.Equal("plan", request.UserRequest);
    }

    [Fact]
    public void BuildExecutionRequest_TranscribesArguments_IntoPayload_ViaPayloadNormalization()
    {
        var request = McpToolInvoker.BuildExecutionRequest("aef_plan_engineering", new Dictionary<string, JsonElement>
        {
            ["RepositoryName"] = Json("Ferret"),
            ["RepositoryRoot"] = Json(@"C:\POC\Ferret"),
        });

        Assert.NotNull(request.Payload);
        Assert.Equal("Ferret", request.Payload!["RepositoryName"]);
        Assert.Equal(@"C:\POC\Ferret", request.Payload["RepositoryRoot"]);
    }

    [Fact]
    public void BuildExecutionRequest_ExtractsSessionIdFromArguments_NotIntoPayload()
    {
        var request = McpToolInvoker.BuildExecutionRequest("aef_analyze_repository", new Dictionary<string, JsonElement>
        {
            ["RepositoryName"] = Json("Ferret"),
            ["RepositoryRoot"] = Json(@"C:\POC\Ferret"),
            ["SessionId"] = Json("session-123"),
        });

        Assert.Equal("session-123", request.SessionId);
        Assert.False(request.Payload!.ContainsKey("SessionId"));
    }

    [Fact]
    public void BuildExecutionRequest_ExtractsProjectProfileIdFromArguments_NotIntoPayload()
    {
        var request = McpToolInvoker.BuildExecutionRequest("aef_analyze_repository", new Dictionary<string, JsonElement>
        {
            ["RepositoryName"] = Json("Ferret"),
            ["RepositoryRoot"] = Json(@"C:\POC\Ferret"),
            ["ProjectProfileId"] = Json("PPR-PRJ-0001"),
        });

        Assert.Equal("PPR-PRJ-0001", request.ProjectProfileId);
        Assert.False(request.Payload!.ContainsKey("ProjectProfileId"));
    }

    [Fact]
    public void BuildExecutionRequest_DefaultsProjectProfileIdToEmpty_WhenNotSupplied()
    {
        var request = McpToolInvoker.BuildExecutionRequest("aef_analyze_repository", new Dictionary<string, JsonElement>
        {
            ["RepositoryName"] = Json("Ferret"),
            ["RepositoryRoot"] = Json(@"C:\POC\Ferret"),
        });

        Assert.Equal(string.Empty, request.ProjectProfileId);
    }

    [Theory]
    [InlineData("aef_start_feature_lifecycle", "feature")]
    [InlineData("aef_start_epic_lifecycle", "epic")]
    [InlineData("aef_plan_engineering", "plan")]
    [InlineData("aef_analyze_repository", "analyze")]
    [InlineData("aef_validate_repository", "validate")]
    [InlineData("aef_refine_prompt", "refine prompt")]
    [InlineData("aef_check_change", "check change")]
    [InlineData("aef_plan_migration", "migrate")]
    [InlineData("aef_plan_maintenance", "maintenance")]
    [InlineData("aef_manage_platform_lifecycle", "platform lifecycle")]
    public void BuildExecutionRequest_EveryInScopeTool_HasItsApprovedRoutingLiteral(string toolName, string expectedLiteral)
    {
        var request = McpToolInvoker.BuildExecutionRequest(toolName, arguments: null);

        Assert.Equal(expectedLiteral, request.UserRequest);
    }

    [Theory]
    [InlineData("aef_deliver_implementation")]
    [InlineData("aef_review_specification")]
    [InlineData("aef_validate_implementation")]
    [InlineData("not_a_real_tool")]
    public void BuildExecutionRequest_ThrowsForDeferredOrUnknownTools(string toolName)
    {
        Assert.Throws<InvalidOperationException>(() => McpToolInvoker.BuildExecutionRequest(toolName, arguments: null));
    }

    [Fact]
    public void BuildCallToolResult_MapsFailedStatus_ToIsErrorTrue()
    {
        var response = new ExecutionResponse(
            "req-1", null, ExecutionStatus.Failed,
            new ExecutionError("Something went wrong.", Array.Empty<string>()),
            null, null,
            new ExecutionEvidence(Array.Empty<string>(), null, null, null, null),
            new ExecutionMetadata(null, null, TimeSpan.Zero, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var result = McpToolInvoker.BuildCallToolResult(response);

        Assert.True(result.IsError);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("Something went wrong.", text.Text);
    }

    [Theory]
    [InlineData(ExecutionStatus.Completed)]
    [InlineData(ExecutionStatus.NeedsClarification)]
    public void BuildCallToolResult_MapsNonFailedStatuses_ToIsErrorFalse(ExecutionStatus status)
    {
        var response = new ExecutionResponse(
            "req-1", null, status, null,
            "Here is the result.", "Next step.",
            new ExecutionEvidence(Array.Empty<string>(), null, null, null, null),
            new ExecutionMetadata(null, null, TimeSpan.Zero, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var result = McpToolInvoker.BuildCallToolResult(response);

        Assert.False(result.IsError);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("Here is the result.", text.Text);
    }

    [Fact]
    public void BuildExecutionRequest_RefinePrompt_UsesFixedLiteral_AndKeepsUserPromptInPayload()
    {
        var arguments = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["UserPrompt"] = System.Text.Json.JsonSerializer.SerializeToElement("plan the epic feature migration"),
            ["RepositoryName"] = System.Text.Json.JsonSerializer.SerializeToElement("repo"),
            ["RepositoryRoot"] = System.Text.Json.JsonSerializer.SerializeToElement("C:/my repo"),
        };

        var request = McpToolInvoker.BuildExecutionRequest("aef_refine_prompt", arguments);

        Assert.Equal("refine prompt", request.UserRequest);
        Assert.Equal("plan the epic feature migration", request.Payload!["UserPrompt"]);
        Assert.Equal("C:/my repo", request.Payload["RepositoryRoot"]);
    }

    private static ExecutionResponse Response(ExecutionStatus status, string? presented, ExecutionEvidence evidence, ExecutionError? error = null) =>
        new("req-1", null, status, error, presented, null, evidence,
            new ExecutionMetadata(null, null, TimeSpan.Zero, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    private static string TextOf(ModelContextProtocol.Protocol.CallToolResult result) =>
        Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;

    [Fact]
    public void BuildCallToolResult_WithDocument_ReturnsTheDocumentOnly()
    {
        var evidence = new ExecutionEvidence(new[] { "an open question" }, new[] { "a check" }, null, null, null, Document: "# Brief\nbody");

        var result = McpToolInvoker.BuildCallToolResult(Response(ExecutionStatus.Completed, "Completed via PROMPT-GROUNDING.", evidence));

        Assert.Equal("# Brief\nbody", TextOf(result)); // the handoff line adds nothing when there is a document
    }

    [Fact]
    public void BuildCallToolResult_WithoutDocument_AppendsDiagnosticsAndValidationResults()
    {
        var evidence = new ExecutionEvidence(new[] { "[Pass] Test Convention: found" }, new[] { "answer" }, null, null, null);

        var result = McpToolInvoker.BuildCallToolResult(Response(ExecutionStatus.Completed, "Completed via WFL-DEL-0008.", evidence));

        Assert.Equal("Completed via WFL-DEL-0008.\n\nFindings:\n- [Pass] Test Convention: found\n\nResults:\n- answer", TextOf(result));
    }

    [Fact]
    public void BuildCallToolResult_Failed_ReturnsReasonThenDiagnostics_IsErrorTrue()
    {
        var evidence = new ExecutionEvidence(new[] { "npm is not installed" }, null, null, null, null);

        var result = McpToolInvoker.BuildCallToolResult(Response(ExecutionStatus.Failed, null, evidence, new ExecutionError("Execution failed.", Array.Empty<string>())));

        Assert.True(result.IsError);
        Assert.Equal("Execution failed.\n\nFindings:\n- npm is not installed", TextOf(result));
    }
}
