using Runtime.Context;
using Runtime.Hosts.Agent;

namespace Runtime.Hosts.Strategy;

// The abstraction the Strategy Host's own exposed "allocate-a-step-to-an-Agent"
// call (I3 §14) delegates to. The Agent Host (S6) is this abstraction's real
// implementation, adapted at the point both Hosts are wired together (S10);
// until one is configured, an allocation is rejected — the Strategy Host
// never reaches into the Agent Host's internals directly.
public interface IAgentAllocator
{
    AgentActivationResult AllocateStep(string agentDefinitionId, ContextSeed seed);
}
