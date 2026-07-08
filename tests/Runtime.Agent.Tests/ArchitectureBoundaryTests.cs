namespace Runtime.Agent.Tests;

// Enforces ADR-0025 (../ai-engineering-framework/epics/decisions/
// 0025-public-execution-interface-boundary.md) as an automated guarantee:
// Runtime.Agent must never acquire a reference to Kernel or Runtime.Ai. A
// failure here means a future change gave Runtime.Agent a project
// reference this architecture forbids - caught here, not at review time.
// A "references Runtime" positive-control assertion is added in Task 6
// (AgentExecutionServiceTests), once code in this assembly actually
// consumes a Runtime.Execution type - the compiler only emits an
// AssemblyRef for a ProjectReference that is genuinely used, so no such
// assertion can hold truthfully yet with only ExecutionStatus present.
public class ArchitectureBoundaryTests
{
    private static IReadOnlyList<string?> RuntimeAgentReferencedAssemblyNames() =>
        typeof(ExecutionStatus).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

    [Fact]
    public void RuntimeAgentAssembly_DoesNotReferenceKernel()
    {
        Assert.DoesNotContain("Kernel", RuntimeAgentReferencedAssemblyNames());
    }

    [Fact]
    public void RuntimeAgentAssembly_DoesNotReferenceRuntimeAi()
    {
        Assert.DoesNotContain("Runtime.Ai", RuntimeAgentReferencedAssemblyNames());
    }
}
