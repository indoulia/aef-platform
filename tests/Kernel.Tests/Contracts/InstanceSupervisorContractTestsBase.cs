using Kernel.Governance;
using Kernel.Registry;
using Kernel.Supervision;
using Xunit;

namespace Kernel.Tests.Contracts;

// Contract test suite for the Instance Supervisor: expressed purely against
// IInstanceSupervisor and IGovernanceBoundary, never against InstanceSupervisor's
// own internals.
public abstract class InstanceSupervisorContractTestsBase
{
    protected abstract IInstanceSupervisor CreateSupervisor(IGovernanceBoundary governanceBoundary);

    private static IGovernanceBoundary AuthorizingBoundary()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        return new GovernanceBoundary(registry, new AllowAllPolicyResolver());
    }

    [Fact]
    public void ClearedActivation_CreatesRunningInstance()
    {
        var supervisor = CreateSupervisor(AuthorizingBoundary());

        var result = supervisor.CreateInstance(new ActivationRequest("def-1"));

        Assert.Equal(InstanceCreationOutcome.Created, result.Outcome);
        Assert.True(supervisor.IsActive(result.Handle!.InstanceId));
    }

    [Fact]
    public void UnclearedActivation_NoInstanceCreated()
    {
        var ungovernedBoundary = new GovernanceBoundary(new InMemoryRegistry());
        var supervisor = CreateSupervisor(ungovernedBoundary);

        var result = supervisor.CreateInstance(new ActivationRequest("missing"));

        Assert.NotEqual(InstanceCreationOutcome.Created, result.Outcome);
        Assert.Null(result.Handle);
    }

    [Fact]
    public void Destroy_RunningInstance_RemovedFromActiveTracking()
    {
        var supervisor = CreateSupervisor(AuthorizingBoundary());
        var created = supervisor.CreateInstance(new ActivationRequest("def-1"));

        var destroyResult = supervisor.DestroyInstance(created.Handle!.InstanceId);

        Assert.Equal(DestroyOutcome.Destroyed, destroyResult.Outcome);
        Assert.False(supervisor.IsActive(created.Handle.InstanceId));
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
