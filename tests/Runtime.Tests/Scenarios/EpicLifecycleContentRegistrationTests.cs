using Kernel.Registry;
using Runtime.Bootstrapping;
using Xunit;

namespace Runtime.Tests.Scenarios;

// S17 (epics/0004-epic-granularity-autonomous-lifecycle.md) - register the
// new Epic-scoped governed content (SKL-EXE-0013, SKL-EXE-0014,
// AGT-EXE-0006, WFL-DEL-0004) against the real Runtime and confirm the
// Catalog reflects it, mirroring D9FeatureLifecycleContentRegistrationTests'
// own proof shape for D9's content.
public class EpicLifecycleContentRegistrationTests
{
    private static readonly (string Id, string Domain)[] EpicLifecycleArtifacts =
    {
        ("SKL-EXE-0013", "Execution"),
        ("SKL-EXE-0014", "Execution"),
        ("AGT-EXE-0006", "Execution"),
        ("WFL-DEL-0004", "Delivery"),
    };

    [Fact]
    public void RegisterAllFourArtifacts_EachAcceptedAsActive_AndCatalogReflectsThem()
    {
        var runtime = RuntimeBootstrapper.Boot();

        foreach (var (id, domain) in EpicLifecycleArtifacts)
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
}
