using Kernel.Registry;
using Xunit;

namespace Kernel.Tests.Scenarios;

// End-to-end dogfooding for Story S1: exercises the Registry exclusively
// through its public contract (IRegistry / RegisterResult / QueryResult),
// the way the Epic's own Definition of Done #3 dogfoods the full Kernel
// once S2-S4 exist. This is S1's own slice of that walking skeleton.
public class RegistryDogfoodingScenarioTests
{
    [Fact]
    public void RegisterQueryDuplicateRejected_EndToEnd()
    {
        IRegistry registry = new InMemoryRegistry();

        var registered = registry.Register(new DefinitionSubmission("agent-42", "domain-agents"));
        Assert.Equal(RegisterOutcome.Success, registered.Outcome);

        var queried = registry.Query("agent-42");
        Assert.True(queried.Found);
        Assert.Equal("agent-42", queried.Entry!.Id);
        Assert.Equal("domain-agents", queried.Entry.Domain);
        Assert.Equal(RegistryEntryStatus.Active, queried.Entry.Status);

        var duplicate = registry.Register(new DefinitionSubmission("agent-42", "domain-other"));
        Assert.Equal(RegisterOutcome.RejectedDuplicate, duplicate.Outcome);

        var queriedAgain = registry.Query("agent-42");
        Assert.True(queriedAgain.Found);
        Assert.Equal("domain-agents", queriedAgain.Entry!.Domain);
    }
}
