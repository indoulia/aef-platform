// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
using Kernel.Registry;
using Runtime.Bootstrapping;

namespace AefPlatform.Host;

public sealed record RuntimeArchitectureLoadReport(int Attempted, int Succeeded, IReadOnlyList<string> Failures)
{
    public bool IsValid => Failures.Count == 0;
}

// The "load the engineering architecture into memory" step: registers a
// known-good artifact set against the already-booted Runtime's Registry
// (Kernel.Registry.Register, unchanged from Epic 0001) and reports which
// artifacts, if any, failed to register — never overwrites, never retries,
// never silently drops a failure.
public static class RuntimeArchitectureLoader
{
    public static RuntimeArchitectureLoadReport Load(RuntimeHandle runtime, IReadOnlyList<ArchitectureArtifact> artifacts)
    {
        var seenInManifest = new HashSet<string>();
        var failures = new List<string>();

        foreach (var artifact in artifacts)
        {
            if (!seenInManifest.Add(artifact.Id))
            {
                failures.Add($"{artifact.Id}: duplicate Id within the Engineering Architecture Manifest.");
                continue;
            }

            var result = runtime.Kernel.Registry.Register(new DefinitionSubmission(artifact.Id, artifact.Domain));
            if (result.Outcome != RegisterOutcome.Success)
            {
                failures.Add($"{artifact.Id}: {result.Reason}");
            }
        }

        return new RuntimeArchitectureLoadReport(artifacts.Count, artifacts.Count - failures.Count, failures);
    }
}
