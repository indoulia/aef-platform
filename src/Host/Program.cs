using Adapters.Rest;
using AefPlatform.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Runtime.Agent;
using Runtime.Agent.Capabilities;
using Runtime.Agent.Health;
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

var builder = WebApplication.CreateBuilder(args);

var restOptions = builder.Configuration.GetSection("RestAdapter").Get<RestAdapterOptions>() ?? new RestAdapterOptions();

builder.Services
    .AddOptions<RestAdapterOptions>()
    .Bind(builder.Configuration.GetSection("RestAdapter"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = restOptions.MaxRequestBodyBytes);
builder.WebHost.UseUrls($"http://localhost:{restOptions.Port}");

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// A single shared HttpClient, not IHttpClientFactory - avoids adding the
// Microsoft.Extensions.Http package (no new NuGet dependency, matching
// this repo's existing zero-extra-dependency convention; a long-lived
// singleton HttpClient is the documented-safe usage for exactly this
// "one well-known remote host per process" shape).
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
builder.Services.AddSingleton<IRuntimeHealthContributor, AiProviderReachabilityHealthContributor>();
builder.Services.AddHostedService<RuntimeStartupHostedService>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        // BadHttpRequestException (e.g. malformed JSON on a [FromBody]
        // parameter) already carries the correct 4xx status - only an
        // exception with no such classification is a genuine, unexpected
        // 500. Never surfaces the exception's own message either way, per
        // "do not expose internal exceptions" (design Decision 9).
        var exception = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        var statusCode = exception is BadHttpRequestException badHttpRequestException
            ? badHttpRequestException.StatusCode
            : StatusCodes.Status500InternalServerError;
        var detail = statusCode == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : "The request could not be processed.";

        await Results.Problem(detail: detail, statusCode: statusCode).ExecuteAsync(context);
    });
});

app.UseRequestObservability();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGroup(restOptions.BasePath).MapRestAdapterEndpoints();

await app.RunAsync();

public partial class Program { }
