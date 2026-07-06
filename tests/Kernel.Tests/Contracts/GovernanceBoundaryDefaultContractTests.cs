using Kernel.Governance;
using Kernel.Registry;

namespace Kernel.Tests.Contracts;

public sealed class GovernanceBoundaryDefaultContractTests : GovernanceBoundaryContractTestsBase
{
    protected override IGovernanceBoundary CreateBoundary(IRegistry registry, IPolicyResolver? policyResolver) =>
        new GovernanceBoundary(registry, policyResolver);
}
