namespace Runtime.Execution;

public enum EngineeringExecutionState
{
    NotStarted,
    Routing,
    Dispatching,
    Completed,
    Failed,
    NeedsClarification
}

// The session/conversation-level context the pipeline threads through one
// request's execution. Deliberately distinct from Runtime.Context.
// RuntimeExecutionContext (the existing, narrower, per-Kernel-Instance
// four-tier concept) - this is a new type, not a redefinition of that one.
public sealed class EngineeringExecutionContext
{
    public required Runtime.DomainContent.ConversationManagement.ConversationContext Conversation { get; init; }
    public required EngineeringRequest Request { get; init; }
    public string? SelectedWorkflowId { get; set; }
    public string? SelectedAgentId { get; set; }
    public EngineeringExecutionState State { get; set; } = EngineeringExecutionState.NotStarted;
    public List<object> Outputs { get; } = new();
    public List<string> Diagnostics { get; } = new();
}
