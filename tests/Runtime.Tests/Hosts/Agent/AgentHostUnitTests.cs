using Kernel.Bootstrapping;
using Kernel.Governance;
using Kernel.Registry;
using Runtime.Context;
using Runtime.Hosts.Agent;

namespace Runtime.Tests.Hosts.Agent;

// S6 — Agent Host (Construction Unit 12, I3 §12).
public class AgentHostUnitTests
{
    [Fact]
    public void Activate_WithClearedGovernance_CreatesInstanceWithFreshContext()
    {
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("agent-1", "domain-agents"));
        var host = new AgentHost(kernel.Supervisor, new ExecutionContextManager());

        var result = host.Activate(new ActivationRequest("agent-1"), MinimalSeed());

        Assert.Equal(AgentActivationOutcome.Created, result.Outcome);
        Assert.NotNull(result.Handle);
        Assert.Equal("profile-1", result.Handle!.Context.ProjectProfileId);
    }

    [Fact]
    public void Activate_Rejected_WhenNotRegistered()
    {
        var kernel = BootWithAllowAllPolicy();
        var host = new AgentHost(kernel.Supervisor, new ExecutionContextManager());

        var result = host.Activate(new ActivationRequest("agent-unregistered"), MinimalSeed());

        Assert.Equal(AgentActivationOutcome.Rejected, result.Outcome);
        Assert.Null(result.Handle);
    }

    [Fact]
    public void InvokeSkill_WithNoSkillInvokerConfigured_ReturnsCapabilityGap()
    {
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("agent-1", "domain-agents"));
        var host = new AgentHost(kernel.Supervisor, new ExecutionContextManager());
        var handle = host.Activate(new ActivationRequest("agent-1"), MinimalSeed()).Handle!;

        var result = handle.InvokeSkill("skill-1");

        Assert.Equal(SkillInvocationOutcome.CapabilityGap, result.Outcome);
    }

    [Fact]
    public void InvokeSkill_WithSkillInvokerConfigured_Delegates()
    {
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("agent-1", "domain-agents"));
        var host = new AgentHost(kernel.Supervisor, new ExecutionContextManager(), new EchoSkillInvoker());
        var handle = host.Activate(new ActivationRequest("agent-1"), MinimalSeed()).Handle!;

        var result = handle.InvokeSkill("skill-1");

        Assert.Equal(SkillInvocationOutcome.Invoked, result.Outcome);
        Assert.Equal("echo:skill-1", result.Output);
    }

    [Fact]
    public void Destroy_DestroysInstanceAndDisposesContext()
    {
        var kernel = BootWithAllowAllPolicy();
        kernel.Registry.Register(new DefinitionSubmission("agent-1", "domain-agents"));
        var contextManager = new ExecutionContextManager();
        var host = new AgentHost(kernel.Supervisor, contextManager);
        var handle = host.Activate(new ActivationRequest("agent-1"), MinimalSeed()).Handle!;

        host.Destroy(handle.InstanceId);

        Assert.False(kernel.Supervisor.IsActive(handle.InstanceId));
        Assert.Null(contextManager.GetContext(handle.InstanceId));
    }

    [Fact]
    public void AgentInstanceHandle_ExposesExactlyInvokeSkillAndSignalGap_NoAgentInvocationSurface()
    {
        var publicMethodNames = typeof(AgentInstanceHandle)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(new[] { "InvokeSkill", "SignalGap" }, publicMethodNames);
    }

    private static KernelHandle BootWithAllowAllPolicy() =>
        KernelBootstrapper.Boot(new KernelConfiguration(PolicyResolver: new AllowAllPolicyResolver()));

    private static ContextSeed MinimalSeed() =>
        new(new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "profile-1", CurrentStepId: null, Tier3SkillContent: null);

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class EchoSkillInvoker : ISkillInvoker
    {
        public SkillInvocationResult Invoke(RuntimeExecutionContext callerContext, string skillId) =>
            new(SkillInvocationOutcome.Invoked, $"echo:{skillId}", Reason: null);
    }
}
