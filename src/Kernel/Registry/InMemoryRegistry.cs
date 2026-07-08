using System.Collections.Concurrent;
using Kernel.Evidence;

namespace Kernel.Registry;

// The one concrete storage-specific implementation for S1. Kept internal
// implementation, exposed only through IRegistry (I4 §7 public/internal rule).
public sealed class InMemoryRegistry : IRegistry
{
    // ConcurrentDictionary + TryAdd, not a plain Dictionary guarded by a
    // ContainsKey-then-index check: this Runtime's Registry is read and
    // written from a long-running host under concurrent Agent/Skill
    // activity, and a check-then-act pair on a plain Dictionary is a race
    // that can let two concurrent Register calls for the same Id both
    // "win" (defeating the no-silent-overwrite guarantee below) or corrupt
    // the Dictionary's internal state entirely.
    private readonly ConcurrentDictionary<string, RegistryEntry> _store = new();
    private readonly IEvidenceSink _evidenceSink;

    public InMemoryRegistry(IEvidenceSink? evidenceSink = null)
    {
        _evidenceSink = evidenceSink ?? new InMemoryEvidenceSink();
    }

    public RegisterResult Register(DefinitionSubmission submission)
    {
        if (string.IsNullOrWhiteSpace(submission.Id) || string.IsNullOrWhiteSpace(submission.Domain))
        {
            const string reason = "Id and Domain must both be non-empty.";
            RecordDecision("Reject", submission.Id, reason);
            return RegisterResult.RejectedMalformed(reason);
        }

        var entry = new RegistryEntry(submission.Id, submission.Domain, RegistryEntryStatus.Active);
        if (!_store.TryAdd(submission.Id, entry))
        {
            const string reason = "An entry with this Id is already Registered — no silent overwrite.";
            RecordDecision("Reject", submission.Id, reason);
            return RegisterResult.RejectedDuplicate(reason);
        }

        RecordDecision("Accept", submission.Id, reason: null);

        return RegisterResult.Success(entry);
    }

    private void RecordDecision(string decision, string subjectId, string? reason)
    {
        _evidenceSink.Record(new EvidenceRecord(
            Contract: "Registration",
            Decision: decision,
            SubjectId: subjectId,
            Reason: reason,
            Timestamp: DateTimeOffset.UtcNow));
    }

    public QueryResult Query(string id)
    {
        return _store.TryGetValue(id, out var entry)
            ? QueryResult.FoundResult(entry)
            : QueryResult.NotFound;
    }

    public IReadOnlyCollection<RegistryEntry> All() => _store.Values.ToList();
}
