// Physically lives in src/Composition/AefPlatform.Composition.csproj -
// namespace unchanged from its prior home in src/Host, see Capability 3's
// design Decision 1.
namespace AefPlatform.Host;

public sealed record ArchitectureArtifact(string Id, string Domain);
