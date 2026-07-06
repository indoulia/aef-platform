using Kernel.Registry;
using Runtime.Bootstrapping;
using Xunit;

namespace Runtime.Tests.Scenarios;

// S12 (epics/0003-feature-lifecycle-dogfooding.md) — Register D9's frozen,
// Active Feature-lifecycle content against the real Runtime for the first
// time in the platform's history (previously only synthetic
// walking-skeleton-echo-* fixtures had ever been Registered, per
// epics/decisions/0004-...md). Reuses the Kernel's Registration Contract
// (S1) and the Runtime Catalog (S9) exactly as built — no new Runtime
// capability is introduced by this Story.
public class D9FeatureLifecycleContentRegistrationTests
{
    // Ids and Domains taken verbatim from each artifact's own frontmatter:
    // framework/execution/skills/feature-intake-and-clarify.md,
    // feature-plan-and-roadmap.md; framework/execution/agents/
    // feature-lifecycle-agent.md, orchestrator-implementation.md;
    // framework/delivery/workflows/feature-lifecycle.md.
    private static readonly (string Id, string Domain)[] D9FeatureLifecycleArtifacts =
    {
        ("SKL-EXE-0004", "Execution"),
        ("SKL-EXE-0005", "Execution"),
        ("AGT-EXE-0002", "Execution"),
        ("ORC-EXE-0001", "Execution"),
        ("WFL-DEL-0001", "Delivery"),
    };

    [Fact]
    public void RegisterAllFiveArtifacts_EachAcceptedAsActive_AndCatalogReflectsThem()
    {
        var runtime = RuntimeBootstrapper.Boot();

        foreach (var (id, domain) in D9FeatureLifecycleArtifacts)
        {
            var registered = runtime.Kernel.Registry.Register(new DefinitionSubmission(id, domain));
            Assert.Equal(RegisterOutcome.Success, registered.Outcome);

            var catalogEntry = runtime.Catalog.Query(id);
            Assert.True(catalogEntry.Found);
            Assert.Equal(id, catalogEntry.Entry!.Id);
            Assert.Equal(domain, catalogEntry.Entry.Domain);
            Assert.Equal(RegistryEntryStatus.Active, catalogEntry.Entry.Status);
        }
    }

    [Fact]
    public void SecondRegistrationOfSameArtifact_IsDeterministicallyRejected()
    {
        var runtime = RuntimeBootstrapper.Boot();
        var (id, domain) = D9FeatureLifecycleArtifacts[0];
        runtime.Kernel.Registry.Register(new DefinitionSubmission(id, domain));

        var secondAttempt = runtime.Kernel.Registry.Register(new DefinitionSubmission(id, domain));

        Assert.Equal(RegisterOutcome.RejectedDuplicate, secondAttempt.Outcome);
    }
}
