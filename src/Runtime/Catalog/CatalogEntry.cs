using Kernel.Registry;

namespace Runtime.Catalog;

// The content-category level I3 §5 fixes: Id, owning Domain, Lifecycle
// status, and — only when a Contract-conformance provider supplies one,
// e.g. for a Strategy specifically — its Contract-conformance result.
public sealed record CatalogEntry(string Id, string Domain, RegistryEntryStatus Status, string? ContractConformanceResult);
