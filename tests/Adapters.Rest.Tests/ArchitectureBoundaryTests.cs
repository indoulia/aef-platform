namespace Adapters.Rest.Tests;

public class ArchitectureBoundaryTests
{
    private static IReadOnlyList<string?> AdaptersRestReferencedAssemblyNames() =>
        typeof(RestExecutionRequest).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    [Fact]
    public void AdaptersRestAssembly_DoesNotReferenceRuntime()
    {
        Assert.DoesNotContain("Runtime", AdaptersRestReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersRestAssembly_DoesNotReferenceRuntimeAi()
    {
        Assert.DoesNotContain("Runtime.Ai", AdaptersRestReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersRestAssembly_DoesNotReferenceKernel()
    {
        Assert.DoesNotContain("Kernel", AdaptersRestReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersRestAssembly_ReferencesRuntimeAgent()
    {
        Assert.Contains("Runtime.Agent", AdaptersRestReferencedAssemblyNames());
    }
}
