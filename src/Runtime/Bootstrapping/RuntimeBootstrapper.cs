using Kernel.Bootstrapping;
using Kernel.Evidence;
using Kernel.Governance;
using Runtime.Catalog;
using Runtime.Context;
using Runtime.Hosts.Agent;
using Runtime.Hosts.Skill;
using Runtime.Hosts.Strategy;

namespace Runtime.Bootstrapping;

// Extends Epic 0001's Kernel-only Bootstrapping (I3 §10 steps 1-2) through
// steps 3-5 for this Epic's one configured Runtime Service (the Execution
// Context Manager, S5): initialize it, wire the three Hosts (S6-S8) and the
// Catalog (S9) against the already-booted Kernel, open the Catalog, and
// reach Runtime Ready.
public static class RuntimeBootstrapper
{
    public static RuntimeHandle Boot(RuntimeConfiguration? configuration = null, IReadOnlyDictionary<string, IBehavior>? behaviors = null)
    {
        configuration ??= new RuntimeConfiguration();
        var evidenceSink = configuration.EvidenceSink ?? new InMemoryEvidenceSink();

        // I3 §10 steps 1-2: Kernel initialization, unchanged from Epic 0001.
        var kernel = KernelBootstrapper.Boot(new KernelConfiguration(
            configuration.PolicyResolver,
            configuration.ResourceAdmissionGate,
            evidenceSink));

        // I3 §10 step 3: initialize the one configured Runtime Service (Unit 4).
        var contextManager = new ExecutionContextManager(configuration.DisposalPolicy);

        var skillHost = new SkillHost(kernel.Registry, behaviors ?? new Dictionary<string, IBehavior>(), evidenceSink);
        var agentHost = new AgentHost(kernel.Supervisor, contextManager, skillHost, evidenceSink);
        var strategyHost = new StrategyHost(kernel.Supervisor, contextManager, new AgentHostAllocator(agentHost), evidenceSink);

        // I3 §10 step 4: open the Catalog once the Registry it projects exists.
        var catalog = new RuntimeCatalog(kernel.Registry, configuration.ConformanceProvider);

        // I3 §10 step 5: Runtime reaches Ready.
        return new RuntimeHandle(kernel, contextManager, agentHost, skillHost, strategyHost, catalog);
    }

    private sealed class AgentHostAllocator : IAgentAllocator
    {
        private readonly AgentHost _agentHost;

        public AgentHostAllocator(AgentHost agentHost) => _agentHost = agentHost;

        public AgentActivationResult AllocateStep(string agentDefinitionId, ContextSeed seed) =>
            _agentHost.Activate(new ActivationRequest(agentDefinitionId), seed);
    }
}
