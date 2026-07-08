namespace Runtime.Agent.Tests;

public class ExecutionContractsTests
{
    [Fact]
    public void ExecutionRequest_PayloadAndProgressObserverDefaultToNull()
    {
        var request = new ExecutionRequest("req-1", null, "do the thing", "default-profile", null);

        Assert.Null(request.SessionId);
        Assert.Null(request.Payload);
        Assert.Null(request.ProgressObserver);
    }

    [Fact]
    public void ExecutionResponse_CarriesEvidenceAndMetadataVerbatim()
    {
        var evidence = new ExecutionEvidence(new[] { "diag" }, null, null, null, null);
        var metadata = new ExecutionMetadata("WFL-DEL-0001", "AGT-EXE-0002", TimeSpan.FromSeconds(1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var response = new ExecutionResponse("req-1", "session-1", ExecutionStatus.Completed, null, "done", "no further action", evidence, metadata);

        Assert.Equal(ExecutionStatus.Completed, response.Status);
        Assert.Same(evidence, response.Evidence);
        Assert.Same(metadata, response.Metadata);
    }

    [Fact]
    public void ExecutionSession_TurnCountAndPayloadAreExplicit()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ExecutionSession("session-1", now, now, "default-profile", null, 0);

        Assert.Equal(0, session.TurnCount);
        Assert.Null(session.LastPayload);
    }

    [Fact]
    public void ExecutionProgressEvent_CarriesStageAndTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var progressEvent = new ExecutionProgressEvent("req-1", "Routing", "Routing request.", timestamp);

        Assert.Equal("Routing", progressEvent.Stage);
        Assert.Equal(timestamp, progressEvent.Timestamp);
    }
}
