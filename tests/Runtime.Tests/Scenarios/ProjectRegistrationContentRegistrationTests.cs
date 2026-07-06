using Kernel.Registry;
using Runtime.Bootstrapping;
using Xunit;

namespace Runtime.Tests.Scenarios;

// S20 (epics/0006-repository-discovery-and-canonical-onboarding.md) -
// register SKL-EXE-0016/AGT-EXE-0008 against the real Runtime, mirroring
// the registration-proof shape every prior Story's content has used.
public class ProjectRegistrationContentRegistrationTests
{
    private static readonly (string Id, string Domain)[] Artifacts =
    {
        ("SKL-EXE-0016", "Execution"),
        ("AGT-EXE-0008", "Execution"),
    };

    [Fact]
    public void RegisterBothArtifacts_EachAcceptedAsActive_AndCatalogReflectsThem()
    {
        var runtime = RuntimeBootstrapper.Boot();

        foreach (var (id, domain) in Artifacts)
        {
            var registered = runtime.Kernel.Registry.Register(new DefinitionSubmission(id, domain));
            Assert.Equal(RegisterOutcome.Success, registered.Outcome);

            var catalogEntry = runtime.Catalog.Query(id);
            Assert.True(catalogEntry.Found);
            Assert.Equal(RegistryEntryStatus.Active, catalogEntry.Entry!.Status);
        }
    }
}
