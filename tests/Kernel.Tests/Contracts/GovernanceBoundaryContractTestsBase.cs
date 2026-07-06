using Kernel.Governance;
using Kernel.Registry;
using Xunit;

namespace Kernel.Tests.Contracts;

// Storage/policy-independent contract test suite for the Activation Contract:
// expressed purely against IGovernanceBoundary, IRegistry, and IPolicyResolver —
// never against GovernanceBoundary's own internals.
public abstract class GovernanceBoundaryContractTestsBase
{
    protected abstract IGovernanceBoundary CreateBoundary(IRegistry registry, IPolicyResolver? policyResolver);

    [Fact]
    public void UnregisteredDefinition_Rejected()
    {
        var boundary = CreateBoundary(new InMemoryRegistry(), policyResolver: null);

        var result = boundary.CheckActivation(new ActivationRequest("missing"));

        Assert.NotEqual(ActivationOutcome.Authorized, result.Outcome);
    }

    [Fact]
    public void RegisteredDefinition_NoPolicyResolver_FailsClosed()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        var boundary = CreateBoundary(registry, policyResolver: null);

        var result = boundary.CheckActivation(new ActivationRequest("def-1"));

        Assert.Equal(ActivationOutcome.RejectedNoPolicyResolver, result.Outcome);
    }

    [Fact]
    public void RegisteredDefinition_PolicyAuthorizes_Passes()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        var boundary = CreateBoundary(registry, new AlwaysAuthorizePolicyResolver());

        var result = boundary.CheckActivation(new ActivationRequest("def-1"));

        Assert.Equal(ActivationOutcome.Authorized, result.Outcome);
    }

    private sealed class AlwaysAuthorizePolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
