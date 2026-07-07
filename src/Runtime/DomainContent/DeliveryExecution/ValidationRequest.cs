namespace Runtime.DomainContent.DeliveryExecution;

// What Story S30's real Implementation Log claims should be verified -
// inert input data. A null/empty Command means no verification procedure
// could be constructed for this change; the Behavior escalates rather
// than guessing one, per CEI-DEL-07.
public sealed record ValidationRequest(
    string StoryReferenceId,
    string WorkingDirectory,
    string? Command,
    IReadOnlyList<string> Arguments);
