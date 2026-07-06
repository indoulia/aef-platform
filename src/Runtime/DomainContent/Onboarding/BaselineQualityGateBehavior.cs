using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.Onboarding;

// The executable realization of SKL-EXE-0017 (framework/execution/skills/
// baseline-quality-gate-and-exception-recording.md)'s Behavior: given a
// Registered project's baseline Quality Gate results, classify the run per
// the greenfield/existing divergence (§3) and record an exception for
// every gap that has a supplied rationale - or escalate when a gap has
// none, or the project is not yet Registered, never leaving a gap
// unaccounted for.
public sealed class BaselineQualityGateBehavior : IBehavior
{
    private readonly BaselineQualityGateRequest _request;

    public BaselineQualityGateBehavior(BaselineQualityGateRequest request) => _request = request;

    public object? Execute(RuntimeExecutionContext context)
    {
        if (_request.Registration.Status != RegistrationStatus.Registered)
        {
            return BaselineQualityGateOutcome.Escalated(
                "Cannot run the baseline Quality Gate set against a project that is not yet Registered.");
        }

        var failingGateIds = _request.GateResults.Where(g => !g.Passed).Select(g => g.GateId).ToList();
        var unaccountedGateIds = failingGateIds.Where(id => !_request.ExceptionRationales.ContainsKey(id)).ToList();
        if (unaccountedGateIds.Count > 0)
        {
            return BaselineQualityGateOutcome.Escalated(
                $"Gap(s) with no recorded exception rationale: {string.Join(", ", unaccountedGateIds)} - onboarding cannot leave a gap unaccounted for.");
        }

        var passingGateIds = _request.GateResults.Where(g => g.Passed).Select(g => g.GateId).ToList();
        var recordedExceptions = failingGateIds
            .Select(id => new ExceptionRecord(id, _request.ExceptionRationales[id]))
            .ToList();

        return BaselineQualityGateOutcome.Completed(_request.ProjectKind, passingGateIds, recordedExceptions);
    }
}
