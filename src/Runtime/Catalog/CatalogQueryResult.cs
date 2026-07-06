namespace Runtime.Catalog;

public sealed record CatalogQueryResult(bool Found, CatalogEntry? Entry)
{
    public static readonly CatalogQueryResult NotFound = new(false, null);

    public static CatalogQueryResult FoundResult(CatalogEntry entry) => new(true, entry);
}
