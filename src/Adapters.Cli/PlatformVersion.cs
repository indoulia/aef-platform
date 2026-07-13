using System.Reflection;

namespace Adapters.Cli;

/// <summary>
/// Resolves AEF Platform's own compiled version for comparison against an
/// `install --version` request. The .NET SDK appends SemVer build metadata
/// (`+&lt;source-revision-id&gt;`) to <see cref="AssemblyInformationalVersionAttribute"/>
/// whenever source-control info is available at build time - discovered by
/// verifying the real released artifact (AEF-Platform-v1.0.0), not caught by
/// unit tests that constructed <see cref="FileSystemPlatformInstaller"/>
/// with a hand-provided version string. Per SemVer, build metadata must not
/// affect version comparison, so it is stripped before any comparison is made.
/// </summary>
public static class PlatformVersion
{
    public static string Resolve(Assembly assembly) =>
        StripBuildMetadata(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown");

    public static string StripBuildMetadata(string version)
    {
        var plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }
}
