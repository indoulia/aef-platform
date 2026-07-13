namespace Adapters.Cli;

/// <summary>
/// AEF Platform's own installation/deployment capability, satisfying the
/// Distribution Component Installation Contract (AEF Distribution
/// Architecture Specification Addendum 1, §4.1/INV-12): version-pinned,
/// target-path-parameterized, idempotent, failure reported as a result
/// rather than an uncaught exception, materialized location reported on
/// success. This capability is AEF Platform's own — it exists independent
/// of AEF Distribution and is invoked by it, never owned by it.
/// </summary>
public interface IPlatformInstaller
{
    InstallOutcome Install(string requestedVersion, string targetPath);
}
