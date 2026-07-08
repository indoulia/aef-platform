using Runtime.Agent.Capabilities;

namespace Runtime.Agent;

public interface IAgentExecutionInterface
{
    ExecutionResponse Execute(ExecutionRequest request);
    IReadOnlyList<CapabilityDescriptor> DiscoverCapabilities();
    ExecutionSession StartSession(string? projectProfileId);
}
