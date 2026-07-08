using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.DeliveryExecution;

// Same per-invocation-binding adapter pattern as DeliveryImplementationSkillBehavior,
// for SKL-EXE-0020.
public sealed class DeliveryDocumentationSkillBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("DocumentationRequest", out var raw) || raw is not DocumentationRequest request)
        {
            return DocumentationOutcome.Escalated("No DocumentationRequest was supplied in the execution context.");
        }

        return new DeliveryDocumentationBehavior(request).Execute(context);
    }
}
