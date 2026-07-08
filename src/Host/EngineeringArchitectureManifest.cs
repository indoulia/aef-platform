namespace AefPlatform.Host;

// Every governed artifact already proven independently registrable by this
// repository's own per-Epic dogfooding scenarios (ai-engineering-framework
// repo Epics 0001-0006) — see docs/superpowers/plans/2026-07-08-p1-runtime-
// foundation-platform-bootstrap.md's Gap Analysis for how this list was
// derived. Consolidates them into the one canonical set the Runtime Loader
// registers together at boot, instead of each Epic proving registrability
// only in an isolated, siloed test.
public static class EngineeringArchitectureManifest
{
    public static IReadOnlyList<ArchitectureArtifact> KnownArtifacts { get; } = new[]
    {
        new ArchitectureArtifact("SKL-EXE-0004", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0005", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0002", "Execution"),
        new ArchitectureArtifact("ORC-EXE-0001", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0001", "Delivery"),
        new ArchitectureArtifact("SKL-EXE-0012", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0005", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0013", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0014", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0006", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0004", "Delivery"),
        new ArchitectureArtifact("SKL-EXE-0015", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0007", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0016", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0008", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0017", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0009", "Execution"),
    };
}
