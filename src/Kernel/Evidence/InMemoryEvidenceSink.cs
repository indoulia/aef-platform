namespace Kernel.Evidence;

// Default sink absent a registered Service. Non-authoritative: its output
// is not governed Evidence (no Lifecycle status, not Review-consumable)
// until the real Knowledge & Evidence Store is wired in at M2 — per this
// Epic's own ADR, Option 3's stated Consequences.
public sealed class InMemoryEvidenceSink : IEvidenceSink
{
    private readonly List<EvidenceRecord> _records = new();

    public IReadOnlyList<EvidenceRecord> Records => _records;

    public void Record(EvidenceRecord record) => _records.Add(record);
}
