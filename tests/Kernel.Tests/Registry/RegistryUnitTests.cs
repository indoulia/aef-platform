using Kernel.Evidence;
using Kernel.Registry;
using Xunit;

namespace Kernel.Tests.Registry;

public class RegistryUnitTests
{
    [Fact]
    public void Register_WellFormedSubmission_StoresAsActiveAndReturnsSuccess()
    {
        var registry = new InMemoryRegistry();

        var result = registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        Assert.Equal(RegisterOutcome.Success, result.Outcome);
        Assert.NotNull(result.Entry);
        Assert.Equal("def-1", result.Entry!.Id);
        Assert.Equal("domain-a", result.Entry.Domain);
        Assert.Equal(RegistryEntryStatus.Active, result.Entry.Status);
    }

    [Fact]
    public void Query_PreviouslyRegisteredId_ReturnsIdDomainStatus()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        var result = registry.Query("def-1");

        Assert.True(result.Found);
        Assert.NotNull(result.Entry);
        Assert.Equal("def-1", result.Entry!.Id);
        Assert.Equal("domain-a", result.Entry.Domain);
        Assert.Equal(RegistryEntryStatus.Active, result.Entry.Status);
    }

    [Fact]
    public void Query_UnknownId_ReturnsNotFound()
    {
        var registry = new InMemoryRegistry();

        var result = registry.Query("does-not-exist");

        Assert.False(result.Found);
        Assert.Null(result.Entry);
    }

    [Fact]
    public void Register_DuplicateId_DeterministicallyRejectedWithoutOverwrite()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        var second = registry.Register(new DefinitionSubmission("def-1", "domain-b"));

        Assert.Equal(RegisterOutcome.RejectedDuplicate, second.Outcome);
        Assert.Null(second.Entry);

        // No silent overwrite: the original entry is unchanged.
        var queried = registry.Query("def-1");
        Assert.True(queried.Found);
        Assert.Equal("domain-a", queried.Entry!.Domain);
    }

    [Theory]
    [InlineData("", "domain-a")]
    [InlineData(" ", "domain-a")]
    [InlineData("def-1", "")]
    [InlineData("def-1", " ")]
    public void Register_MalformedSubmission_RejectedMalformed(string id, string domain)
    {
        var registry = new InMemoryRegistry();

        var result = registry.Register(new DefinitionSubmission(id, domain));

        Assert.Equal(RegisterOutcome.RejectedMalformed, result.Outcome);
        Assert.Null(result.Entry);
    }

    [Fact]
    public void Register_Success_RecordsAcceptDecisionOnEvidenceSink()
    {
        var sink = new FakeEvidenceSink();
        var registry = new InMemoryRegistry(sink);

        registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        var record = Assert.Single(sink.Records);
        Assert.Equal("Registration", record.Contract);
        Assert.Equal("Accept", record.Decision);
        Assert.Equal("def-1", record.SubjectId);
    }

    [Fact]
    public void Register_Duplicate_RecordsRejectDecisionOnEvidenceSink()
    {
        var sink = new FakeEvidenceSink();
        var registry = new InMemoryRegistry(sink);
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));

        registry.Register(new DefinitionSubmission("def-1", "domain-b"));

        Assert.Equal(2, sink.Records.Count);
        var rejection = sink.Records[1];
        Assert.Equal("Registration", rejection.Contract);
        Assert.Equal("Reject", rejection.Decision);
        Assert.Equal("def-1", rejection.SubjectId);
        Assert.NotNull(rejection.Reason);
    }

    [Fact]
    public void Register_Malformed_RecordsRejectDecisionOnEvidenceSink()
    {
        var sink = new FakeEvidenceSink();
        var registry = new InMemoryRegistry(sink);

        registry.Register(new DefinitionSubmission("", "domain-a"));

        var record = Assert.Single(sink.Records);
        Assert.Equal("Registration", record.Contract);
        Assert.Equal("Reject", record.Decision);
        Assert.NotNull(record.Reason);
    }

    [Fact]
    public void All_NoEntries_ReturnsEmpty()
    {
        var registry = new InMemoryRegistry();

        Assert.Empty(registry.All());
    }

    [Fact]
    public void All_AfterRegistrations_ReturnsEveryEntry()
    {
        var registry = new InMemoryRegistry();
        registry.Register(new DefinitionSubmission("def-1", "domain-a"));
        registry.Register(new DefinitionSubmission("def-2", "domain-b"));
        registry.Register(new DefinitionSubmission("def-1", "domain-c")); // duplicate, rejected — must not appear twice

        var all = registry.All();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Id == "def-1" && e.Domain == "domain-a");
        Assert.Contains(all, e => e.Id == "def-2" && e.Domain == "domain-b");
    }

    [Fact]
    public void Register_ConcurrentDuplicateSubmissions_ExactlyOneSucceeds()
    {
        // Regression: a plain Dictionary guarded by a ContainsKey-then-index
        // check lets two racing Register calls for the same Id both "win",
        // defeating the no-silent-overwrite guarantee - only possible to
        // observe under genuine concurrency, not sequential calls.
        var registry = new InMemoryRegistry();
        const int attempts = 50;

        var results = new RegisterResult[attempts];
        Parallel.For(0, attempts, i =>
        {
            results[i] = registry.Register(new DefinitionSubmission("def-race", $"domain-{i}"));
        });

        Assert.Single(results, r => r.Outcome == RegisterOutcome.Success);
        Assert.Equal(attempts - 1, results.Count(r => r.Outcome == RegisterOutcome.RejectedDuplicate));
        Assert.Single(registry.All());
    }

    [Fact]
    public void Register_ManyDistinctIdsConcurrently_AllSucceedAndAllAreQueryable()
    {
        var registry = new InMemoryRegistry();
        const int count = 200;

        Parallel.For(0, count, i => registry.Register(new DefinitionSubmission($"def-{i}", "domain-a")));

        Assert.Equal(count, registry.All().Count);
        for (var i = 0; i < count; i++)
        {
            Assert.True(registry.Query($"def-{i}").Found);
        }
    }

    private sealed class FakeEvidenceSink : IEvidenceSink
    {
        public List<EvidenceRecord> Records { get; } = new();

        public void Record(EvidenceRecord record) => Records.Add(record);
    }
}
