using Kernel.Governance;
using Kernel.Registry;
using Xunit;

namespace Kernel.Tests.Scenarios;

// End-to-end dogfooding for Story S2: Register (S1) -> Activate (S2), through
// public contracts only, chaining the two Kernel modules built so far.
public class GovernanceBoundaryDogfoodingScenarioTests
{
    [Fact]
    public void RegisterThenActivate_EndToEnd()
    {
        IRegistry registry = new InMemoryRegistry();
        IGovernanceBoundary boundary = new GovernanceBoundary(registry);

        var unauthorized = boundary.CheckActivation(new ActivationRequest("agent-42"));
        Assert.Equal(ActivationOutcome.RejectedNotRegistered, unauthorized.Outcome);

        registry.Register(new DefinitionSubmission("agent-42", "domain-agents"));

        var noPolicy = boundary.CheckActivation(new ActivationRequest("agent-42"));
        Assert.Equal(ActivationOutcome.RejectedNoPolicyResolver, noPolicy.Outcome);

        IGovernanceBoundary boundaryWithPolicy = new GovernanceBoundary(registry, new AllowAllPolicyResolver());
        var authorized = boundaryWithPolicy.CheckActivation(new ActivationRequest("agent-42"));
        Assert.Equal(ActivationOutcome.Authorized, authorized.Outcome);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
