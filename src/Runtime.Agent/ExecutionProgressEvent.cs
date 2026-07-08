namespace Runtime.Agent;

public sealed record ExecutionProgressEvent(string RequestId, string Stage, string Message, DateTimeOffset Timestamp);
