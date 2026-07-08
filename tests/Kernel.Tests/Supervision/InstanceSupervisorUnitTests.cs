using Kernel.Evidence;
using Kernel.Governance;
using Kernel.Registry;
using Kernel.Supervision;
using Xunit;

namespace Kernel.Tests.Supervision;

public class InstanceSupervisorUnitTests
{
    private static IGovernanceBoundary AlwaysAuthorizingBoundary()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        return new GovernanceBoundary(registry, new AllowAllPolicyResolver());
    }

    [Fact]
    public void CreateInstance_ClearedActivation_CreatesRunningInstanceAndReturnsHandle()
    {
        var supervisor = new InstanceSupervisor(AlwaysAuthorizingBoundary());

        var result = supervisor.CreateInstance(new ActivationRequest("def-1"));

        Assert.Equal(InstanceCreationOutcome.Created, result.Outcome);
        Assert.NotNull(result.Handle);
        Assert.Equal("def-1", result.Handle!.DefinitionId);
        Assert.False(string.IsNullOrWhiteSpace(result.Handle.InstanceId));
    }

    [Fact]
    public void CreateInstance_ActivationNotCleared_NoInstanceCreatedUnderAnyCircumstance()
    {
        var boundary = new GovernanceBoundary(new InMemoryRegistry()); // unregistered definition -> rejected
        var supervisor = new InstanceSupervisor(boundary);

        var result = supervisor.CreateInstance(new ActivationRequest("does-not-exist"));

        Assert.Equal(InstanceCreationOutcome.RejectedGovernance, result.Outcome);
        Assert.Null(result.Handle);
    }

    [Fact]
    public void CreateInstance_NoResourceAdmissionGate_ProceedsUnconstrained()
    {
        var supervisor = new InstanceSupervisor(AlwaysAuthorizingBoundary(), resourceAdmissionGate: null);

        var result = supervisor.CreateInstance(new ActivationRequest("def-1"));

        Assert.Equal(InstanceCreationOutcome.Created, result.Outcome);
    }

    [Fact]
    public void CreateInstance_ResourceAdmissionGateDenies_InstanceCreationRefused()
    {
        var gate = new FakeResourceAdmissionGate(allow: false);
        var supervisor = new InstanceSupervisor(AlwaysAuthorizingBoundary(), gate);

        var result = supervisor.CreateInstance(new ActivationRequest("def-1"));

        Assert.Equal(InstanceCreationOutcome.RejectedInsufficientBudget, result.Outcome);
        Assert.Null(result.Handle);
    }

    [Fact]
    public void CreateInstance_ResourceAdmissionDenied_RecordsRejectDecisionOnEvidenceSink()
    {
        var sink = new FakeEvidenceSink();
        var gate = new FakeResourceAdmissionGate(allow: false);
        var supervisor = new InstanceSupervisor(AlwaysAuthorizingBoundary(), gate, sink);

        supervisor.CreateInstance(new ActivationRequest("def-1"));

        var record = Assert.Single(sink.Records);
        Assert.Equal("Supervision", record.Contract);
        Assert.Equal("Reject", record.Decision);
        Assert.NotNull(record.Reason);
    }

    [Fact]
    public void DestroyInstance_RunningInstance_TransitionsToDestroyedAndRemovedFromActiveTracking()
    {
        var supervisor = new InstanceSupervisor(AlwaysAuthorizingBoundary());
        var created = supervisor.CreateInstance(new ActivationRequest("def-1"));

        var destroyResult = supervisor.DestroyInstance(created.Handle!.InstanceId);

        Assert.Equal(DestroyOutcome.Destroyed, destroyResult.Outcome);
        Assert.False(supervisor.IsActive(created.Handle.InstanceId));
    }

    [Fact]
    public void DestroyInstance_UnknownId_ReturnsNotFound()
    {
        var supervisor = new InstanceSupervisor(AlwaysAuthorizingBoundary());

        var destroyResult = supervisor.DestroyInstance("does-not-exist");

        Assert.Equal(DestroyOutcome.NotFound, destroyResult.Outcome);
    }

    [Fact]
    public void CreateInstance_ManyConcurrentCallsForTheSameDefinition_EachGetsAUniqueTrackedInstance()
    {
        // Regression: a plain Dictionary indexer-write from concurrent
        // CreateInstance calls is a race against DestroyInstance/IsActive
        // readers - only observable under genuine concurrency.
        var supervisor = new InstanceSupervisor(AlwaysAuthorizingBoundary());
        const int count = 200;

        var handles = new InstanceHandle?[count];
        Parallel.For(0, count, i =>
        {
            handles[i] = supervisor.CreateInstance(new ActivationRequest("def-1")).Handle;
        });

        Assert.All(handles, h => Assert.NotNull(h));
        Assert.Equal(count, handles.Select(h => h!.InstanceId).Distinct().Count());
        Assert.All(handles, h => Assert.True(supervisor.IsActive(h!.InstanceId)));
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class FakeResourceAdmissionGate : IResourceAdmissionGate
    {
        private readonly bool _allow;

        public FakeResourceAdmissionGate(bool allow) => _allow = allow;

        public AdmissionDecision CheckAdmission(ActivationRequest request) =>
            _allow ? AdmissionDecision.Allow() : AdmissionDecision.Deny("insufficient budget");
    }

    private sealed class FakeEvidenceSink : IEvidenceSink
    {
        public List<EvidenceRecord> Records { get; } = new();

        public void Record(EvidenceRecord record) => Records.Add(record);
    }
}
