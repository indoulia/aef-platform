namespace Runtime.Agent.Health;

public static class RuntimeHealthAggregator
{
    public static RuntimeHealthReport Aggregate(IEnumerable<IRuntimeHealthContributor> contributors)
    {
        var reports = contributors.Select(c => c.Check()).ToList();
        if (reports.Count == 0)
        {
            return new RuntimeHealthReport("Aggregate", RuntimeHealthStatus.Healthy, null);
        }

        var worstStatus = reports.Max(r => r.Status);
        var reasons = reports.Where(r => r.Status != RuntimeHealthStatus.Healthy).Select(r => $"{r.Name}: {r.Reason}").ToList();
        return new RuntimeHealthReport("Aggregate", worstStatus, reasons.Count > 0 ? string.Join("; ", reasons) : null);
    }
}
