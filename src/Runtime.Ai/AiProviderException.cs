namespace Runtime.Ai;

// IsTransient is the one classification decision a provider makes about
// its own failure (network error, 5xx, 429 => transient; 400/401/403,
// malformed request => structural) - AiExecutionService trusts it
// verbatim into RetryExecutor's own FailureClassification, mirroring
// POL-GOV-0001's "the caller supplying this result is the one place
// that classification decision is made" boundary.
public sealed class AiProviderException : Exception
{
    public bool IsTransient { get; }

    public AiProviderException(string message, bool isTransient, Exception? inner = null)
        : base(message, inner)
    {
        IsTransient = isTransient;
    }
}
