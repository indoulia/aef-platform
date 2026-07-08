using AefPlatform.Host;
using Kernel.Registry;
using Runtime.Bootstrapping;

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

public class RuntimeArchitectureLoaderTests
{
    [Fact]
    public void Load_AllArtifactsRegisterCleanly_ReportsFullSuccessAndIsValid()
    {
        var runtime = RuntimeBootstrapper.Boot();
        var artifacts = new[]
        {
            new ArchitectureArtifact("art-1", "Execution"),
            new ArchitectureArtifact("art-2", "Delivery"),
        };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.True(report.IsValid);
        Assert.Equal(2, report.Attempted);
        Assert.Equal(2, report.Succeeded);
        Assert.Empty(report.Failures);
        Assert.True(runtime.Kernel.Registry.Query("art-1").Found);
        Assert.True(runtime.Kernel.Registry.Query("art-2").Found);
    }

    [Fact]
    public void Load_DuplicateIdWithinManifest_ReportsFailureAndRegistersOnlyTheFirst()
    {
        var runtime = RuntimeBootstrapper.Boot();
        var artifacts = new[]
        {
            new ArchitectureArtifact("art-1", "Execution"),
            new ArchitectureArtifact("art-1", "Delivery"),
        };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.False(report.IsValid);
        Assert.Equal(2, report.Attempted);
        Assert.Equal(1, report.Succeeded);
        Assert.Single(report.Failures);
        Assert.Contains("art-1", report.Failures[0]);
        Assert.Equal("Execution", runtime.Kernel.Registry.Query("art-1").Entry!.Domain);
    }

    [Fact]
    public void Load_IdAlreadyRegisteredBeforeLoadRuns_ReportsFailureViaRejectedDuplicate()
    {
        var runtime = RuntimeBootstrapper.Boot();
        runtime.Kernel.Registry.Register(new DefinitionSubmission("art-1", "Execution"));
        var artifacts = new[] { new ArchitectureArtifact("art-1", "Execution") };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.False(report.IsValid);
        Assert.Equal(1, report.Attempted);
        Assert.Equal(0, report.Succeeded);
        Assert.Single(report.Failures);
    }

    [Fact]
    public void Load_MalformedArtifact_ReportsFailure()
    {
        var runtime = RuntimeBootstrapper.Boot();
        var artifacts = new[] { new ArchitectureArtifact("", "Execution") };

        var report = RuntimeArchitectureLoader.Load(runtime, artifacts);

        Assert.False(report.IsValid);
        Assert.Single(report.Failures);
    }

    [Fact]
    public void Load_TheRealManifest_RegistersEveryKnownArtifactCleanly()
    {
        var runtime = RuntimeBootstrapper.Boot();

        var report = RuntimeArchitectureLoader.Load(runtime, EngineeringArchitectureManifest.KnownArtifacts);

        Assert.True(report.IsValid);
        Assert.Equal(EngineeringArchitectureManifest.KnownArtifacts.Count, report.Succeeded);
    }
}
