using System.Diagnostics.Metrics;

namespace Adapters.Mcp.Tests;

// Epic M2-0001 Story S6: observability parity with RestRequestMetrics
// (Adapters.Rest) - same Meter/Counter/Histogram shape, "tool" replacing
// "path" as the dimension. Verified with a real MeterListener, the
// idiomatic way to test System.Diagnostics.Metrics code - not just "does
// not throw".
public class McpRequestMetricsTests : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<(string InstrumentName, long? LongValue, double? DoubleValue, IReadOnlyList<KeyValuePair<string, object?>> Tags)> _measurements = new();

    public McpRequestMetricsTests()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "AefPlatform.Adapters.Mcp")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            _measurements.Add((instrument.Name, measurement, null, tags.ToArray())));
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            _measurements.Add((instrument.Name, null, measurement, tags.ToArray())));
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void RecordRequest_ForSuccess_IncrementsRequestCount_AndRecordsDuration_ButNotErrorCount()
    {
        McpRequestMetrics.RecordRequest("aef_plan_engineering", isError: false, TimeSpan.FromMilliseconds(42));

        Assert.Contains(_measurements, m => m.InstrumentName == "aef.mcp.requests" && m.LongValue == 1);
        Assert.Contains(_measurements, m => m.InstrumentName == "aef.mcp.request_duration_ms" && m.DoubleValue == 42.0);
        Assert.DoesNotContain(_measurements, m => m.InstrumentName == "aef.mcp.errors");
    }

    [Fact]
    public void RecordRequest_ForError_AlsoIncrementsErrorCount()
    {
        McpRequestMetrics.RecordRequest("aef_validate_repository", isError: true, TimeSpan.FromMilliseconds(7));

        Assert.Contains(_measurements, m => m.InstrumentName == "aef.mcp.requests" && m.LongValue == 1);
        Assert.Contains(_measurements, m => m.InstrumentName == "aef.mcp.errors" && m.LongValue == 1);
    }

    [Fact]
    public void RecordRequest_TagsEachMeasurement_WithTheToolOrOperationName()
    {
        McpRequestMetrics.RecordRequest("tools/list", isError: false, TimeSpan.FromMilliseconds(3));

        var requestMeasurement = Assert.Single(_measurements, m => m.InstrumentName == "aef.mcp.requests");
        Assert.Contains(requestMeasurement.Tags, tag => tag.Key == "tool" && (string?)tag.Value == "tools/list");
    }
}
