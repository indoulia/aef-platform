using Kernel.Evidence;
using Kernel.Registry;

namespace Kernel.Governance;

public sealed class GovernanceBoundary : IGovernanceBoundary
{
    private readonly IRegistry _registry;
    private readonly IPolicyResolver? _policyResolver;
    private readonly IEvidenceSink _evidenceSink;

    public GovernanceBoundary(IRegistry registry, IPolicyResolver? policyResolver = null, IEvidenceSink? evidenceSink = null)
    {
        _registry = registry;
        _policyResolver = policyResolver;
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
    }

    public ActivationResult CheckActivation(ActivationRequest request)
    {
        var query = _registry.Query(request.DefinitionId);

        if (!query.Found)
        {
            const string reason = "Definition is not Registered.";
            RecordDecision("Reject", request.DefinitionId, reason);
            return ActivationResult.Rejected(ActivationOutcome.RejectedNotRegistered, reason);
        }

        if (query.Entry!.Status != RegistryEntryStatus.Active)
        {
            const string reason = "Definition is Registered but not Active.";
            RecordDecision("Reject", request.DefinitionId, reason);
            return ActivationResult.Rejected(ActivationOutcome.RejectedNotActive, reason);
        }

        if (_policyResolver is null)
        {
            const string reason = "No Policy-resolution callback is registered — failing closed.";
            RecordDecision("Reject", request.DefinitionId, reason);
            return ActivationResult.Rejected(ActivationOutcome.RejectedNoPolicyResolver, reason);
        }

        var resolution = _policyResolver.Resolve(request);
        if (!resolution.Authorized)
        {
            var reason = resolution.Reason ?? "Policy-resolution callback denied the request.";
            RecordDecision("Reject", request.DefinitionId, reason);
            return ActivationResult.Rejected(ActivationOutcome.RejectedPolicyDenied, reason);
        }

        RecordDecision("Accept", request.DefinitionId, reason: null);
        return ActivationResult.Authorized();
    }

    private void RecordDecision(string decision, string subjectId, string? reason)
    {
        _evidenceSink.Record(new EvidenceRecord(
            Contract: "Activation",
            Decision: decision,
            SubjectId: subjectId,
            Reason: reason,
            Timestamp: DateTimeOffset.UtcNow));
    }
}
