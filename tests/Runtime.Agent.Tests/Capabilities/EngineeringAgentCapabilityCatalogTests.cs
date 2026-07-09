using Runtime.Agent.Capabilities;

namespace Runtime.Agent.Tests.Capabilities;

public class EngineeringAgentCapabilityCatalogTests
{
    private static readonly string[] KnownDispatchableIds =
    {
        "WFL-DEL-0001", "WFL-DEL-0004", "WFL-DEL-0005", "WFL-DEL-0006",
        "WFL-DEL-0007", "WFL-DEL-0008", "WFL-DEL-0009", "SKL-EXE-0018", "REPO-VALIDATION",
    };

    [Fact]
    public void Discover_ReturnsExactlyOneDescriptorPerKnownDispatchableId()
    {
        var catalog = new EngineeringAgentCapabilityCatalog();

        var descriptors = catalog.Discover();

        foreach (var id in KnownDispatchableIds)
        {
            var matches = descriptors.Where(d => d.CapabilityId == id).ToList();
            Assert.Single(matches);
            Assert.False(string.IsNullOrWhiteSpace(matches[0].Description));
            Assert.Equal("Synchronous", matches[0].ExecutionMode);
        }
        Assert.Equal(KnownDispatchableIds.Length, descriptors.Count);
    }

    [Fact]
    public void Discover_UnknownIdHasNoDescriptor()
    {
        var catalog = new EngineeringAgentCapabilityCatalog();

        var descriptors = catalog.Discover();

        Assert.DoesNotContain(descriptors, d => d.CapabilityId == "NOT-A-REAL-CAPABILITY");
    }
}
