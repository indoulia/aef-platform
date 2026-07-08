namespace Runtime.Ai;

public interface IAiProvider
{
    string Name { get; }

    Task<AiCompletionResult> CompleteAsync(AiPromptRequest request, CancellationToken cancellationToken);
}
