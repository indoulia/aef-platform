using Runtime.Agent.Health;

namespace Runtime.Agent.Tests.Health;

public class RuntimeHealthAggregatorTests
{
    private sealed class FixedContributor : IRuntimeHealthContributor
    {
        private readonly RuntimeHealthReport _report;
        public FixedContributor(RuntimeHealthReport report) => _report = report;
        public RuntimeHealthReport Check() => _report;
    }

    [Fact]
    public void Aggregate_NoContributors_ReturnsHealthy()
    {
        var result = RuntimeHealthAggregator.Aggregate(Array.Empty<IRuntimeHealthContributor>());

        Assert.Equal(RuntimeHealthStatus.Healthy, result.Status);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Aggregate_AllHealthy_ReturnsHealthy()
    {
        var contributors = new[]
        {
            new FixedContributor(new RuntimeHealthReport("A", RuntimeHealthStatus.Healthy, null)),
            new FixedContributor(new RuntimeHealthReport("B", RuntimeHealthStatus.Healthy, null)),
        };

        var result = RuntimeHealthAggregator.Aggregate(contributors);

        Assert.Equal(RuntimeHealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Aggregate_OneUnhealthy_ReturnsUnhealthyWithReasonNamingIt()
    {
        var contributors = new[]
        {
            new FixedContributor(new RuntimeHealthReport("A", RuntimeHealthStatus.Healthy, null)),
            new FixedContributor(new RuntimeHealthReport("B", RuntimeHealthStatus.Unhealthy, "down")),
        };

        var result = RuntimeHealthAggregator.Aggregate(contributors);

        Assert.Equal(RuntimeHealthStatus.Unhealthy, result.Status);
        Assert.Contains("B: down", result.Reason);
    }
}
