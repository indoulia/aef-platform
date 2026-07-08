using Runtime.Agent.Capabilities;
using Runtime.Agent.Sessions;
using Runtime.Execution;

namespace Runtime.Agent;

// The single seam into governed execution (ADR-0025): the only caller of
// EngineeringExecutionPipeline.Execute anywhere in this assembly. Execute
// is synchronous, matching the pipeline's own synchronous signature.
public sealed class AgentExecutionService : IAgentExecutionInterface
{
    private readonly EngineeringExecutionPipeline _pipeline;
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly ISessionStore _sessionStore;

    public AgentExecutionService(EngineeringExecutionPipeline pipeline, ICapabilityCatalog capabilityCatalog, ISessionStore sessionStore)
    {
        _pipeline = pipeline;
        _capabilityCatalog = capabilityCatalog;
        _sessionStore = sessionStore;
    }

    public IReadOnlyList<CapabilityDescriptor> DiscoverCapabilities() => _capabilityCatalog.Discover();

    public ExecutionSession StartSession(string? projectProfileId) => _sessionStore.Create(projectProfileId);

    public ExecutionResponse Execute(ExecutionRequest request)
    {
        var startedAt = DateTimeOffset.UtcNow;

        ExecutionSession? session = null;
        if (request.SessionId is not null)
        {
            session = _sessionStore.Get(request.SessionId);
            if (session is null)
            {
                return UnknownSessionResponse(request, startedAt);
            }
        }

        // Explicit fields on the request always win: an empty
        // ProjectProfileId (there is no nullable variant of this required
        // field) or a null Payload defer to the session's stored default.
        var effectiveProjectProfileId = !string.IsNullOrEmpty(request.ProjectProfileId)
            ? request.ProjectProfileId
            : session?.LastProjectProfileId ?? request.ProjectProfileId;
        var effectivePayload = request.Payload ?? session?.LastPayload;

        request.ProgressObserver?.OnProgress(new ExecutionProgressEvent(
            request.RequestId, "Routing", "Routing request through the Engineering Execution Pipeline.", DateTimeOffset.UtcNow));

        var executionResult = _pipeline.Execute(new EngineeringRequest(request.UserRequest, effectiveProjectProfileId, effectivePayload));

        if (session is not null)
        {
            session = _sessionStore.Touch(session.SessionId, effectiveProjectProfileId, effectivePayload);
        }

        var response = MapToResponse(request, session, executionResult, startedAt, DateTimeOffset.UtcNow);

        request.ProgressObserver?.OnProgress(new ExecutionProgressEvent(
            request.RequestId, response.Status.ToString(), "Execution finished.", DateTimeOffset.UtcNow));

        return response;
    }

    private static ExecutionResponse UnknownSessionResponse(ExecutionRequest request, DateTimeOffset startedAt)
    {
        var completedAt = DateTimeOffset.UtcNow;
        return new ExecutionResponse(
            request.RequestId,
            request.SessionId,
            ExecutionStatus.Failed,
            new ExecutionError("Unknown session.", new[] { $"No session exists for id '{request.SessionId}'." }),
            null,
            null,
            new ExecutionEvidence(Array.Empty<string>(), null, null, null, null),
            new ExecutionMetadata(null, null, completedAt - startedAt, startedAt, completedAt));
    }

    // Kind == NeedsClarification                                -> NeedsClarification
    // Kind == Completed || (Unspecified && Success)              -> Completed
    // Kind == Failed    || (Unspecified && !Success)             -> Failed
    private static ExecutionStatus MapStatus(ExecutionResultKind kind, bool success) => kind switch
    {
        ExecutionResultKind.NeedsClarification => ExecutionStatus.NeedsClarification,
        ExecutionResultKind.Completed => ExecutionStatus.Completed,
        ExecutionResultKind.Failed => ExecutionStatus.Failed,
        _ => success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
    };

    private static ExecutionResponse MapToResponse(ExecutionRequest request, ExecutionSession? session, ExecutionResult result, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        var status = MapStatus(result.Kind, result.Success);
        var error = status == ExecutionStatus.Completed
            ? null
            : new ExecutionError(result.PresentedResult ?? "Execution did not complete.", result.Diagnostics);

        var evidence = new ExecutionEvidence(result.Diagnostics, result.ValidationResults, result.RepositoryAnalyzed, result.FilesChanged, result.TestsExecuted);
        var metadata = new ExecutionMetadata(result.ExecutedWorkflowId, result.ExecutedAgentId, result.Duration, startedAt, completedAt);

        return new ExecutionResponse(
            request.RequestId,
            session?.SessionId,
            status,
            error,
            result.PresentedResult,
            result.SuggestedNextStep,
            evidence,
            metadata);
    }
}
