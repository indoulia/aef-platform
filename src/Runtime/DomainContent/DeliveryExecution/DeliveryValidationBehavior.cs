using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.DeliveryExecution;

// The executable realization of SKL-EXE-0019
// (framework/execution/skills/delivery-validation.md)'s Behavior:
// re-run (or newly run) a captured verification procedure against
// Story S30's real Implementation output, per CEI-DEL-07's
// Self-Validation-on-Own-Output Practice. A failing run is still a
// Recorded outcome (Passed: false) - it is never hidden, downgraded, or
// silently retried away. Only the absence of any constructible
// verification procedure escalates.
public sealed class DeliveryValidationBehavior : IBehavior
{
    private const int MaxExcerptLength = 4000;

    private readonly ValidationRequest _request;
    private readonly IVerificationRunner _runner;

    public DeliveryValidationBehavior(ValidationRequest request, IVerificationRunner runner)
    {
        _request = request;
        _runner = runner;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (string.IsNullOrEmpty(_request.Command))
        {
            return ValidationOutcome.Escalated(
                "No verification procedure could be constructed for this change - halting and reporting rather than fabricating a pass, per CEI-DEL-07.");
        }

        var result = _runner.Run(_request.WorkingDirectory, _request.Command, _request.Arguments);

        var entry = new TestPlanAndResultsEntry(
            _request.StoryReferenceId,
            Passed: result.ExitCode == 0,
            result.ExitCode,
            Excerpt(result.StandardOutput),
            Excerpt(result.StandardError));

        return ValidationOutcome.Recorded(entry);
    }

    private static string Excerpt(string text) =>
        text.Length <= MaxExcerptLength ? text : text[..MaxExcerptLength] + "... (truncated)";
}
