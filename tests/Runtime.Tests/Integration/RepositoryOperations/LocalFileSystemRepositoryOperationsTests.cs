using Runtime.Integration.RepositoryOperations;

namespace Runtime.Tests.Integration.RepositoryOperations;

public class LocalFileSystemRepositoryOperationsTests
{
    [Fact]
    public void WriteFile_ThenReadFile_RoundTripsContent()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var writeResult = ops.WriteFile("src/hello.txt", "hello world");
            var readResult = ops.ReadFile("src/hello.txt");

            Assert.Equal(RepositoryOperationStatus.Success, writeResult.Status);
            Assert.Equal(RepositoryFileReadStatus.Success, readResult.Status);
            Assert.Equal("hello world", readResult.Content);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ReadFile_MissingFile_ReturnsFileNotFound()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var result = ops.ReadFile("does-not-exist.txt");

            Assert.Equal(RepositoryFileReadStatus.FileNotFound, result.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ReadFile_PathEscapesRoot_ReturnsPathEscapesRoot()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var result = ops.ReadFile("../outside.txt");

            Assert.Equal(RepositoryFileReadStatus.PathEscapesRoot, result.Status);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void DeleteFile_ExistingFile_RemovesIt()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);
            ops.WriteFile("temp.txt", "x");

            var result = ops.DeleteFile("temp.txt");

            Assert.Equal(RepositoryOperationStatus.Success, result.Status);
            Assert.False(File.Exists(Path.Combine(root.FullName, "temp.txt")));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void Move_ExistingFile_RelocatesIt()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);
            ops.WriteFile("a.txt", "content");

            var result = ops.Move("a.txt", "moved/b.txt");

            Assert.Equal(RepositoryOperationStatus.Success, result.Status);
            Assert.False(File.Exists(Path.Combine(root.FullName, "a.txt")));
            Assert.True(File.Exists(Path.Combine(root.FullName, "moved", "b.txt")));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void CreateDirectory_ThenDeleteDirectory_Succeeds()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var ops = new LocalFileSystemRepositoryOperations(root.FullName);

            var createResult = ops.CreateDirectory("nested/dir");
            var deleteResult = ops.DeleteDirectory("nested", recursive: true);

            Assert.Equal(RepositoryOperationStatus.Success, createResult.Status);
            Assert.Equal(RepositoryOperationStatus.Success, deleteResult.Status);
            Assert.False(Directory.Exists(Path.Combine(root.FullName, "nested")));
        }
        finally
        {
            root.Delete(true);
        }
    }
}
