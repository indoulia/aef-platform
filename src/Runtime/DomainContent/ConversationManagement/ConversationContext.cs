namespace Runtime.DomainContent.ConversationManagement;

// The "runtime conversation context" AGT-EXE-0011's own design doc
// (docs/superpowers/specs/2026-07-07-aef-assistant-conversation-manager-
// design.md) defines: host-supplied, not a governed artifact or Context
// type. The design doc deliberately declines to specify storage/
// persistence mechanics - this type carries exactly the named fields and
// nothing more.
public sealed record ConversationContext(
    string ConversationId,
    IReadOnlyList<string> AskedQuestions,
    IReadOnlyList<string> AnsweredQuestions,
    string? ActiveWorkflowId,
    string? RoadmapItemInFocus,
    bool PendingApproval,
    bool RepositoryContextAvailable)
{
    public static ConversationContext New(string conversationId) =>
        new(conversationId, Array.Empty<string>(), Array.Empty<string>(), null, null, false, false);
}
