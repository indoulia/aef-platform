namespace Runtime.Integration.RepositoryOperations;

// The one concrete binding of IRepositoryFileOperations in scope: local
// filesystem, root-bounded. Resolution/escape-prevention mirrors
// LocalFileSystemRepositoryScanner's own algorithm (Epic 0008, Story S25) -
// duplicated rather than shared, consistent with this codebase's existing
// pattern of independently self-contained Integration Adapters (S22's
// scanner and Story S30's git verifier already never share code either).
public sealed class LocalFileSystemRepositoryOperations : IRepositoryFileOperations
{
    private readonly string _root;

    public LocalFileSystemRepositoryOperations(string root)
    {
        if (string.IsNullOrEmpty(root))
        {
            throw new ArgumentException("Root path must not be null or empty; the adapter has no boundary to enforce without one.", nameof(root));
        }

        _root = Path.GetFullPath(root);
    }

    public RepositoryFileReadResult ReadFile(string relativePath)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryFileReadResult.Failure(RepositoryFileReadStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        if (!File.Exists(resolved))
        {
            return RepositoryFileReadResult.Failure(RepositoryFileReadStatus.FileNotFound, $"File '{relativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            return RepositoryFileReadResult.Success(File.ReadAllText(resolved));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryFileReadResult.Failure(RepositoryFileReadStatus.ReadFailed, $"File '{relativePath}' could not be read: {ex.Message}");
        }
    }

    public RepositoryOperationResult WriteFile(string relativePath, string content)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        try
        {
            var parent = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(resolved, content);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"File '{relativePath}' could not be written: {ex.Message}");
        }
    }

    public RepositoryOperationResult CreateDirectory(string relativePath)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        try
        {
            Directory.CreateDirectory(resolved);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"Directory '{relativePath}' could not be created: {ex.Message}");
        }
    }

    public RepositoryOperationResult DeleteFile(string relativePath)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        if (!File.Exists(resolved))
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathNotFound, $"File '{relativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            File.Delete(resolved);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"File '{relativePath}' could not be deleted: {ex.Message}");
        }
    }

    public RepositoryOperationResult DeleteDirectory(string relativePath, bool recursive)
    {
        var resolved = Resolve(relativePath);
        if (resolved is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Path '{relativePath}' resolves outside root '{_root}'.");
        }

        if (!Directory.Exists(resolved))
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathNotFound, $"Directory '{relativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            Directory.Delete(resolved, recursive);
            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"Directory '{relativePath}' could not be deleted: {ex.Message}");
        }
    }

    public RepositoryOperationResult Move(string sourceRelativePath, string destinationRelativePath)
    {
        var source = Resolve(sourceRelativePath);
        var destination = Resolve(destinationRelativePath);
        if (source is null || destination is null)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathEscapesRoot, $"Source '{sourceRelativePath}' or destination '{destinationRelativePath}' resolves outside root '{_root}'.");
        }

        var sourceIsFile = File.Exists(source);
        var sourceIsDirectory = Directory.Exists(source);
        if (!sourceIsFile && !sourceIsDirectory)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.PathNotFound, $"Source '{sourceRelativePath}' does not exist within root '{_root}'.");
        }

        try
        {
            var destinationParent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }

            if (sourceIsFile)
            {
                File.Move(source, destination);
            }
            else
            {
                Directory.Move(source, destination);
            }

            return RepositoryOperationResult.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RepositoryOperationResult.Failure(RepositoryOperationStatus.OperationFailed, $"Move from '{sourceRelativePath}' to '{destinationRelativePath}' failed: {ex.Message}");
        }
    }

    // Mirrors LocalFileSystemRepositoryScanner's own ResolveWithinRoot
    // algorithm exactly (Epic 0008, Story S25): walks the combined path one
    // segment at a time, following any reparse point (symlink or junction)
    // encountered along the way to its real target before continuing, so a
    // symlink placed inside _root that points outside it cannot be used to
    // escape the boundary - a plain textual StartsWith(root) check on the
    // unresolved path would miss exactly this case. Also resolves a file
    // symlink at the final segment, which the scanner itself never needed
    // (it only ever lists directories) but this adapter's file-mutating
    // operations do.
    private string? Resolve(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relativePath ?? string.Empty));
        var current = Path.GetPathRoot(combined) ?? combined;
        var segments = combined[current.Length..].Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

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
            else if (File.Exists(current))
            {
                var linkTarget = new FileInfo(current).ResolveLinkTarget(returnFinalTarget: true);
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
