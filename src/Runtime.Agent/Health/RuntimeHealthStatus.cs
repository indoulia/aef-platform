namespace Runtime.Agent.Health;

// Declaration order is load-bearing: RuntimeHealthAggregator.Aggregate
// uses the default enum ordinal ordering to pick the worst status via
// Enumerable.Max. Reordering these members changes behavior, not just
// cosmetics.
public enum RuntimeHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}
