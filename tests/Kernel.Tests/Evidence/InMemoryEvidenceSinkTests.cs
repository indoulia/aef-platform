using Kernel.Evidence;

namespace Kernel.Tests.Evidence;

public class InMemoryEvidenceSinkTests
{
    [Fact]
    public void Record_SingleCall_AppearsInRecords()
    {
        var sink = new InMemoryEvidenceSink();
        var record = new EvidenceRecord("Contract", "Completed", "subject-1", null, DateTimeOffset.UtcNow);

        sink.Record(record);

        Assert.Single(sink.Records);
        Assert.Equal(record, sink.Records[0]);
    }

    [Fact]
    public void Record_ConcurrentCalls_AllRecordsAreCapturedWithoutCorruption()
    {
        var sink = new InMemoryEvidenceSink();
        const int threadCount = 20;
        const int recordsPerThread = 200;

        Parallel.For(0, threadCount, i =>
        {
            for (var j = 0; j < recordsPerThread; j++)
            {
                sink.Record(new EvidenceRecord("Contract", "Completed", $"subject-{i}-{j}", null, DateTimeOffset.UtcNow));
            }
        });

        Assert.Equal(threadCount * recordsPerThread, sink.Records.Count);
    }
}
