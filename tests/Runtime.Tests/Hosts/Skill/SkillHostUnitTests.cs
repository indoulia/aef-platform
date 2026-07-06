using Kernel.Bootstrapping;
using Kernel.Registry;
using Runtime.Context;
using Runtime.Hosts.Agent;
using Runtime.Hosts.Skill;

namespace Runtime.Tests.Hosts.Skill;

// S7 — Skill Host (Construction Unit 13, I3 §13).
public class SkillHostUnitTests
{
    [Fact]
    public void Invoke_RegisteredSkillWithBoundBehavior_ExecutesAndReturnsOutput()
    {
        var kernel = KernelBootstrapper.Boot();
        kernel.Registry.Register(new DefinitionSubmission("skill-1", "domain-skills"));
        var host = new SkillHost(kernel.Registry, new Dictionary<string, IBehavior> { ["skill-1"] = new EchoBehavior() });

        var result = host.Invoke(MinimalContext(), "skill-1");

        Assert.Equal(SkillInvocationOutcome.Invoked, result.Outcome);
        Assert.Equal("echo:instance-1", result.Output);
    }

    [Fact]
    public void Invoke_UnregisteredSkill_ReturnsCapabilityGap()
    {
        var kernel = KernelBootstrapper.Boot();
        var host = new SkillHost(kernel.Registry, new Dictionary<string, IBehavior>());

        var result = host.Invoke(MinimalContext(), "skill-unregistered");

        Assert.Equal(SkillInvocationOutcome.CapabilityGap, result.Outcome);
    }

    [Fact]
    public void Invoke_RegisteredSkillWithNoBoundBehavior_ReturnsCapabilityGap()
    {
        var kernel = KernelBootstrapper.Boot();
        kernel.Registry.Register(new DefinitionSubmission("skill-1", "domain-skills"));
        var host = new SkillHost(kernel.Registry, new Dictionary<string, IBehavior>());

        var result = host.Invoke(MinimalContext(), "skill-1");

        Assert.Equal(SkillInvocationOutcome.CapabilityGap, result.Outcome);
    }

    [Fact]
    public void Invoke_DoesNotCreateAnyDurableInstance()
    {
        var kernel = KernelBootstrapper.Boot();
        kernel.Registry.Register(new DefinitionSubmission("skill-1", "domain-skills"));
        var host = new SkillHost(kernel.Registry, new Dictionary<string, IBehavior> { ["skill-1"] = new EchoBehavior() });

        host.Invoke(MinimalContext(), "skill-1");

        // A Skill invocation is synchronous/non-pausable (I1 §8 footnote) — the
        // Kernel's Instance Supervisor is never touched, so nothing is ever tracked as active.
        Assert.False(kernel.Supervisor.IsActive("skill-1"));
    }

    private static RuntimeExecutionContext MinimalContext() =>
        new("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "profile-1", null, new Dictionary<string, object>());

    private sealed class EchoBehavior : IBehavior
    {
        public object? Execute(RuntimeExecutionContext context) => $"echo:{context.InstanceId}";
    }
}
