// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
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
        new ArchitectureArtifact("AGT-EXE-0010", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0018", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0019", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0020", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0011", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0021", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0022", "Execution"),
        new ArchitectureArtifact("AGT-EXE-0012", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0023", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0024", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0025", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0006", "Delivery"),
        new ArchitectureArtifact("AGT-EXE-0013", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0026", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0007", "Delivery"),
        new ArchitectureArtifact("AGT-EXE-0014", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0027", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0028", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0029", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0005", "Delivery"),
        new ArchitectureArtifact("AGT-EXE-0015", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0030", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0031", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0032", "Execution"),
        new ArchitectureArtifact("SKL-EXE-0033", "Execution"),
        new ArchitectureArtifact("WFL-DEL-0008", "Delivery"),
        new ArchitectureArtifact("REPO-VALIDATION", "Execution"),
    };
}
