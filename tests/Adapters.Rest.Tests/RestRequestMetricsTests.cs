using System.Diagnostics.Metrics;

namespace Adapters.Rest.Tests;

public class RestRequestMetricsTests
{
    [Fact]
    public void RecordRequest_EmitsARequestCountMeasurement()
    {
        long? observedCount = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "AefPlatform.Adapters.Rest" && instrument.Name == "aef.rest.requests")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => observedCount = measurement);
        listener.Start();

        RestRequestMetrics.RecordRequest("/api/v1/health", 200, TimeSpan.FromMilliseconds(5));

        Assert.Equal(1, observedCount);
    }
}
