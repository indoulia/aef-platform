using Kernel.Governance;
using Kernel.Supervision;

namespace Kernel.Tests.Contracts;

public sealed class InstanceSupervisorDefaultContractTests : InstanceSupervisorContractTestsBase
{
    protected override IInstanceSupervisor CreateSupervisor(IGovernanceBoundary governanceBoundary) =>
        new InstanceSupervisor(governanceBoundary);
}
