using Runtime.Context;
using Runtime.DomainContent.EngineeringPlanning;

namespace Runtime.Tests.DomainContent.EngineeringPlanning;

public class ReadinessClassificationAndApprovalRequestBehaviorTests
{
    private static readonly SpecificationDraft SampleDraft = new("Standard", "goal", "criteria", "capability", "requirements", "scope", "risks", Array.Empty<string>(), "acceptance");

    private static RuntimeExecutionContext ContextWith(ReadinessRequest? request)
    {
        var content = request is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["ReadinessRequest"] = request };
        return new RuntimeExecutionContext("instance-1", new Tier0Context(Array.Empty<string>(), Array.Empty<string>()), "default-profile", "Planning-Readiness", content);
    }

    [Fact]
    public void Execute_NoRequestSupplied_RequiresClarification()
    {
        var behavior = new ReadinessClassificationAndApprovalRequestBehavior();

        var result = (ReadinessResult)behavior.Execute(ContextWith(null))!;

        Assert.Equal("Requires Clarification", result.ImplementationReadiness);
        Assert.Equal("pending_approval", result.Status);
    }

    [Fact]
    public void Execute_NoOpenItemsNoAlignmentFindings_ReadyForImplementation()
    {
        var behavior = new ReadinessClassificationAndApprovalRequestBehavior();
        var request = new ReadinessRequest(SampleDraft, Array.Empty<string>(), Array.Empty<string>());

        var result = (ReadinessResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Ready for Implementation", result.ImplementationReadiness);
        Assert.Equal("pending_approval", result.Status);
    }

    [Fact]
    public void Execute_OpenItemsPresent_RequiresClarification()
    {
        var behavior = new ReadinessClassificationAndApprovalRequestBehavior();
        var request = new ReadinessRequest(SampleDraft, Array.Empty<string>(), new[] { "Success Criteria requires human input." });

        var result = (ReadinessResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Requires Clarification", result.ImplementationReadiness);
    }

    [Fact]
    public void Execute_NoOpenItemsButAlignmentFindingsPresent_RequiresRedesign()
    {
        var behavior = new ReadinessClassificationAndApprovalRequestBehavior();
        var request = new ReadinessRequest(SampleDraft, new[] { "Conflicts with ADR-0007." }, Array.Empty<string>());

        var result = (ReadinessResult)behavior.Execute(ContextWith(request))!;

        Assert.Equal("Requires Redesign", result.ImplementationReadiness);
    }
}
