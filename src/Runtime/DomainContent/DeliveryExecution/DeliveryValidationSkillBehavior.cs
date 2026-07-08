using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.DeliveryExecution;

// Same per-invocation-binding adapter pattern as DeliveryImplementationSkillBehavior,
// for SKL-EXE-0019.
public sealed class DeliveryValidationSkillBehavior : IBehavior
{
    private readonly IVerificationRunner _runner;

    public DeliveryValidationSkillBehavior(IVerificationRunner? runner = null)
    {
        _runner = runner ?? new ProcessVerificationRunner();
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ValidationRequest", out var raw) || raw is not ValidationRequest request)
        {
            return ValidationOutcome.Escalated("No ValidationRequest was supplied in the execution context.");
        }

        return new DeliveryValidationBehavior(request, _runner).Execute(context);
    }
}
