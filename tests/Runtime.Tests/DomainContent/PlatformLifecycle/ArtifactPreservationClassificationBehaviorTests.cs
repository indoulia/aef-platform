using Runtime.Context;
using Runtime.DomainContent.PlatformLifecycle;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.PlatformLifecycle;

public class ArtifactPreservationClassificationBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(ArtifactPreservationClassificationRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["ArtifactPreservationClassificationRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "PlatformLifecycle-Reset", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_ClassifiesGoverned_ConservativeDefault()
    {
        var behavior = new ArtifactPreservationClassificationBehavior();

        var result = (ArtifactPreservationClassificationResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("Governed", result.Classification);
    }

    [Fact]
    public void Execute_RuntimeCatalogScope_ClassifiesDerivedProjection()
    {
        var behavior = new ArtifactPreservationClassificationBehavior();

        var result = (ArtifactPreservationClassificationResult)behavior.Execute(
            ContextWith(new ArtifactPreservationClassificationRequest("the Runtime Catalog")))!;

        Assert.Equal("Derived Projection", result.Classification);
    }

    [Theory]
    [InlineData("an ADR")]
    [InlineData("a Skill definition")]
    [InlineData("everything")]
    public void Execute_AnyOtherScope_ClassifiesGoverned_NeverInventsALargerDerivedCategory(string scope)
    {
        var behavior = new ArtifactPreservationClassificationBehavior();

        var result = (ArtifactPreservationClassificationResult)behavior.Execute(
            ContextWith(new ArtifactPreservationClassificationRequest(scope)))!;

        Assert.Equal("Governed", result.Classification);
    }
}
