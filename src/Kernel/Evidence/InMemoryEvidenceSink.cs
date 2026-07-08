namespace Kernel.Evidence;

// Default sink absent a registered Service. Non-authoritative: its output
// is not governed Evidence (no Lifecycle status, not Review-consumable)
// until the real Knowledge & Evidence Store is wired in at M2 — per this
// Epic's own ADR, Option 3's stated Consequences.
public sealed class InMemoryEvidenceSink : IEvidenceSink
{
    private readonly object _lock = new();
    private readonly List<EvidenceRecord> _records = new();

    // AiExecutionService is registered as a process-wide singleton with
    // this as its default evidence sink when none is supplied (Program.cs),
    // so concurrent adapter requests can call Record from multiple threads
    // at once - List<T> is not thread-safe for concurrent writes without
    // this lock (GA-3 Capability 1 concurrency audit).
    public IReadOnlyList<EvidenceRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return _records.ToArray();
            }
        }
    }

    public void Record(EvidenceRecord record)
    {
        lock (_lock)
        {
            _records.Add(record);
        }
    }
}
