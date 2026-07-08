using Kernel.Governance;
using Kernel.Registry;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.Execution;
using Runtime.Hosts.Skill;
using Runtime.Integration.DeliveryExecution;

namespace Runtime.Tests.Execution;

public class EngineeringExecutionPipelineTests
{
    private static RuntimeHandle BootWithBehaviors(IReadOnlyDictionary<string, IBehavior> behaviors) =>
        RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()), behaviors);

    private static IReadOnlyDictionary<string, IBehavior> ConversationBehaviors(
        IReadOnlyDictionary<string, IBehavior>? extra = null)
    {
        var behaviors = new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
            ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
        };
        if (extra is not null)
        {
            foreach (var (id, behavior) in extra)
            {
                behaviors[id] = behavior;
            }
        }
        return behaviors;
    }

    [Fact]
    public void Execute_FeatureRequest_RoutesAndDispatchesToFeatureLifecycleOrchestrator()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("WFL-DEL-0001", "Delivery"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0002", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("Please implement the new feature", "default-profile"));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0001", result.ExecutedWorkflowId);
    }

    [Fact]
    public void Execute_EpicRequest_RoutesAndDispatchesToEpicLifecycleOrchestrator()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("WFL-DEL-0004", "Delivery"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0006", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("Start the epic for onboarding", "default-profile"));

        Assert.True(result.Success);
        Assert.Equal("WFL-DEL-0004", result.ExecutedWorkflowId);
    }

    [Fact]
    public void Execute_UnroutableRequest_ReturnsFailureWithClarificationDiagnostics()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("What time is it?", "default-profile"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains("I need more information", result.PresentedResult);
    }

    [Fact]
    public void Execute_DeliveryRequest_DispatchesThroughSkillHostAndReturnsValidationResults()
    {
        var deliveryBehaviors = new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(_ => new AlwaysVerifiedVerifier()),
        };
        var runtime = BootWithBehaviors(ConversationBehaviors(deliveryBehaviors));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0018", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var payload = new Dictionary<string, object>
        {
            ["ImplementationRequest"] = new ImplementationRequest("story-1", "feature/x", new[] { "abc123" }, "plan"),
            ["RepositoryRoot"] = "C:\\fake-repo",
        };
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("Please deliver the fix", "default-profile", payload));

        Assert.True(result.Success);
        Assert.Equal("AGT-EXE-0010", result.ExecutedAgentId);
        Assert.NotNull(result.ValidationResults);
    }

    [Fact]
    public void Execute_WorkflowNotRegistered_ReturnsFailureDiagnostics()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var pipeline = new EngineeringExecutionPipeline(runtime);

        var result = pipeline.Execute(new EngineeringRequest("implement a feature", "default-profile"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class AlwaysVerifiedVerifier : IIsolatedChangeVerifier
    {
        public bool BranchExists(string branchName) => true;
        public bool IsDefaultBranch(string branchName) => false;
        public bool CommitExistsOnBranch(string branchName, string commitSha) => true;
    }
}
