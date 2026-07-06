namespace Runtime.Catalog;

// Optional, pluggable — never a hard-coded Catalog dependency. Absent a
// configured provider, no entry ever carries a Contract-conformance result.
public interface IContractConformanceProvider
{
    string? GetConformanceResult(string id);
}
