namespace Runtime.Integration.RepositoryScanning;

// The one concrete binding of IRepositoryScanner in scope for Story S22:
// local filesystem, read-only, root-bounded. Every method resolves
// relative (or absolute) paths against the constructor-supplied root and
// refuses - returning a structured failure, never throwing an
// unhandled exception, never silently truncating - any resolution that
// would escape that root, including via ".." segments or a reparse
// point (symbolic link or junction) that resolves outside the root
// (Epic 0008, Story S22 acceptance criterion 2).
public sealed class LocalFileSystemRepositoryScanner : IRepositoryScanner
{
    private readonly string _root;

    public LocalFileSystemRepositoryScanner(string root)
    {
        // A null/empty root is a caller-contract violation, not a
        // runtime environmental failure: an adapter cannot enforce a
        // boundary it was never given one to enforce. Guarding
        // explicitly here turns what would otherwise be an unguarded,
        // incidental ArgumentNullException/ArgumentException from
        // Path.GetFullPath (Epic 0008, Story S25) into a deliberate,
        // documented one - consistent with this contract's "structured
        // failure, never an incidental exception" discipline, even
        // though this particular failure is a thrown exception rather
        // than a RepositoryScanResult (that shape is reserved for
        // failures only discoverable once a root and a call are both in
        // hand, e.g. root-not-readable or path-escapes-root).
        if (string.IsNullOrEmpty(root))
        {
            throw new ArgumentException("Root path must not be null or empty; the scanner has no boundary to enforce without one.", nameof(root));
        }

        _root = Path.GetFullPath(root);
    }

    public bool IsRootReadable()
    {
        try
        {
            return Directory.Exists(_root);
        }
        catch
        {
            return false;
        }
    }

    public RepositoryScanResult ListEntries(string relativePath)
    {
        if (!IsRootReadable())
        {
            return RepositoryScanResult.Failure(RepositoryScanStatus.RootNotReadable, $"Root '{_root}' is not readable.");
        }

        string? resolved;
        try
        {
            resolved = ResolveWithinRoot(relativePath ?? string.Empty);
        }
        catch
        {
            return RepositoryScanResult.Failure(RepositoryScanStatus.PathEscapesRoot, $"Path '{relativePath}' could not be resolved safely within root '{_root}'.");
        }

        if (resolved is null)
        {
            return RepositoryScanResult.Failure(RepositoryScanStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        if (!Directory.Exists(resolved))
        {
            return RepositoryScanResult.Failure(RepositoryScanStatus.PathNotFound, $"Path '{relativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            var entries = Directory.EnumerateFileSystemEntries(resolved)
                .Select(entryPath => new RepositoryEntry(Path.GetFileName(entryPath), Directory.Exists(entryPath)))
                .ToList();

            return RepositoryScanResult.Success(entries);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryScanResult.Failure(RepositoryScanStatus.RootNotReadable, $"Path '{relativePath}' could not be read within root '{_root}'.");
        }
    }

    // Resolves relativePath against the root in a single shot first -
    // Path.Combine(root, relativePath) returns relativePath verbatim when
    // it is itself rooted (e.g. "C:\Windows\System32"), so an absolute
    // input resolves to its own real, ambient-current-directory-independent
    // target rather than being reinterpreted relative to root. It is only
    // safe to walk the *resulting* path's segments afterward, one at a
    // time, following any reparse point (symlink or junction) encountered
    // along the way to its real target before continuing - so a later ".."
    // segment is evaluated against the resolved, real location, not the
    // pre-link path. Returns null if the final, fully-resolved location
    // falls outside the root.
    private string? ResolveWithinRoot(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relativePath));

        var current = Path.GetPathRoot(combined) ?? combined;
        var segments = combined.Substring(current.Length).Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            current = Path.GetFullPath(Path.Combine(current, segment));

            if (Directory.Exists(current))
            {
                var linkTarget = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true);
                if (linkTarget is not null)
                {
                    current = linkTarget.FullName;
                }
            }
        }

        return IsWithinRoot(current) ? current : null;
    }

    private bool IsWithinRoot(string candidateFullPath)
    {
        var normalizedRoot = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidate = candidateFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
