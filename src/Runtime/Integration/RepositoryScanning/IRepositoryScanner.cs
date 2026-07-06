namespace Runtime.Integration.RepositoryScanning;

// The generic Integration Adapter Boundary contract for reading a
// repository's characteristics (docs/aep/P1-Autonomous-Engineering-
// Platform-Architecture.md §7/§11, ai-engineering-framework repo): every
// integration is an adapter binding, never an architectural dependency,
// so no member here may encode a specific external system's identity or
// shape - in particular, no member accepts a network address,
// credential, or process-execution argument of any kind (Epic 0008,
// Story S22 acceptance criterion 1; mechanically enforced by a
// reflection-based contract test, extended further in Story S25).
public interface IRepositoryScanner
{
    // Whether this scanner's bound root is itself readable at all.
    bool IsRootReadable();

    // Lists the immediate entries (name, is-directory) of the directory
    // at relativePath within the bound root - no file content is read
    // beyond that. Never throws for an expected, in-bounds-of-the-
    // contract failure (root not readable, path escapes root, path does
    // not exist); see RepositoryScanResult.
    RepositoryScanResult ListEntries(string relativePath);
}
