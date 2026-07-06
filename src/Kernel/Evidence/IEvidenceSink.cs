namespace Kernel.Evidence;

// The pluggable Evidence-sink extension point epics/decisions/0001-kernel-evidence-emission-before-store-exists.md
// (this Epic's own ADR) requires: every Kernel accept/reject decision invokes this,
// absent the Knowledge & Evidence Store (Construction Unit 5, Milestone M2), which
// will register itself against this same extension point once built.
public interface IEvidenceSink
{
    void Record(EvidenceRecord record);
}
