using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.DomainContent.DeliveryExecution;

// The executable realization of SKL-EXE-0018
// (framework/execution/skills/delivery-implementation.md)'s Behavior:
// given a claimed Implementation (branch + commit identifiers) for an
// approved Story's Plan, produce a real, independently-verified
// Implementation Log entry, or escalate rather than trust the caller's
// own summary - exceeding SKL-EXE-0009's tracking-only precedent, which
// accepts a caller-supplied implementation_summary as-is.
//
// Isolation (RUL-DEL-0004/CEI-DEL-08) is enforced structurally, not by
// caller discipline: this class holds only an IIsolatedChangeVerifier
// reference, whose own contract exposes no merge/push/checkout-capable
// member (LocalGitChangeVerifierTests.Contract_...) - so a completed run
// of this Behavior has no way to touch the primary integration line,
// regardless of what it decides, mirroring RepositoryAssessmentBehavior's
// own "holds no IRegistry reference" pattern (Onboarding/
// RepositoryAssessmentBehavior.cs) for a different capability.
public sealed class DeliveryImplementationBehavior : IBehavior
{
    private readonly ImplementationRequest _request;
    private readonly IIsolatedChangeVerifier _verifier;

    public DeliveryImplementationBehavior(ImplementationRequest request, IIsolatedChangeVerifier verifier)
    {
        _request = request;
        _verifier = verifier;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        if (_verifier.IsDefaultBranch(_request.BranchName))
        {
            return ImplementationOutcome.Escalated(
                $"Branch '{_request.BranchName}' is the repository's own default/primary integration line - an Implementation Skill run must operate on an isolated branch, never the primary line, per RUL-DEL-0004/CEI-DEL-08.");
        }

        if (!_verifier.BranchExists(_request.BranchName))
        {
            return ImplementationOutcome.Escalated(
                $"Branch '{_request.BranchName}' does not exist in the repository - cannot record an Implementation Log entry for a change that is not independently verifiable.");
        }

        if (_request.CommitShas.Count == 0)
        {
            return ImplementationOutcome.Escalated(
                "No commit identifiers were supplied - this Skill records real, independently-verified change Evidence, never a caller-supplied summary (unlike SKL-EXE-0009's implementation_summary input).");
        }

        var verifiedShas = new List<string>();
        foreach (var sha in _request.CommitShas)
        {
            if (!_verifier.CommitExistsOnBranch(_request.BranchName, sha))
            {
                return ImplementationOutcome.Escalated(
                    $"Commit '{sha}' is not verifiably reachable from branch '{_request.BranchName}' - cannot record it as real change Evidence without guessing.");
            }

            verifiedShas.Add(sha);
        }

        return ImplementationOutcome.Recorded(new ImplementationLogEntry(
            _request.StoryReferenceId,
            _request.BranchName,
            verifiedShas,
            _request.PlanSummary));
    }
}
