using Kernel.Evidence;
using Kernel.Governance;
using Kernel.Registry;
using Kernel.Supervision;

namespace Kernel.Bootstrapping;

// The Kernel-only subset of I3 §10's boot sequence: load Kernel-relevant
// configuration, initialize S1-S3 in order, reach a Kernel-ready state
// exposing all three public contracts. No persistent store exists yet
// (I1 §21 defers the Registry's concrete data model) — each Boot call
// starts fresh, never a partial or undefined state.
public static class KernelBootstrapper
{
    public static KernelHandle Boot(KernelConfiguration? configuration = null)
    {
        configuration ??= new KernelConfiguration();
        var evidenceSink = configuration.EvidenceSink ?? new InMemoryEvidenceSink();

        var registry = new InMemoryRegistry(evidenceSink);
        var governanceBoundary = new GovernanceBoundary(registry, configuration.PolicyResolver, evidenceSink);
        var supervisor = new InstanceSupervisor(governanceBoundary, configuration.ResourceAdmissionGate, evidenceSink);

        return new KernelHandle(registry, governanceBoundary, supervisor);
    }
}
