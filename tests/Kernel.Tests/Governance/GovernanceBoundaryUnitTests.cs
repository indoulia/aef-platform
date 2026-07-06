using Kernel.Evidence;
using Kernel.Governance;
using Kernel.Registry;
using Xunit;

namespace Kernel.Tests.Governance;

public class GovernanceBoundaryUnitTests
{
    private static IRegistry RegistryWithActiveEntry(string id = "def-1", string domain = "domain-a")
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission(id, domain));
        return registry;
    }

    [Fact]
    public void CheckActivation_UnregisteredDefinition_RejectedNotRegistered()
    {
        var boundary = new GovernanceBoundary(new InMemoryRegistry());

        var result = boundary.CheckActivation(new ActivationRequest("does-not-exist"));

        Assert.Equal(ActivationOutcome.RejectedNotRegistered, result.Outcome);
    }

    [Fact]
    public void CheckActivation_RegisteredActiveDefinition_NoPolicyResolver_RejectedNoPolicyResolver()
    {
        var boundary = new GovernanceBoundary(RegistryWithActiveEntry());

        var result = boundary.CheckActivation(new ActivationRequest("def-1"));

        Assert.Equal(ActivationOutcome.RejectedNoPolicyResolver, result.Outcome);
    }

    [Fact]
    public void CheckActivation_PolicyResolverAuthorizes_Authorized()
    {
        var resolver = new FakePolicyResolver(authorized: true);
        var boundary = new GovernanceBoundary(RegistryWithActiveEntry(), resolver);

        var result = boundary.CheckActivation(new ActivationRequest("def-1"));

        Assert.Equal(ActivationOutcome.Authorized, result.Outcome);
    }

    [Fact]
    public void CheckActivation_PolicyResolverDenies_RejectedPolicyDenied()
    {
        var resolver = new FakePolicyResolver(authorized: false);
        var boundary = new GovernanceBoundary(RegistryWithActiveEntry(), resolver);

        var result = boundary.CheckActivation(new ActivationRequest("def-1"));

        Assert.Equal(ActivationOutcome.RejectedPolicyDenied, result.Outcome);
    }

    [Fact]
    public void CheckActivation_Success_RecordsAcceptDecisionOnEvidenceSink()
    {
        var sink = new FakeEvidenceSink();
        var resolver = new FakePolicyResolver(authorized: true);
        var boundary = new GovernanceBoundary(RegistryWithActiveEntry(), resolver, sink);

        boundary.CheckActivation(new ActivationRequest("def-1"));

        var record = Assert.Single(sink.Records);
        Assert.Equal("Activation", record.Contract);
        Assert.Equal("Accept", record.Decision);
        Assert.Equal("def-1", record.SubjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckActivation_EveryRejectionPath_RecordsRejectDecisionOnEvidenceSink(bool registerDefinition)
    {
        var sink = new FakeEvidenceSink();
        var registry = registerDefinition ? RegistryWithActiveEntry() : new InMemoryRegistry();
        var boundary = new GovernanceBoundary(registry, evidenceSink: sink);

        boundary.CheckActivation(new ActivationRequest("def-1"));

        var record = Assert.Single(sink.Records);
        Assert.Equal("Activation", record.Contract);
        Assert.Equal("Reject", record.Decision);
        Assert.NotNull(record.Reason);
    }

    private sealed class FakePolicyResolver : IPolicyResolver
    {
        private readonly bool _authorized;

        public FakePolicyResolver(bool authorized) => _authorized = authorized;

        public PolicyResolution Resolve(ActivationRequest request) =>
            _authorized ? PolicyResolution.Authorize() : PolicyResolution.Deny("policy denied");
    }

    private sealed class FakeEvidenceSink : IEvidenceSink
    {
        public List<EvidenceRecord> Records { get; } = new();

        public void Record(EvidenceRecord record) => Records.Add(record);
    }
}
