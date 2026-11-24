using Kernel.Governance;
using Kernel.Registry;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Sessions;
using Runtime.Bootstrapping;
using Runtime.Context;
using Runtime.DomainContent.ConversationManagement;
using Runtime.Execution;
using Runtime.Hosts.Skill;

namespace Runtime.Agent.Tests;

public class AgentExecutionServiceTests
{
    private static RuntimeHandle BootWithBehaviors(IReadOnlyDictionary<string, IBehavior> behaviors) =>
        RuntimeBootstrapper.Boot(new RuntimeConfiguration(PolicyResolver: new AllowAllPolicyResolver()), behaviors);

    private static IReadOnlyDictionary<string, IBehavior> ConversationBehaviors(IBehavior? routingBehavior = null) =>
        new Dictionary<string, IBehavior>
        {
            ["SKL-EXE-0021"] = routingBehavior ?? new ConversationIntakeAndRoutingBehavior(),
            ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
        };

    private static void RegisterFeatureCapabilities(RuntimeHandle runtime)
    {
        runtime.Kernel.Registry.Register(new DefinitionSubmission("ORC-EXE-0001", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("WFL-DEL-0001", "Delivery"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("AGT-EXE-0002", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
    }

    private static AgentExecutionService NewService(RuntimeHandle runtime, ISessionStore? sessionStore = null) =>
        new(new EngineeringExecutionPipeline(runtime), new EngineeringAgentCapabilityCatalog(), sessionStore ?? new InMemorySessionStore());

    [Fact]
    public void Execute_CompletedPath_MapsCapabilityAgentDiagnosticsAndPresentedResult()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        RegisterFeatureCapabilities(runtime);
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", null, "Please implement the new feature", "default-profile", null));

        Assert.Equal(ExecutionStatus.Completed, response.Status);
        Assert.Null(response.Error);
        Assert.Equal("WFL-DEL-0001", response.Metadata.ExecutedCapabilityId);
    }

    [Fact]
    public void Execute_UnroutableRequest_ReturnsNeedsClarificationStatus()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", null, "What time is it?", "default-profile", null));

        Assert.Equal(ExecutionStatus.NeedsClarification, response.Status);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public void Execute_DispatchFailure_ReturnsFailedStatus_DistinctFromNeedsClarification()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0021", "Execution"));
        runtime.Kernel.Registry.Register(new DefinitionSubmission("SKL-EXE-0022", "Execution"));
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", null, "implement a feature", "default-profile", null));

        Assert.Equal(ExecutionStatus.Failed, response.Status);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public void Execute_SessionRoundTrip_SecondCallOmittingProjectProfileIdInheritsPriorTurnValue()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        RegisterFeatureCapabilities(runtime);
        var service = NewService(runtime);

        var session = service.StartSession("default-profile");
        var first = service.Execute(new ExecutionRequest("req-1", session.SessionId, "Please implement the new feature", "default-profile", null));
        Assert.Equal(ExecutionStatus.Completed, first.Status);
        Assert.Equal(session.SessionId, first.SessionId);

        var second = service.Execute(new ExecutionRequest("req-2", session.SessionId, "Please implement the new feature", "", null));

        Assert.Equal(ExecutionStatus.Completed, second.Status);
        Assert.Equal(session.SessionId, second.SessionId);
    }

    private sealed class NoCodeIntelligence : Runtime.Integration.CodeIntelligence.ICodeIntelligenceProvider
    {
        private static Runtime.Integration.CodeIntelligence.CodeLookupResult None => Runtime.Integration.CodeIntelligence.CodeLookupResult.Unavailable("not available in this test.");

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindRelevant(string repositoryRoot, string taskDescription, int maxResults) => None;

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindSymbol(string repositoryRoot, string name, int maxResults) => None;

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindTests(string repositoryRoot, string file) => None;

        public Runtime.Integration.CodeIntelligence.CodeLookupResult FindAffected(string repositoryRoot, IReadOnlyList<string> changedFiles) => None;
    }

    [Fact]
    public void Execute_MapsExecutionResultDocument_ToEvidenceDocument()
    {
        var behaviors = new Dictionary<string, IBehavior>(ConversationBehaviors())
        {
            ["PROMPT-GROUNDING"] = new Runtime.DomainContent.PromptGrounding.PromptGroundingSkillBehavior(new NoCodeIntelligence()),
        };
        var runtime = BootWithBehaviors(behaviors);
        foreach (var id in new[] { "SKL-EXE-0021", "SKL-EXE-0022", "PROMPT-GROUNDING" })
        {
            runtime.Kernel.Registry.Register(new DefinitionSubmission(id, "Execution"));
        }

        var payload = new Dictionary<string, object> { ["UserPrompt"] = "make it better", ["RepositoryName"] = "tmp", ["RepositoryRoot"] = Path.GetTempPath() };
        var response = NewService(runtime).Execute(new ExecutionRequest("req-1", null, "refine prompt", "default-profile", payload));

        Assert.Equal(ExecutionStatus.Completed, response.Status);
        Assert.StartsWith("## Goal", response.Evidence.Document);
    }

    [Fact]
    public void Execute_UnknownSessionId_ReturnsErrorWithoutInvokingPipeline()
    {
        var countingBehavior = new CountingRoutingBehavior();
        var runtime = BootWithBehaviors(ConversationBehaviors(countingBehavior));
        var service = NewService(runtime);

        var response = service.Execute(new ExecutionRequest("req-1", "unknown-session-id", "Please implement the new feature", "default-profile", null));

        Assert.Equal(ExecutionStatus.Failed, response.Status);
        Assert.NotNull(response.Error);
        Assert.Equal(0, countingBehavior.InvocationCount);
    }

    [Fact]
    public void Execute_WithProgressObserver_ReceivesExactlyRoutingAndTerminalEvents()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        RegisterFeatureCapabilities(runtime);
        var service = NewService(runtime);
        var observer = new RecordingProgressObserver();

        var response = service.Execute(new ExecutionRequest("req-1", null, "Please implement the new feature", "default-profile", null, observer));

        Assert.Equal(2, observer.Events.Count);
        Assert.Equal("Routing", observer.Events[0].Stage);
        Assert.Equal(response.Status.ToString(), observer.Events[1].Stage);
    }

    [Fact]
    public void DiscoverCapabilities_DelegatesToCatalog()
    {
        var runtime = BootWithBehaviors(ConversationBehaviors());
        var service = NewService(runtime);

        Assert.Equal(13, service.DiscoverCapabilities().Count);
    }

    [Fact]
    public void RuntimeAgentAssembly_NowReferencesRuntime()
    {
        var referencedAssemblyNames = typeof(AgentExecutionService).Assembly
            .GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.Contains("Runtime", referencedAssemblyNames);
    }

    private sealed class AllowAllPolicyResolver : IPolicyResolver
    {
        public PolicyResolution Resolve(ActivationRequest request) => PolicyResolution.Authorize();
    }

    private sealed class CountingRoutingBehavior : IBehavior
    {
        private readonly IBehavior _inner = new ConversationIntakeAndRoutingBehavior();
        public int InvocationCount { get; private set; }

        public object? Execute(RuntimeExecutionContext context)
        {
            InvocationCount++;
            return _inner.Execute(context);
        }
    }

    private sealed class RecordingProgressObserver : IExecutionProgressObserver
    {
        public List<ExecutionProgressEvent> Events { get; } = new();
        public void OnProgress(ExecutionProgressEvent progressEvent) => Events.Add(progressEvent);
    }
}
