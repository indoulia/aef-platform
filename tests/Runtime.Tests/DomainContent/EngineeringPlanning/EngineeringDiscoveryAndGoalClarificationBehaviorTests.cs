using Runtime.Context;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.Execution;

namespace Runtime.Tests.DomainContent.EngineeringPlanning;

public class EngineeringDiscoveryAndGoalClarificationBehaviorTests
{
    private static RuntimeExecutionContext ContextWith(DiscoveryRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["DiscoveryRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Planning-Discovery", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_NeedsClarification()
    {
        var behavior = new EngineeringDiscoveryAndGoalClarificationBehavior();

        var result = (DiscoveryResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("needs_clarification", result.Status);
    }

    [Fact]
    public void Execute_EmptyGoal_NeedsClarificationWithGoalQuestion()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new EngineeringDiscoveryAndGoalClarificationBehavior();

        var result = (DiscoveryResult)behavior.Execute(ContextWith(new DiscoveryRequest("", repoContext)))!;

        Assert.Equal("needs_clarification", result.Status);
        Assert.Contains(result.ClarificationQuestions!, q => q.Contains("goal"));
    }

    [Fact]
    public void Execute_RealGoalWithTechnologyProfile_DiscoveryCompleteWithExistingCapability()
    {
        var technologyProfile = new Runtime.DomainContent.Onboarding.TechnologyProfileDraft("TPR-0001", new[] { new Runtime.Integration.RepositoryScanning.TechnologyEvidence("dotnet", "marker file(s) found at: repository root") });
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, technologyProfile);
        var behavior = new EngineeringDiscoveryAndGoalClarificationBehavior();

        var result = (DiscoveryResult)behavior.Execute(ContextWith(new DiscoveryRequest("Add a new API endpoint", repoContext)))!;

        Assert.Equal("discovery_complete", result.Status);
        Assert.Contains(result.Findings!.ExistingCapability, c => c.Contains("dotnet"));
        Assert.Empty(result.Findings.Gaps);
    }

    [Fact]
    public void Execute_RealGoalWithoutTechnologyProfile_DiscoveryCompleteWithGap()
    {
        var repoContext = new RepositoryExecutionContext("root", "test-repo", true, false, null, null, null);
        var behavior = new EngineeringDiscoveryAndGoalClarificationBehavior();

        var result = (DiscoveryResult)behavior.Execute(ContextWith(new DiscoveryRequest("Add a new API endpoint", repoContext)))!;

        Assert.Equal("discovery_complete", result.Status);
        Assert.Empty(result.Findings!.ExistingCapability);
        Assert.Single(result.Findings.Gaps);
    }
}
