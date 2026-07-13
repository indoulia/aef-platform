namespace Adapters.Cli;

/// <summary>
/// Materializes AEF Platform by copying its own already-built output
/// directory (<paramref name="sourceDirectory"/>) into an arbitrary target
/// path. This is the minimal capability satisfying §4.1: it does not
/// introduce a package/artifact repository — the "version" it installs is
/// exactly whatever is currently built, verified by comparing the caller's
/// requested version against <paramref name="builtVersion"/> before doing
/// any file operation, so a mismatched request fails closed rather than
/// silently materializing the wrong build.
/// </summary>
public sealed class FileSystemPlatformInstaller(string sourceDirectory, string builtVersion) : IPlatformInstaller
{
    public InstallOutcome Install(string requestedVersion, string targetPath)
    {
        if (requestedVersion != builtVersion)
        {
            return Failure(
                $"Requested version '{requestedVersion}' does not match this build's version '{builtVersion}'.");
        }

        if (!Directory.Exists(sourceDirectory))
        {
            return Failure($"Source directory '{sourceDirectory}' does not exist.");
        }

        try
        {
            CopyDirectory(sourceDirectory, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure($"Failed to materialize AEF Platform at '{targetPath}': {ex.Message}");
        }

        return new InstallOutcome(true, targetPath, null);
    }

    private static InstallOutcome Failure(string reason) => new(false, null, reason);

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
