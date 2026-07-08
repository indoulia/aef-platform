using Kernel.Registry;

namespace Runtime.Catalog;

// A derived, read-only projection of the Runtime Registry (I3 §5) — never a
// second source of truth. Every query delegates live to the Registry; the
// Catalog holds no state of its own, so it can never disagree with it and
// never requires a restart to reflect a Registry change.
public sealed class RuntimeCatalog
{
    private readonly IRegistry _registry;
    private readonly IContractConformanceProvider? _conformanceProvider;

    public RuntimeCatalog(IRegistry registry, IContractConformanceProvider? conformanceProvider = null)
    {
        _registry = registry;
        _conformanceProvider = conformanceProvider;
    }

    public CatalogQueryResult Query(string id)
    {
        var result = _registry.Query(id);
        if (!result.Found)
        {
            return CatalogQueryResult.NotFound;
        }

        var entry = result.Entry!;
        var conformance = _conformanceProvider?.GetConformanceResult(id);
        return CatalogQueryResult.FoundResult(new CatalogEntry(entry.Id, entry.Domain, entry.Status, conformance));
    }

    public IReadOnlyCollection<CatalogEntry> All()
    {
        return _registry.All()
            .Select(entry => new CatalogEntry(entry.Id, entry.Domain, entry.Status, _conformanceProvider?.GetConformanceResult(entry.Id)))
            .ToList();
    }
}
