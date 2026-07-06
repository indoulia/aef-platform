using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.Context;
using Runtime.Hosts.Skill;
using Runtime.Hosts.Strategy;
using Xunit;

namespace Runtime.Tests.Scenarios;

// This Epic's own Definition of Done #3, in full: this is the Walking
// Skeleton's own internal walking skeleton — I4 §12's M1 exit criterion —
// Register -> Activate the Strategy -> allocate a step to the Agent ->
// invoke the Skill -> Output/Evidence returned -> Session completes, using
// the trivial content epics/decisions/0004-walking-skeleton-trivial-content-selection.md
// fixes, exclusively through the Kernel's, Hosts', and Catalog's public
// contracts — no stub stands in for any of Units 4, 12-15 themselves.
public class RuntimeWalkingSkeletonScenarioTests
{
    private const string SkillId = "walking-skeleton-echo-skill";
    private const string AgentId = "walking-skeleton-echo-agent";
    private const string StrategyId = "walking-skeleton-echo-strategy";

    [Fact]
    public void RegisterActivateAllocateInvokeComplete_EndToEnd()
    {
        var runtime = RuntimeBootstrapper.Boot(
            new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()),
            behaviors: new Dictionary<string, IBehavior> { [SkillId] = new EchoBehavior() });

        // Register all three, per ADR-0004's fixed content identities.
        Assert.Equal(RegisterOutcome.Success, runtime.Kernel.Registry.Register(new DefinitionSubmission(SkillId, "domain-execution")).Outcome);
        Assert.Equal(RegisterOutcome.Success, runtime.Kernel.Registry.Register(new DefinitionSubmission(AgentId, "domain-execution")).Outcome);
        Assert.Equal(RegisterOutcome.Success, runtime.Kernel.Registry.Register(new DefinitionSubmission(StrategyId, "domain-execution")).Outcome);

        // The Catalog reflects the Registry's state, live, per S9.
        Assert.True(runtime.Catalog.Query(StrategyId).Found);

        // Activate the Strategy.
        var strategyActivation = runtime.StrategyHost.Activate(new ActivationRequest(StrategyId), MinimalSeed());
        Assert.Equal(StrategyActivationOutcome.Created, strategyActivation.Outcome);
        var strategy = strategyActivation.Handle!;

        // The Strategy allocates its one step to the Agent.
        var agentActivation = strategy.AllocateStep(AgentId, MinimalSeed());
        Assert.Equal(Runtime.Hosts.Agent.AgentActivationOutcome.Created, agentActivation.Outcome);
        var agent = agentActivation.Handle!;

        // The Agent invokes the Skill.
        var skillResult = agent.InvokeSkill(SkillId);
        Assert.Equal(Runtime.Hosts.Agent.SkillInvocationOutcome.Invoked, skillResult.Outcome);
        Assert.Equal("acknowledged", skillResult.Output);

        // The Strategy's Session reaches completion.
        Assert.True(strategy.Complete());
        Assert.Equal(SessionState.Completed, strategy.State);
    }

    private static ContextSeed MinimalSeed() =>
        new(new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "profile-1", CurrentStepId: null, Tier3SkillContent: null);

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class EchoBehavior : IBehavior
    {
        public object? Execute(RuntimeExecutionContext context) => "acknowledged";
    }
}
