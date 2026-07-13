using System.Reflection;

namespace Adapters.Cli.Tests;

public class PlatformVersionTests
{
    [Fact]
    public void StripBuildMetadata_NoPlus_ReturnsUnchanged()
    {
        Assert.Equal("1.0.0", PlatformVersion.StripBuildMetadata("1.0.0"));
    }

    [Fact]
    public void StripBuildMetadata_WithSourceRevisionSuffix_ReturnsVersionOnly()
    {
        // Reproduces the real defect found verifying the released build:
        // the SDK's own AssemblyInformationalVersionAttribute embeds
        // +<git-sha> (SemVer build metadata) whenever source-revision info
        // is available. A caller passing --version 1.0.0 must still match.
        Assert.Equal("1.0.0", PlatformVersion.StripBuildMetadata("1.0.0+f2123ecb10959c1b090ef88dd3681dca731a3d0f"));
    }

    [Fact]
    public void StripBuildMetadata_EmptyString_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, PlatformVersion.StripBuildMetadata(string.Empty));
    }

    [Fact]
    public void Resolve_ExecutingAssembly_NeverContainsPlus()
    {
        var resolved = PlatformVersion.Resolve(Assembly.GetExecutingAssembly());

        Assert.DoesNotContain('+', resolved);
    }
}
