namespace Kernel.Registry;

public sealed record QueryResult(bool Found, RegistryEntry? Entry)
{
    public static QueryResult FoundResult(RegistryEntry entry) => new(true, entry);

    public static readonly QueryResult NotFound = new(false, null);
}
