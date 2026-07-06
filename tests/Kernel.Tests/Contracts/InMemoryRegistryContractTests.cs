using Kernel.Registry;

namespace Kernel.Tests.Contracts;

public sealed class InMemoryRegistryContractTests : RegistryContractTestsBase
{
    protected override IRegistry CreateRegistry() => new InMemoryRegistry();
}
