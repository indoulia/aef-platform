using Kernel.Registry;
using Xunit;

namespace Kernel.Tests.Contracts;

// Storage-independent contract test suite (epic S1 AC4): expressed purely
// against IRegistry, never against a concrete implementation's internals.
// Any storage backend that implements IRegistry and passes this suite
// satisfies the Discovery read-contract's content-category obligation
// (Id, Domain, status), regardless of how it physically stores entries.
public abstract class RegistryContractTestsBase
{
    protected abstract IRegistry CreateRegistry();

    [Fact]
    public void Register_WellFormedSubmission_StoresAsActiveAndReturnsSuccess()
    {
        var registry = CreateRegistry();

        var result = registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        Assert.Equal(RegisterOutcome.Success, result.Outcome);
        Assert.Equal("def-1", result.Entry!.Id);
        Assert.Equal("domain-a", result.Entry.Domain);
        Assert.Equal(RegistryEntryStatus.Active, result.Entry.Status);
    }

    [Fact]
    public void Query_PreviouslyRegisteredId_ReturnsIdDomainAndStatus()
    {
        var registry = CreateRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        var result = registry.Query("def-1");

        Assert.True(result.Found);
        Assert.Equal("def-1", result.Entry!.Id);
        Assert.Equal("domain-a", result.Entry.Domain);
        Assert.Equal(RegistryEntryStatus.Active, result.Entry.Status);
    }

    [Fact]
    public void Query_UnknownId_ReturnsNotFound()
    {
        var registry = CreateRegistry();

        var result = registry.Query("does-not-exist");

        Assert.False(result.Found);
    }

    [Fact]
    public void Register_DuplicateId_DeterministicallyRejectedWithoutOverwrite()
    {
        var registry = CreateRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        var second = registry.Register(new DefinitionSubmission("def-1", "domain-b"));

        Assert.Equal(RegisterOutcome.RejectedDuplicate, second.Outcome);
        Assert.Equal("domain-a", registry.Query("def-1").Entry!.Domain);
    }
}
