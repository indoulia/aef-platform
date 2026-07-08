using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;

namespace Adapters.Rest;

public static class RestRequestMetrics
{
    private static readonly Meter Meter = new("AefPlatform.Adapters.Rest");
    private static readonly Counter<long> RequestCount = Meter.CreateCounter<long>("aef.rest.requests");
    private static readonly Counter<long> ErrorCount = Meter.CreateCounter<long>("aef.rest.errors");
    private static readonly Histogram<double> RequestLatencyMs = Meter.CreateHistogram<double>("aef.rest.request_duration_ms");

    public static void RecordRequest(PathString path, int statusCode, TimeSpan elapsed)
    {
        var tags = new KeyValuePair<string, object?>("path", path.Value);
        RequestCount.Add(1, tags);
        RequestLatencyMs.Record(elapsed.TotalMilliseconds, tags);
        if (statusCode >= 500 || statusCode == StatusCodes.Status400BadRequest || statusCode == StatusCodes.Status403Forbidden)
        {
            ErrorCount.Add(1, tags);
        }
    }
}
