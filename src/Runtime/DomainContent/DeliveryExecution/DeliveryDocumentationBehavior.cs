using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.DeliveryExecution;

// The executable realization of SKL-EXE-0020
// (framework/execution/skills/delivery-documentation.md)'s Behavior:
// package the AI Agent's own drafted end-user and maintainer content into
// a structurally audience-separated Documentation Log entry, per
// CEI-DEL-06, honoring the project's own documentation-content human
// read-gate policy where one is declared (08-feature-lifecycle.md §3.7:
// "not a formal gate unless the project's Policy says so"). This
// Behavior never authors the prose itself - drafting is the invoking
// Agent's own generative work, the same reasoning that keeps
// SKL-EXE-0018 from writing the underlying code change itself.
public sealed class DeliveryDocumentationBehavior : IBehavior
{
    private readonly DocumentationRequest _request;

    public DeliveryDocumentationBehavior(DocumentationRequest request)
    {
        _request = request;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(_request.EndUserContent) && string.IsNullOrWhiteSpace(_request.MaintainerContent))
        {
            return DocumentationOutcome.Escalated(
                "No documentation content was supplied for either audience - cannot fabricate documentation for a change with no described content.");
        }

        return DocumentationOutcome.Recorded(new DocumentationLogEntry(
            _request.StoryReferenceId,
            _request.EndUserContent,
            _request.MaintainerContent,
            _request.ProjectRequiresHumanReadGate));
    }
}
