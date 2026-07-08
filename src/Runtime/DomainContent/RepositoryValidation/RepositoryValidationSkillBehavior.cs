using Runtime.Context;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.RepositoryValidation;

// P3 §6 (Validation Pipeline): derives the Command/Arguments from the
// RepositoryExecutionContext's own detected Technology Profile via
// BuildSystemDetector, then delegates to the existing, unmodified
// DeliveryValidationBehavior/IVerificationRunner - the exact same reuse
// pattern the Delivery Execution SkillHost adapters (P2 Task 6) already
// established for per-invocation request binding.
public sealed class RepositoryValidationSkillBehavior : IBehavior
{
    private readonly IVerificationRunner _runner;

    public RepositoryValidationSkillBehavior(IVerificationRunner? runner = null)
    {
        _runner = runner ?? new ProcessVerificationRunner();
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("RepositoryContext", out var raw) || raw is not RepositoryExecutionContext repositoryContext)
        {
            return ValidationOutcome.Escalated("No RepositoryExecutionContext was supplied - cannot construct a verification procedure.");
        }

        var stackNames = repositoryContext.TechnologyProfile?.Technologies.Select(t => t.Name).ToList() ?? new List<string>();
        var command = BuildSystemDetector.DetectDefaultValidationCommand(stackNames);

        if (command is null)
        {
            return ValidationOutcome.Escalated($"No known default build/test command for repository '{repositoryContext.RepositoryName}''s detected stack - halting rather than guessing one.");
        }

        var request = new ValidationRequest(repositoryContext.RepositoryName, repositoryContext.RepositoryRoot, command.Value.Command, command.Value.Arguments);
        return new DeliveryValidationBehavior(request, _runner).Execute(context);
    }
}
