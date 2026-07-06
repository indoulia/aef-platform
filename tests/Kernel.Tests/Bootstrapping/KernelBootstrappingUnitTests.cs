using Kernel.Bootstrapping;
using Kernel.Governance;
using Kernel.Registry;
using Kernel.Supervision;
using Xunit;

namespace Kernel.Tests.Bootstrapping;

public class KernelBootstrappingUnitTests
{
    [Fact]
    public void Boot_NoPriorState_AllThreeContractsReadyAndCallable()
    {
        var handle = KernelBootstrapper.Boot();

        Assert.NotNull(handle.Registry);
        Assert.NotNull(handle.GovernanceBoundary);
        Assert.NotNull(handle.Supervisor);

        // Callable end-to-end through the returned handle.
        var registered = handle.Registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        Assert.Equal(RegisterOutcome.Success, registered.Outcome);

        var activation = handle.GovernanceBoundary.CheckActivation(new ActivationRequest("def-1"));
        Assert.Equal(ActivationOutcome.RejectedNoPolicyResolver, activation.Outcome); // no resolver configured
    }

    [Fact]
    public void Boot_SharesOneEvidenceSinkAcrossAllThreeModules()
    {
        var evidenceSink = new Kernel.Evidence.InMemoryEvidenceSink();
        var handle = KernelBootstrapper.Boot(new KernelConfiguration(EvidenceSink: evidenceSink, PolicyResolver: new AllowAllPolicyResolver()));

        handle.Registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        handle.Supervisor.CreateInstance(new ActivationRequest("def-1")); // internally invokes GovernanceBoundary's Activation check

        // Registration (Accept) + Activation (Accept, from inside CreateInstance) + Supervision (Accept) all land on the one shared sink.
        Assert.Equal(3, evidenceSink.Records.Count);
    }

    [Fact]
    public void Reboot_NoPersistentStoreConfigured_RegistryStartsEmpty()
    {
        var firstBoot = KernelBootstrapper.Boot();
        firstBoot.Registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        var secondBoot = KernelBootstrapper.Boot(); // simulates a restart — a fresh Boot call, no persistence configured

        var queried = secondBoot.Registry.Query("def-1");
        Assert.False(queried.Found); // starts empty, never a partial/undefined state
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
