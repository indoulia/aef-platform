namespace Runtime.Ai.Providers;

// A provider error body flows into AiExecutionService's EvidenceRecord.Reason
// (an internal audit record) via AiProviderException.Message - bounding it
// keeps an unbounded/adversarial error response from bloating diagnostics,
// independent of whether Evidence is ever surfaced externally later.
internal static class HttpErrorBodyFormatter
{
    private const int MaxLength = 1000;

    public static string Truncate(string body) =>
        body.Length > MaxLength ? $"{body[..MaxLength]}...(truncated, {body.Length} chars total)" : body;
}
