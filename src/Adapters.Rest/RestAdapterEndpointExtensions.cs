using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Runtime.Agent;
using Runtime.Agent.Security;

namespace Adapters.Rest;

public static class RestAdapterEndpointExtensions
{
    // AgentExecutionService.UnknownSessionResponse's own literal reason
    // string (Runtime.Agent, frozen) - duplicated here rather than
    // promoted to a shared constant, since editing a frozen GA-2 file for
    // a robustness nicety (not a bug) is out of this Capability's scope
    // (design Decision 9's "Accepted risk"). If this string is ever
    // renamed, the 404-mapping test in ExecutionEndpointTests fails
    // loudly against the real AgentExecutionService - not a silent break.
    private const string UnknownSessionReason = "Unknown session.";

    public static RouteGroupBuilder MapRestAdapterEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/execution", Execute);
        group.MapGet("/capabilities", DiscoverCapabilities);
        group.MapPost("/sessions", StartSession);
        group.MapGet("/health", Health);
        group.MapGet("/version", Version);
        return group;
    }

    private static IResult Execute(RestExecutionRequest request, IAgentExecutionInterface agent, ISecurityEvaluator securityEvaluator, HttpContext httpContext)
    {
        var validationError = RestExecutionRequestValidator.Validate(request);
        if (validationError is not null)
        {
            return Results.Problem(detail: validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        var decision = securityEvaluator.Evaluate(RestSecurityContextFactory.FromHttpContext(httpContext));
        if (!decision.IsAllowed)
        {
            return Results.Problem(detail: decision.DenialReason ?? "Access denied.", statusCode: StatusCodes.Status403Forbidden);
        }

        var executionRequest = new ExecutionRequest(Guid.NewGuid().ToString("N"), request.SessionId, request.UserRequest, request.ProjectProfileId, request.Payload);
        var response = agent.Execute(executionRequest);

        if (response.Status == ExecutionStatus.Failed && response.Error?.Reason == UnknownSessionReason)
        {
            return Results.Json(response, statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(response);
    }

    private static IResult DiscoverCapabilities(IAgentExecutionInterface agent) => Results.Ok(agent.DiscoverCapabilities());

    private static IResult StartSession(RestStartSessionRequest request, IAgentExecutionInterface agent)
    {
        if (request.ProjectProfileId is not null && string.IsNullOrWhiteSpace(request.ProjectProfileId))
        {
            return Results.Problem(detail: "ProjectProfileId must not be whitespace when supplied.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Json(agent.StartSession(request.ProjectProfileId), statusCode: StatusCodes.Status201Created);
    }

    private static IResult Health() => Results.Ok(new RestHealthResponse("Healthy"));

    private static IResult Version() => Results.Ok(new RestVersionResponse(GetInformationalVersion()));

    private static string GetInformationalVersion()
    {
        var assembly = typeof(RestAdapterEndpointExtensions).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
