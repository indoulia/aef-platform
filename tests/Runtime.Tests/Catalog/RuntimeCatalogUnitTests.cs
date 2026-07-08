using Kernel.Registry;
using Runtime.Catalog;

namespace Runtime.Tests.Catalog;

// S9 — Runtime Catalog (Construction Unit 15, I3 §5).
public class RuntimeCatalogUnitTests
{
    [Fact]
    public void Query_ForRegisteredActiveDefinition_ReturnsIdDomainStatus()
    {
        var registry = new InMemoryRegistry(new Kernel.Evidence.InMemoryEvidenceSink());
        registry.Register(new DefinitionSubmission("skill-1", "domain-skills"));
        var catalog = new RuntimeCatalog(registry);

        var result = catalog.Query("skill-1");

        Assert.True(result.Found);
        Assert.Equal("skill-1", result.Entry!.Id);
        Assert.Equal("domain-skills", result.Entry.Domain);
        Assert.Equal(RegistryEntryStatus.Active, result.Entry.Status);
    }

    [Fact]
    public void Query_ForUnregisteredDefinition_ReturnsNotFound()
    {
        var registry = new InMemoryRegistry(new Kernel.Evidence.InMemoryEvidenceSink());
        var catalog = new RuntimeCatalog(registry);

        var result = catalog.Query("skill-unregistered");

        Assert.False(result.Found);
    }

    [Fact]
    public void Query_ReflectsRegistryChanges_WithoutRestart()
    {
        var registry = new InMemoryRegistry(new Kernel.Evidence.InMemoryEvidenceSink());
        var catalog = new RuntimeCatalog(registry);

        Assert.False(catalog.Query("agent-1").Found);

        registry.Register(new DefinitionSubmission("agent-1", "domain-agents"));

        Assert.True(catalog.Query("agent-1").Found);
    }

    [Fact]
    public void Query_NeverOriginatesState_RegistryIsAuthoritative()
    {
        var fakeRegistry = new FakeRegistry(new QueryResult(true, new RegistryEntry("strategy-1", "domain-strategies", RegistryEntryStatus.Active)));
        var catalog = new RuntimeCatalog(fakeRegistry);

        var result = catalog.Query("strategy-1");

        Assert.Equal(fakeRegistry.NextResult.Entry!.Id, result.Entry!.Id);
        Assert.Equal(fakeRegistry.NextResult.Entry.Domain, result.Entry.Domain);
        Assert.Equal(fakeRegistry.NextResult.Entry.Status, result.Entry.Status);
    }

    [Fact]
    public void Query_WithConformanceProviderConfigured_AttachesContractConformanceResult()
    {
        var registry = new InMemoryRegistry(new Kernel.Evidence.InMemoryEvidenceSink());
        registry.Register(new DefinitionSubmission("strategy-1", "domain-strategies"));
        var catalog = new RuntimeCatalog(registry, new FixedConformanceProvider("strategy-1", "Conformant"));

        var result = catalog.Query("strategy-1");

        Assert.Equal("Conformant", result.Entry!.ContractConformanceResult);
    }

    [Fact]
    public void Query_WithNoConformanceProviderConfigured_ContractConformanceResultIsNull()
    {
        var registry = new InMemoryRegistry(new Kernel.Evidence.InMemoryEvidenceSink());
        registry.Register(new DefinitionSubmission("skill-1", "domain-skills"));
        var catalog = new RuntimeCatalog(registry);

        var result = catalog.Query("skill-1");

        Assert.Null(result.Entry!.ContractConformanceResult);
    }

    private sealed class FakeRegistry : IRegistry
    {
        public QueryResult NextResult { get; }

        public FakeRegistry(QueryResult nextResult) => NextResult = nextResult;

        public RegisterResult Register(DefinitionSubmission submission) => throw new NotSupportedException();

        public QueryResult Query(string id) => NextResult;

        public IReadOnlyCollection<RegistryEntry> All() => throw new NotSupportedException();
    }

    private sealed class FixedConformanceProvider : IContractConformanceProvider
    {
        private readonly string _id;
        private readonly string _result;

        public FixedConformanceProvider(string id, string result)
        {
            _id = id;
            _result = result;
        }

        public string? GetConformanceResult(string id) => id == _id ? _result : null;
    }
}
