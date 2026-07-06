using Kernel.Bootstrapping;
using Kernel.Governance;
using Kernel.Registry;
using Kernel.Supervision;
using Xunit;

namespace Kernel.Tests.Scenarios;

// Epic 0001's own Definition of Done #3, in full: Register -> Activate ->
// confirm Running -> Destroy -> Reboot -> confirm clean state, exclusively
// through the Kernel's public contracts, using stub callbacks for
// Policy-resolution (S2), Resource Manager admission (AD-72), and the
// Evidence-sink (the default, per this Epic's own ADR). This is the Epic's
// own internal walking skeleton, proving Units 1-3 cohere, with Bootstrapping
// (S4) as the entry point.
public class KernelWalkingSkeletonScenarioTests
{
    [Fact]
    public void RegisterActivateRunningDestroyRebootCleanState_EndToEnd()
    {
        var configuration = new KernelConfiguration(
            PolicyResolver: new AllowAllPolicyResolver(),
            ResourceAdmissionGate: new AllowAllResourceAdmissionGate());
        var handle = KernelBootstrapper.Boot(configuration);

        // Register
        var registered = handle.Registry.Register(new DefinitionSubmission("agent-42", "domain-agents"));
        Assert.Equal(RegisterOutcome.Success, registered.Outcome);

        // Activate -> confirm Running
        var created = handle.Supervisor.CreateInstance(new ActivationRequest("agent-42"));
        Assert.Equal(InstanceCreationOutcome.Created, created.Outcome);
        Assert.True(handle.Supervisor.IsActive(created.Handle!.InstanceId));

        // Destroy
        var destroyed = handle.Supervisor.DestroyInstance(created.Handle.InstanceId);
        Assert.Equal(DestroyOutcome.Destroyed, destroyed.Outcome);
        Assert.False(handle.Supervisor.IsActive(created.Handle.InstanceId));

        // Reboot -> confirm clean state (no persistent store configured)
        var rebooted = KernelBootstrapper.Boot(configuration);
        Assert.False(rebooted.Registry.Query("agent-42").Found);
        Assert.False(rebooted.Supervisor.IsActive(created.Handle.InstanceId));
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class AllowAllResourceAdmissionGate : IResourceAdmissionGate
    {
        public AdmissionDecision CheckAdmission(ActivationRequest request) => AdmissionDecision.Allow();
    }
}
