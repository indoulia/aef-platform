using Kernel.Governance;

namespace Kernel.Supervision;

// The Resource Manager admission-gate callback (AD-72): a pluggable, optional
// extension point the Kernel exposes now so no Kernel redesign is needed once
// the Resource Manager Service (Unit 9, Milestone M2) is built. Absent a
// registered gate, admission proceeds unconstrained, per AD-72.
public interface IResourceAdmissionGate
{
    AdmissionDecision CheckAdmission(ActivationRequest request);
}
