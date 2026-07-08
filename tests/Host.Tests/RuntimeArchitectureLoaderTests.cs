using AefPlatform.Host;

namespace Host.Tests;

public class EngineeringArchitectureManifestTests
{
    [Fact]
    public void KnownArtifacts_ContainsNoDuplicateIds()
    {
        var ids = EngineeringArchitectureManifest.KnownArtifacts.Select(a => a.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void KnownArtifacts_EveryEntryHasNonEmptyIdAndDomain()
    {
        Assert.All(EngineeringArchitectureManifest.KnownArtifacts, artifact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(artifact.Id));
            Assert.False(string.IsNullOrWhiteSpace(artifact.Domain));
        });
    }

    [Fact]
    public void KnownArtifacts_IncludesEveryEpicAlreadyProvenRegistrableInThisRepo()
    {
        var ids = EngineeringArchitectureManifest.KnownArtifacts.Select(a => a.Id).ToHashSet();

        // D9FeatureLifecycleContentRegistrationTests / FeatureLifecycleDogfoodingScenarioTests
        Assert.Contains("SKL-EXE-0004", ids);
        Assert.Contains("SKL-EXE-0005", ids);
        Assert.Contains("AGT-EXE-0002", ids);
        Assert.Contains("ORC-EXE-0001", ids);
        Assert.Contains("WFL-DEL-0001", ids);
        // NextEligibleItemDiscoveryRegistrationTests
        Assert.Contains("SKL-EXE-0012", ids);
        Assert.Contains("AGT-EXE-0005", ids);
        // EpicLifecycleContentRegistrationTests
        Assert.Contains("SKL-EXE-0013", ids);
        Assert.Contains("SKL-EXE-0014", ids);
        Assert.Contains("AGT-EXE-0006", ids);
        Assert.Contains("WFL-DEL-0004", ids);
        // RepositoryAssessmentContentRegistrationTests
        Assert.Contains("SKL-EXE-0015", ids);
        Assert.Contains("AGT-EXE-0007", ids);
        // ProjectRegistrationContentRegistrationTests
        Assert.Contains("SKL-EXE-0016", ids);
        Assert.Contains("AGT-EXE-0008", ids);
        // BaselineQualityGateContentRegistrationTests
        Assert.Contains("SKL-EXE-0017", ids);
        Assert.Contains("AGT-EXE-0009", ids);
    }
}
