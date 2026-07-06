namespace Runtime.Integration.RepositoryScanning;

// One immediate directory entry - its name and whether it is itself a
// directory. Nothing else: a listing never carries file content
// (Epic 0008, Story S22 acceptance criterion 3).
public sealed record RepositoryEntry(string Name, bool IsDirectory);
