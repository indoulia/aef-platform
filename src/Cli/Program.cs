// This composition root deliberately duplicates the DI-wiring sequence in
// src/Host/Program.cs (minus every REST-specific piece) rather than
// sharing it - see Capability 3's design Decision 2 for the accepted
// tradeoff. If you change one, check the other.
using Adapters.Cli;
using AefPlatform.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
using Runtime.DomainContent.RepositoryValidation;
using Runtime.Execution;
using Runtime.Hosts.Skill;

// Host.CreateApplicationBuilder() is called WITHOUT the CLI's own `args`
// (unlike Host's own Program.cs, which legitimately has no positional
// arguments of its own to worry about) - passing this process's args
// straight through would feed them to the Generic Host's own
// command-line configuration provider, which would try to interpret
// this CLI's own domain flags (--user-request, --project-profile-id,
// ...) as configuration-key overrides. args is parsed separately, by
// this file's own ParseFlags, once command dispatch begins.
//
// ContentRootPath is pinned to AppContext.BaseDirectory (the directory
// the built AefPlatform.Cli.dll actually lives in) rather than left at
// the Generic Host's own default (Directory.GetCurrentDirectory()) - a
// CLI tool must find its own appsettings.json regardless of the
// caller's current directory. Without this, appsettings.json's "OpenAi"
// section silently fails to bind whenever this tool is invoked from
// anywhere other than its own output directory (discovered by manual
// smoke test, not by build or unit tests, since those already run with
// a matching working directory).
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });

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

using var host = builder.Build();
var provider = host.Services;

var runtime = provider.GetRequiredService<RuntimeHandle>();
var report = RuntimeArchitectureLoader.Load(runtime, EngineeringArchitectureManifest.KnownArtifacts);
if (!report.IsValid)
{
    Console.Error.WriteLine($"Startup validation failed: {report.Failures.Count} registration failure(s).");
    foreach (var failure in report.Failures)
    {
        Console.Error.WriteLine($"  {failure}");
    }

    return CliExitCodeMapper.SoftwareError;
}

try
{
    return Run(args, provider);
}
catch (Exception)
{
    Console.Error.WriteLine("An unexpected error occurred.");
    return CliExitCodeMapper.SoftwareError;
}

static int Run(string[] args, IServiceProvider provider)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: aef-cli <execute|capabilities> [options]");
        return CliExitCodeMapper.UsageError;
    }

    var command = args[0];
    var flags = ParseFlags(args.Skip(1).ToArray());

    return command switch
    {
        "execute" => RunExecute(flags, provider),
        "capabilities" => RunCapabilities(flags, provider),
        _ => UnknownCommand(command),
    };
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'. Expected 'execute' or 'capabilities'.");
    return CliExitCodeMapper.UsageError;
}

static Dictionary<string, string?> ParseFlags(string[] args)
{
    var flags = new Dictionary<string, string?>();
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var name = args[i][2..];
        var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
        flags[name] = value;
    }

    return flags;
}

static int RunExecute(Dictionary<string, string?> flags, IServiceProvider provider)
{
    var output = flags.GetValueOrDefault("output", "text")!;
    var cliArguments = new CliExecuteArguments(
        flags.GetValueOrDefault("user-request", string.Empty)!,
        flags.GetValueOrDefault("project-profile-id", string.Empty)!,
        flags.GetValueOrDefault("session-id"),
        flags.GetValueOrDefault("payload-json"),
        output);

    var validationError = CliExecuteArgumentsValidator.Validate(cliArguments);
    if (validationError is not null)
    {
        Console.Error.WriteLine(validationError);
        return CliExitCodeMapper.UsageError;
    }

    var securityEvaluator = provider.GetRequiredService<ISecurityEvaluator>();
    var decision = securityEvaluator.Evaluate(new SecurityContext(Environment.UserName, null));
    if (!decision.IsAllowed)
    {
        Console.Error.WriteLine(decision.DenialReason ?? "Access denied.");
        return CliExitCodeMapper.SecurityDenied;
    }

    IReadOnlyDictionary<string, object>? rawPayload = cliArguments.PayloadJson is null
        ? null
        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(cliArguments.PayloadJson);
    var payload = PayloadNormalization.Normalize(rawPayload);

    var agent = provider.GetRequiredService<IAgentExecutionInterface>();
    var request = new ExecutionRequest(Guid.NewGuid().ToString("N"), cliArguments.SessionId, cliArguments.UserRequest, cliArguments.ProjectProfileId, payload);
    var response = agent.Execute(request);

    Console.WriteLine(CliOutputFormatter.FormatExecutionResponse(response, output));
    return CliExitCodeMapper.MapExecutionStatus(response.Status);
}

static int RunCapabilities(Dictionary<string, string?> flags, IServiceProvider provider)
{
    var output = flags.GetValueOrDefault("output", "text")!;
    var agent = provider.GetRequiredService<IAgentExecutionInterface>();
    Console.WriteLine(CliOutputFormatter.FormatCapabilities(agent.DiscoverCapabilities(), output));
    return 0;
}
