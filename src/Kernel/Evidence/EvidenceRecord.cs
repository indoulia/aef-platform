namespace Kernel.Evidence;

public sealed record EvidenceRecord(string Contract, string Decision, string SubjectId, string? Reason, DateTimeOffset Timestamp);
