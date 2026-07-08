using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.DeliveryExecution;

// SkillHost binds exactly one IBehavior instance per skillId at Boot time
// (Runtime.Bootstrapping.RuntimeBootstrapper), but DeliveryImplementationBehavior
// is constructor-bound to one fixed ImplementationRequest - this adapter
// reads the real per-invocation request from context.Tier3SkillContent so
// SKL-EXE-0018 can be re-invoked through SkillHost.Invoke with a different
// request each call, without changing IBehavior, SkillHost, or
// DeliveryImplementationBehavior itself.
public sealed class DeliveryImplementationSkillBehavior : IBehavior
{
    private readonly Func<string, IIsolatedChangeVerifier> _verifierFactory;

    public DeliveryImplementationSkillBehavior(Func<string, IIsolatedChangeVerifier>? verifierFactory = null)
    {
        _verifierFactory = verifierFactory ?? (repositoryRoot => new LocalGitChangeVerifier(repositoryRoot, "main"));
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("ImplementationRequest", out var rawRequest) || rawRequest is not ImplementationRequest request)
        {
            return ImplementationOutcome.Escalated("No ImplementationRequest was supplied in the execution context.");
        }

        if (!context.Tier3SkillContent.TryGetValue("RepositoryRoot", out var rawRoot) || rawRoot is not string repositoryRoot)
        {
            return ImplementationOutcome.Escalated("No RepositoryRoot was supplied in the execution context.");
        }

        var verifier = _verifierFactory(repositoryRoot);
        return new DeliveryImplementationBehavior(request, verifier).Execute(context);
    }
}
