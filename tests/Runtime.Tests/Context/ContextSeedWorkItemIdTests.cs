using Runtime.Context;
using Xunit;

namespace Runtime.Tests.Context;

// Epic 0005 (ai-engineering-framework repo), Story S33: a minimal,
// additive extension of Agent-to-step work allocation to carry a
// Story-level work-item pointer - proven here as non-breaking (existing
// call sites omitting the new parameter still compile and default to
// null) and as a genuinely distinct value from CurrentStepId.
public sealed class ContextSeedWorkItemIdTests
{
    [Fact]
    public void ExistingCallSitesOmittingWorkItemId_StillCompileAndDefaultToNull()
    {
        var seed = new ContextSeed(new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "proj-1", "Implementation", null);

        Assert.Null(seed.WorkItemId);
    }

    [Fact]
    public void WorkItemIdIsDistinctFromCurrentStepId_BothCarriedThroughIndependently()
    {
        var seed = new ContextSeed(new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "proj-1", "Implementation", null, WorkItemId: "S30");

        Assert.Equal("Implementation", seed.CurrentStepId);
        Assert.Equal("S30", seed.WorkItemId);
    }
}
