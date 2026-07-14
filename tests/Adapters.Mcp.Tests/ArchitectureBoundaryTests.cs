using System.Xml.Linq;

namespace Adapters.Mcp.Tests;

// Enforces ADR-0025 (../ai-engineering-framework/docs/delivery/decisions/
// 0025-public-execution-interface-boundary.md) for Adapters.Mcp, mirroring
// Runtime.Agent.Tests/ArchitectureBoundaryTests.cs's own idiom: a channel
// adapter must reference Runtime.Agent only - never Runtime, Runtime.Ai, or
// Kernel. Epic M2-0001 Story S1 (scaffold only) - a failure here means a
// future change gave Adapters.Mcp a project reference this architecture
// forbids, caught here, not at review time.
public class ArchitectureBoundaryTests
{
    private static IReadOnlyList<string?> AdaptersMcpReferencedAssemblyNames() =>
        typeof(McpAdapterAssemblyReference).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    [Fact]
    public void AdaptersMcpAssembly_DoesNotReferenceKernel()
    {
        Assert.DoesNotContain("Kernel", AdaptersMcpReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersMcpAssembly_DoesNotReferenceRuntime()
    {
        Assert.DoesNotContain("Runtime", AdaptersMcpReferencedAssemblyNames());
    }

    [Fact]
    public void AdaptersMcpAssembly_DoesNotReferenceRuntimeAi()
    {
        Assert.DoesNotContain("Runtime.Ai", AdaptersMcpReferencedAssemblyNames());
    }

    // Mirrors HostExecutableStartupTests.LocateHostExecutablePath's repo-root
    // walk (tests/Host.Tests/HostExecutableStartupTests.cs) - the established
    // idiom in this suite for resolving a source file's on-disk path from a
    // test's own bin output directory.
    [Fact]
    public void AdaptersMcpProject_HasExactlyOneProjectReference_ToRuntimeAgent()
    {
        var csprojPath = LocateAdaptersMcpCsproj();
        var document = XDocument.Load(csprojPath);
        var references = document.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")!.Value)
            .ToList();

        var reference = Assert.Single(references);
        Assert.EndsWith("Runtime.Agent.csproj", reference.Replace('\\', '/'));
    }

    private static string LocateAdaptersMcpCsproj()
    {
        var testBinDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var repositoryRoot = testBinDirectory;
        while (repositoryRoot is not null && !File.Exists(Path.Combine(repositoryRoot.FullName, "AefPlatform.sln")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        if (repositoryRoot is null)
        {
            throw new InvalidOperationException("Could not locate AefPlatform.sln walking up from the test's base directory.");
        }

        var csprojPath = Path.Combine(repositoryRoot.FullName, "src", "Adapters.Mcp", "Adapters.Mcp.csproj");
        if (!File.Exists(csprojPath))
        {
            throw new FileNotFoundException($"Adapters.Mcp.csproj not found at '{csprojPath}'.", csprojPath);
        }

        return csprojPath;
    }
}
