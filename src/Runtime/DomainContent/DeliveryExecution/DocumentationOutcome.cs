namespace Runtime.DomainContent.DeliveryExecution;

public sealed record DocumentationLogEntry(
    string StoryReferenceId,
    string EndUserContent,
    string MaintainerContent,
    bool RequiresHumanRead);

// Mirrors ImplementationOutcome/ValidationOutcome's closed shape: a
// completed run either produces a real, structurally-separated
// Documentation Log entry, or escalates when no content was supplied to
// draft from at all - it never fabricates documentation for a change
// with no described content.
public sealed record DocumentationOutcome
{
    public DocumentationLogEntry? LogEntry { get; }
    public string? EscalationReason { get; }

    private DocumentationOutcome(DocumentationLogEntry? logEntry, string? escalationReason)
    {
        LogEntry = logEntry;
        EscalationReason = escalationReason;
    }

    public static DocumentationOutcome Recorded(DocumentationLogEntry logEntry) => new(logEntry, null);

    public static DocumentationOutcome Escalated(string reason) => new(null, reason);

    public bool IsEscalated => EscalationReason is not null;
}
