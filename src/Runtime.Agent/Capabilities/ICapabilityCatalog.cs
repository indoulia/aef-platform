namespace Runtime.Agent.Capabilities;

public interface ICapabilityCatalog
{
    IReadOnlyList<CapabilityDescriptor> Discover();
}
