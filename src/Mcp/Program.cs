// This composition root deliberately duplicates the DI-wiring sequence in
// src/Cli/Program.cs and src/Host/Program.cs (minus every CLI/REST-specific
// piece) rather than sharing it - the same accepted tradeoff src/Cli's own
// header comment already documents for itself vs. src/Host. If you change
// one of the three, check the other two.
//
// Epic M2-0001 Story S2: boots the full RuntimeBootstrapper/
// IAgentExecutionInterface registration (identical to Cli/Host), then
// starts an MCP server over stdio - zero tools registered yet (Story S3's
// own job). Transport and SDK are both pre-resolved decisions, not made
// here - see docs/06-history/2026-07-14-aef-m2-epic-m2-0001-pre-s2-
// architecture-decision-review.md (ai-engineering-framework repo).
using Adapters.Mcp;
using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Security;
using Runtime.Agent.Sessions;
using Runtime.Ai;
using Runtime.Ai.Prompts;
using Runtime.Ai.Providers;
using Runtime.Bootstrapping;
using Runtime.DomainContent.ConversationManagement;
using Runtime.DomainContent.DeliveryExecution;
using Runtime.DomainContent.EngineeringPlanning;
using Runtime.DomainContent.EngineeringSpecificationReview;
using Runtime.DomainContent.ImplementationValidation;
using Runtime.DomainContent.RepositoryIntelligence;
using Runtime.DomainContent.PlatformLifecycle;
using Runtime.DomainContent.RepositoryMaintenance;
using Runtime.DomainContent.RepositoryMigration;
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Execution;
using Runtime.Hosts.Skill;

// ContentRootPath pinned to AppContext.BaseDirectory - identical root cause
// and fix to Cli's/Host's own Program.cs: an MCP client (Claude Desktop,
// Claude Code) launches this process with an arbitrary working directory,
// not necessarily this executable's own output directory, so
// appsettings.json's "Ai:OpenAi" section would otherwise silently fail to
// bind.
//
// args is intentionally passed straight through here (unlike Cli's own
// Program.cs, which withholds args from the Generic Host to protect its own
// domain flags) - this executable has no positional command-line flags of
// its own to protect; an MCP client does not invoke it with arguments.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

// stdout is reserved exclusively for MCP JSON-RPC messages (the spec's own
// framing rule) - the Generic Host's default console logger writes to
// stdout, which corrupts the protocol stream the instant anything logs.
// Redirecting every level to stderr is the official SDK's own documented
// pattern for a stdio server, not a workaround.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Anchor reference so the Adapters.Mcp ProjectReference above is genuinely
// used by this assembly (kept minimal deliberately - Story S1's own
// architecture-boundary tests apply to Adapters.Mcp itself, not to this
// executable).
_ = typeof(McpAdapterAssemblyReference);

