using System.Collections.Concurrent;
using Kernel.Evidence;
using Kernel.Governance;

namespace Kernel.Supervision;

public sealed class InstanceSupervisor : IInstanceSupervisor
{
    private readonly IGovernanceBoundary _governanceBoundary;
    private readonly IResourceAdmissionGate? _resourceAdmissionGate;
    private readonly IEvidenceSink _evidenceSink;
    // ConcurrentDictionary: CreateInstance/DestroyInstance/IsActive run
    // concurrently for independently-supervised Agent/Skill instances in a
    // long-running host - a plain Dictionary has no such guarantee.
    private readonly ConcurrentDictionary<string, InstanceStatus> _activeInstances = new();

    public InstanceSupervisor(
        IGovernanceBoundary governanceBoundary,
        IResourceAdmissionGate? resourceAdmissionGate = null,
        IEvidenceSink? evidenceSink = null)
    {
        _governanceBoundary = governanceBoundary;
        _resourceAdmissionGate = resourceAdmissionGate;
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
    }

    public InstanceCreationResult CreateInstance(ActivationRequest request)
    {
        var activation = _governanceBoundary.CheckActivation(request);
        if (activation.Outcome != ActivationOutcome.Authorized)
        {
            // S2's own CheckActivation already recorded this Evidence decision —
            // no duplicate record is produced for the same underlying decision.
            return InstanceCreationResult.Rejected(
                InstanceCreationOutcome.RejectedGovernance,
                activation.Reason ?? "Activation did not clear the Governance Boundary.");
        }

        if (_resourceAdmissionGate is not null)
        {
            var admission = _resourceAdmissionGate.CheckAdmission(request);
            if (!admission.Allowed)
            {
                var reason = admission.Reason ?? "Resource admission denied.";
                RecordDecision("Reject", request.DefinitionId, reason);
                return InstanceCreationResult.Rejected(InstanceCreationOutcome.RejectedInsufficientBudget, reason);
            }
        }

        var instanceId = Guid.NewGuid().ToString("N");
        _activeInstances[instanceId] = InstanceStatus.Running;
        RecordDecision("Accept", request.DefinitionId, reason: null);

        return InstanceCreationResult.Created(new InstanceHandle(instanceId, request.DefinitionId));
    }

    public DestroyResult DestroyInstance(string instanceId)
    {
        return _activeInstances.TryRemove(instanceId, out _)
            ? new DestroyResult(DestroyOutcome.Destroyed)
            : new DestroyResult(DestroyOutcome.NotFound);
    }

    public bool IsActive(string instanceId) => _activeInstances.ContainsKey(instanceId);

    private void RecordDecision(string decision, string subjectId, string? reason)
    {
        _evidenceSink.Record(new EvidenceRecord(
            Contract: "Supervision",
            Decision: decision,
            SubjectId: subjectId,
            Reason: reason,
            Timestamp: DateTimeOffset.UtcNow));
    }
}
