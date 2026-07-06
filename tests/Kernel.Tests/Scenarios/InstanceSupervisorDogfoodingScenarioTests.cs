using Kernel.Governance;
using Kernel.Registry;
using Kernel.Supervision;
using Xunit;

namespace Kernel.Tests.Scenarios;

// End-to-end dogfooding across all three Stories built so far: Register (S1)
// -> Activate (S2) -> Create -> Destroy (S3), through public contracts only —
// this Epic's own internal walking skeleton (Definition of Done #3), proving
// Units 1-3 cohere, using a stub Policy-resolution callback.
public class InstanceSupervisorDogfoodingScenarioTests
{
    [Fact]
    public void RegisterActivateCreateDestroy_EndToEnd()
    {
        IRegistry registry = new InMemoryRegistry();
        IGovernanceBoundary boundary = new GovernanceBoundary(registry, new AllowAllPolicyResolver());
        IInstanceSupervisor supervisor = new InstanceSupervisor(boundary);

        registry.Register(new DefinitionSubmission("agent-42", "domain-agents"));

        var created = supervisor.CreateInstance(new ActivationRequest("agent-42"));
        Assert.Equal(InstanceCreationOutcome.Created, created.Outcome);
        Assert.True(supervisor.IsActive(created.Handle!.InstanceId));

        var destroyed = supervisor.DestroyInstance(created.Handle.InstanceId);
        Assert.Equal(DestroyOutcome.Destroyed, destroyed.Outcome);
        Assert.False(supervisor.IsActive(created.Handle.InstanceId));
    }

    [Fact]
    public void UnregisteredDefinition_NeverReachesInstanceCreation()
    {
        IRegistry registry = new InMemoryRegistry();
        IGovernanceBoundary boundary = new GovernanceBoundary(registry, new AllowAllPolicyResolver());
        IInstanceSupervisor supervisor = new InstanceSupervisor(boundary);

        var result = supervisor.CreateInstance(new ActivationRequest("never-registered"));

        Assert.Equal(InstanceCreationOutcome.RejectedGovernance, result.Outcome);
        Assert.Null(result.Handle);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
