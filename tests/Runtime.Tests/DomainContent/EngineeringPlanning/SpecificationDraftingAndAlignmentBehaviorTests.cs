using Runtime.Context;
using Runtime.DomainContent.EngineeringPlanning;

namespace Runtime.Tests.DomainContent.EngineeringPlanning;

public class SpecificationDraftingAndAlignmentBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(DraftingRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["DraftingRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Planning-Drafting", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_ReturnsEmptyDraftWithSingleOpenItem()
    {
        var behavior = new SpecificationDraftingAndAlignmentBehavior();

        var result = (DraftingResult)behavior.Execute(ContextWith(null))!;

        Assert.Single(result.OpenItems);
        Assert.Equal("Standard", result.SpecificationDraft.SpecificationType);
    }

    [Fact]
    public void Execute_RealRequestWithCapabilityAndGap_PopulatesExistingCapabilityAndOpenItems()
    {
        var findings = new DiscoveryFindings(new[] { "dotnet (marker file(s) found at: repository root)" }, new[] { "No README found." });
        var behavior = new SpecificationDraftingAndAlignmentBehavior();

        var result = (DraftingResult)behavior.Execute(ContextWith(new DraftingRequest("Add a new API endpoint", findings)))!;

        Assert.Contains("dotnet", result.SpecificationDraft.ExistingCapabilityAnalysis);
        Assert.Contains("No README found.", result.OpenItems);
        Assert.Equal(4, result.OpenItems.Count); // 1 gap + 3 fixed human-input items
        Assert.Equal("Add a new API endpoint", result.SpecificationDraft.GoalProblemStatement);
        Assert.Empty(result.SpecificationDraft.Assumptions);
    }
}
