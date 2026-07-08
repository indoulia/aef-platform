namespace Adapters.Cli.Tests;

public class ArchitectureBoundaryTests
{
    private static IReadOnlyList<string?> AdaptersCliReferencedAssemblyNames() =>
        typeof(CliExecuteArguments).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    [Fact]
    public void AdaptersCliAssembly_DoesNotReferenceRuntime()
    {
        Assert.DoesNotContain("Runtime", AdaptersCliReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersCliAssembly_DoesNotReferenceRuntimeAi()
    {
        Assert.DoesNotContain("Runtime.Ai", AdaptersCliReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersCliAssembly_DoesNotReferenceKernel()
    {
        Assert.DoesNotContain("Kernel", AdaptersCliReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersCliAssembly_ReferencesRuntimeAgent()
    {
        Assert.Contains("Runtime.Agent", AdaptersCliReferencedAssemblyNames());
    }
}