builder.Services
    .AddOptions<PlatformHostOptions>()
    .Bind(builder.Configuration.GetSection("AefPlatform"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AiProviderOptions>()
    .Bind(builder.Configuration.GetSection("Ai"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(new HttpClient());

builder.Services.AddSingleton<IAiProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
    var httpClient = sp.GetRequiredService<HttpClient>();
    return options.Provider switch
    {
        "AzureOpenAI" => new AzureOpenAiProvider(httpClient, new AzureOpenAiProviderOptions
        {
            Endpoint = options.AzureOpenAi!.Endpoint,
            ApiKey = options.AzureOpenAi.ApiKey,
            Deployment = options.AzureOpenAi.Deployment,
            ApiVersion = options.AzureOpenAi.ApiVersion,
        }),
        _ => new OpenAiProvider(httpClient, new OpenAiProviderOptions
        {
            ApiKey = options.OpenAi!.ApiKey,
            Model = options.OpenAi.Model,
            BaseUrl = options.OpenAi.BaseUrl,
        }),
    };
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
    return new AiExecutionService(sp.GetRequiredService<IAiProvider>(), timeout: TimeSpan.FromSeconds(options.TimeoutSeconds));
});

builder.Services.AddSingleton<IPromptCatalog, EngineeringPromptCatalog>();

builder.Services.AddSingleton(sp =>
{
    IReadOnlyDictionary<string, IBehavior> behaviors = new Dictionary<string, IBehavior>
    {
        ["SKL-EXE-0018"] = new DeliveryImplementationSkillBehavior(),
        ["SKL-EXE-0019"] = new DeliveryValidationSkillBehavior(),
        ["SKL-EXE-0020"] = new DeliveryDocumentationSkillBehavior(),
        ["SKL-EXE-0021"] = new ConversationIntakeAndRoutingBehavior(),
        ["SKL-EXE-0022"] = new ConversationHandoffAndFollowupBehavior(),
        ["SKL-EXE-0023"] = new EngineeringDiscoveryAndGoalClarificationBehavior(),
        ["SKL-EXE-0024"] = new SpecificationDraftingAndAlignmentBehavior(),
        ["SKL-EXE-0025"] = new ReadinessClassificationAndApprovalRequestBehavior(),
        ["SKL-EXE-0026"] = new SpecificationIndependentReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0027"] = new SpecificationConformanceValidationBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0028"] = new ArchitectureStandardsConformanceReviewBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0029"] = new ValidationFindingsSynthesisBehavior(sp.GetRequiredService<AiExecutionService>(), sp.GetRequiredService<IPromptCatalog>()),
        ["SKL-EXE-0030"] = new RepositoryAndArchitectureUnderstandingBehavior(),
        ["SKL-EXE-0031"] = new CodeIntelligenceAndQualitySignalDetectionBehavior(),
        ["SKL-EXE-0032"] = new EngineeringDiagnosticsAndRootCauseAnalysisBehavior(),
        ["SKL-EXE-0033"] = new RecommendationSynthesisAndEvidenceBasedReportingBehavior(),
        ["REPO-VALIDATION"] = new RepositoryValidationSkillBehavior(),
        ["SKL-EXE-0034"] = new MigrationAssessmentAndTransformationClassificationBehavior(),
        ["SKL-EXE-0035"] = new MigrationStrategyPhasingAndRiskAnalysisBehavior(),
        ["SKL-EXE-0036"] = new MigrationSpecificationAssemblyAndApprovalRequestBehavior(),
        ["SKL-EXE-0042"] = new RepositoryHealthAssessmentBehavior(),
        ["SKL-EXE-0043"] = new MaintenancePlanningRiskAndSequencingBehavior(),
        ["SKL-EXE-0044"] = new MaintenanceSpecificationAssemblyAndApprovalRequestBehavior(),
        ["SKL-EXE-0045"] = new PlatformLifecycleRequestClassificationBehavior(),
        ["SKL-EXE-0046"] = new RegistryAndReferenceIntegrityDiagnosisBehavior(),
        ["SKL-EXE-0047"] = new ArtifactPreservationClassificationBehavior(),
        ["SKL-EXE-0048"] = new SnapshotAndRestorePlanningBehavior(),
        ["SKL-EXE-0049"] = new PlatformLifecycleOperationPlanAssemblyAndApprovalRequestBehavior(),
    };

    return RuntimeBootstrapper.Boot(
        new RuntimeConfiguration(PolicyResolver: new PermissiveActivationPolicyResolver()),
        behaviors);
});
builder.Services.AddSingleton(sp => new EngineeringExecutionPipeline(sp.GetRequiredService<RuntimeHandle>()));
builder.Services.AddSingleton<ICapabilityCatalog, EngineeringAgentCapabilityCatalog>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IAgentExecutionInterface>(sp => new AgentExecutionService(
    sp.GetRequiredService<EngineeringExecutionPipeline>(),
    sp.GetRequiredService<ICapabilityCatalog>(),
    sp.GetRequiredService<ISessionStore>()));
builder.Services.AddSingleton<ISecurityEvaluator, TrustedDefaultSecurityEvaluator>();

// Story S2 scope ends here: the MCP server is registered with the tools
// capability not yet enabled at all - verified against the real SDK (not
// assumed) that .WithToolsFromAssembly() has no effect while zero
// [McpServerToolType] classes exist anywhere in this assembly, so it was
// deliberately left out rather than kept as a no-op call. A tools/list
// request today correctly, honestly returns a protocol-conformant
// "Method not found" (-32601) - Story S3 populates the tool catalog from
// ICapabilityCatalog, most likely via the SDK's programmatic
// WithListToolsHandler/WithCallToolHandler surface (since S3's own
// "transcribed from CapabilityDescriptor at runtime, never hand-invented"
// shape is a dynamic catalog, not compile-time-attributed C# methods
// WithToolsFromAssembly expects) - confirming which is Story S3's own
// first task, not decided here.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

using var host = builder.Build();

var runtime = host.Services.GetRequiredService<RuntimeHandle>();
var report = RuntimeArchitectureLoader.Load(runtime, EngineeringArchitectureManifest.KnownArtifacts);
if (!report.IsValid)
{
    await Console.Error.WriteLineAsync($"Startup validation failed: {report.Failures.Count} registration failure(s).");
    foreach (var failure in report.Failures)
    {
        await Console.Error.WriteLineAsync($"  {failure}");
    }

    Environment.Exit(1);
    return;
}

await host.RunAsync();
