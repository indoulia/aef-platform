using System.Diagnostics.Metrics;

namespace Adapters.Mcp;

// Epic M2-0001 Story S6: observability parity with Adapters.Rest's own
// RestRequestMetrics.cs - same Meter/Counter/Histogram shape ("aef.mcp.*"
// mirroring "aef.rest.*"), "tool" replacing "path" as the one dimension,
// IsError (already computed by every WithCallToolHandler/
// WithListToolsHandler path) replacing REST's status-code-range check -
// a simpler, more direct signal already available on this channel.
public static class McpRequestMetrics
{
    private static readonly Meter Meter = new("AefPlatform.Adapters.Mcp");
    private static readonly Counter<long> RequestCount = Meter.CreateCounter<long>("aef.mcp.requests");
    private static readonly Counter<long> ErrorCount = Meter.CreateCounter<long>("aef.mcp.errors");
    private static readonly Histogram<double> RequestLatencyMs = Meter.CreateHistogram<double>("aef.mcp.request_duration_ms");

    public static void RecordRequest(string toolOrOperationName, bool isError, TimeSpan elapsed)
    {
        var tags = new KeyValuePair<string, object?>("tool", toolOrOperationName);
        RequestCount.Add(1, tags);
        RequestLatencyMs.Record(elapsed.TotalMilliseconds, tags);
        if (isError)
        {
            ErrorCount.Add(1, tags);
        }
    }
}
