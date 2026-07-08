namespace Runtime.Integration.RepositoryOperations;

// The Repository Operations Integration Adapter Boundary contract (P3 §2):
// read/write/create/delete/move a file or directory, root-bounded exactly
// like IRepositoryScanner - no member accepts a network address,
// credential, or process-execution argument.
public interface IRepositoryFileOperations
{
    RepositoryFileReadResult ReadFile(string relativePath);

    RepositoryOperationResult WriteFile(string relativePath, string content);

    RepositoryOperationResult CreateDirectory(string relativePath);

    RepositoryOperationResult DeleteFile(string relativePath);

    RepositoryOperationResult DeleteDirectory(string relativePath, bool recursive);

    RepositoryOperationResult Move(string sourceRelativePath, string destinationRelativePath);
}
