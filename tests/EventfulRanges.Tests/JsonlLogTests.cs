using System.Text;
using Shouldly;

namespace EventfulRanges.Tests;

public sealed class JsonlLogTests
{
    private static Task Append(JsonlLog log, long expectedVersion, params Op[] ops) =>
        log.AppendAsync(expectedVersion, ops).AsTask();

    [Fact]
    public async Task Operations_round_trip_through_the_stream()
    {
        using var directory = new TemporaryDirectory();
        var op = Op.Add(1, 5) with { Id = "a", Timestamp = 7, Origin = "test" };
        await Append(JsonlLog.Open(directory.DirectoryPath), 0, op);

        var read = await JsonlLog.Open(directory.DirectoryPath).ReadAsync(0);

        read.Version.ShouldBe(1);
        read.Ops.ShouldHaveSingleItem().ShouldBe(op);
    }

    [Fact]
    public async Task A_fresh_directory_reads_as_an_empty_stream()
    {
        using var directory = new TemporaryDirectory();

        var read = await JsonlLog.Open(directory.DirectoryPath).ReadAsync(0);

        read.Version.ShouldBe(0);
        read.Ops.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_stream_uses_the_wire_names_and_omits_the_null_origin()
    {
        using var directory = new TemporaryDirectory();
        var log = JsonlLog.Open(directory.DirectoryPath);
        await Append(log, 0, Op.Add(1, 5, Bound.Closed, Bound.Open) with { Id = "a", Timestamp = 7 });

        var line = File.ReadAllLines(log.FilePath).ShouldHaveSingleItem();

        line.ShouldContain("\"kind\":\"add\"");
        line.ShouldContain("\"startBound\":\"closed\"");
        line.ShouldContain("\"endBound\":\"open\"");
        line.ShouldContain("\"ts\":7");
        line.ShouldNotContain("origin");
        line.ShouldNotContain("isValid");
        line.ShouldNotContain("isEmpty");
    }

    [Fact]
    public async Task A_stale_writer_is_rejected_because_the_version_comes_from_the_file()
    {
        using var directory = new TemporaryDirectory();
        var first = JsonlLog.Open(directory.DirectoryPath);
        var second = JsonlLog.Open(directory.DirectoryPath);
        await Append(first, 0, Op.Add(1, 2) with { Id = "a" });

        await Should.ThrowAsync<StoreConflictException>(() => Append(second, 0, Op.Add(3, 4) with { Id = "b" }));

        (await first.ReadAsync(0)).Version.ShouldBe(1);
    }

    [Fact]
    public async Task A_snapshot_is_embedded_in_the_stream_and_round_trips_byte_for_byte()
    {
        using var directory = new TemporaryDirectory();
        var log = JsonlLog.Open(directory.DirectoryPath);
        var payload = Encoding.UTF8.GetBytes("""{"strategy":"lww","version":0,"intervals":[],"ops":[]}""");

        await log.SaveSnapshotAsync(payload, 3);
        var snapshot = await JsonlLog.Open(directory.DirectoryPath).LoadSnapshotAsync();

        snapshot.Version.ShouldBe(3);
        snapshot.Data.ShouldBe(payload);
        File.ReadAllLines(log.FilePath).ShouldHaveSingleItem()
            .ShouldStartWith("""{"snapshot":{"version":3,"data":""");
    }

    [Fact]
    public async Task Loading_without_a_snapshot_is_reported()
    {
        using var directory = new TemporaryDirectory();

        await Should.ThrowAsync<SnapshotNotFoundException>(
            () => JsonlLog.Open(directory.DirectoryPath).LoadSnapshotAsync().AsTask());
    }

    [Fact]
    public async Task Compaction_rewrites_the_stream_as_a_snapshot_plus_the_operations_it_does_not_cover()
    {
        using var directory = new TemporaryDirectory();
        var log = JsonlLog.Open(directory.DirectoryPath);
        await Append(log, 0, Op.Add(1, 2) with { Id = "a" });
        await Append(log, 1, Op.Add(3, 4) with { Id = "b" });
        await Append(log, 2, Op.Add(5, 6) with { Id = "c" });
        var payload = Encoding.UTF8.GetBytes("""{"strategy":"lww","version":1,"intervals":[],"ops":[]}""");

        await log.CompactAsync(payload, 1);

        File.ReadAllLines(log.FilePath).Length.ShouldBe(3);
        var reopened = JsonlLog.Open(directory.DirectoryPath);
        var expectedIds = new[] { "b", "c" };
        (await reopened.ReadAsync(0)).Ops.Select(op => op.Id).ShouldBe(expectedIds);
        (await reopened.LoadSnapshotAsync()).Data.ShouldBe(payload);
    }
}
