using Kernel.Bootstrapping;
using Runtime.Catalog;
using Runtime.Context;
using Runtime.Hosts.Agent;
using Runtime.Hosts.Skill;
using Runtime.Hosts.Strategy;

namespace Runtime.Bootstrapping;

// Exposes the Kernel's three contracts (Epic 0001) plus this Epic's own
// five Construction Units (S5-S9) to a test harness, per this Story's own
// acceptance criterion.
public sealed record RuntimeHandle(
    KernelHandle Kernel,
    IExecutionContextManager ContextManager,
    AgentHost AgentHost,
    SkillHost SkillHost,
    StrategyHost StrategyHost,
    RuntimeCatalog Catalog);
