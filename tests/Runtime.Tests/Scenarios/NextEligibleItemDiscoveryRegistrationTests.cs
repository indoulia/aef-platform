using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.DomainContent.EpicLifecycle;
using Runtime.Hosts.Agent;
using Runtime.Hosts.Skill;
using Xunit;

namespace Runtime.Tests.Scenarios;

// S16 (epics/0004-epic-granularity-autonomous-lifecycle.md) — register
// SKL-EXE-0012 and AGT-EXE-0005 against the real Runtime and prove the
// Skill is invocable through the Skill Host's public contract, exactly as
// D9FeatureLifecycleContentRegistrationTests did for D9's own content.
public class NextEligibleItemDiscoveryRegistrationTests
{
    private const string SkillId = "SKL-EXE-0012";
    private const string AgentId = "AGT-EXE-0005";

    [Fact]
    public void RegisterSkillAndAgent_EachAcceptedAsActive_AndCatalogReflectsThem()
    {
        var runtime = RuntimeBootstrapper.Boot();

        foreach (var (id, domain) in new[] { (SkillId, "Execution"), (AgentId, "Execution") })
        {
            var registered = runtime.Kernel.Registry.Register(new DefinitionSubmission(id, domain));
            Assert.Equal(RegisterOutcome.Success, registered.Outcome);

            var catalogEntry = runtime.Catalog.Query(id);
            Assert.True(catalogEntry.Found);
            Assert.Equal(RegistryEntryStatus.Active, catalogEntry.Entry!.Status);
        }
    }

    [Fact]
    public void InvokingTheSkillThroughTheSkillHost_ReturnsADiscoveryOutcome()
    {
        var dependencyId = "SOME-SATISFIED-DEPENDENCY";
        var candidates = new[] { new CandidateItem("CU-X", "Candidate X", dependencyId) };

        var runtime = RuntimeBootstrapper.Boot(
            new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()),
            behaviors: new Dictionary<string, IBehavior>());

        // Registered after Boot so the Behavior can close over the live Catalog.
        var behavior = new NextEligibleItemDiscoveryBehavior(runtime.Catalog, candidates);
        var skillHostWithBehavior = new SkillHost(runtime.Kernel.Registry, new Dictionary<string, IBehavior> { [SkillId] = behavior });

        runtime.Kernel.Registry.Register(new DefinitionSubmission(SkillId, "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission(dependencyId, "Execution"));

        var invocation = skillHostWithBehavior.Invoke(callerContext: null!, SkillId);

        Assert.Equal(SkillInvocationOutcome.Invoked, invocation.Outcome);
        var outcome = (DiscoveryOutcome)invocation.Output!;
        Assert.Equal(DiscoveryStatus.Eligible, outcome.Status);
        Assert.Equal("CU-X", outcome.Candidate!.Id);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }
}
