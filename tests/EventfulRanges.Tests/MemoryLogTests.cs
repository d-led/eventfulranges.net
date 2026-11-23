using Shouldly;

namespace EventfulRanges.Tests;

public sealed class MemoryLogTests
{
    private static Task Append(MemoryLog log, long expectedVersion, params Op[] ops) =>
        log.AppendAsync(expectedVersion, ops).AsTask();

    [Fact]
    public async Task Appending_at_the_current_version_grows_the_log()
    {
        var log = new MemoryLog();

        await Append(log, 0, Op.Add(1, 2), Op.Add(3, 4));

        log.Version.ShouldBe(2);
    }

    [Fact]
    public async Task A_stale_expected_version_is_rejected_and_nothing_is_written()
    {
        var log = new MemoryLog();
        await Append(log, 0, Op.Add(1, 2));

        await Should.ThrowAsync<StoreConflictException>(() => Append(log, 0, Op.Add(3, 4)));

        log.Version.ShouldBe(1);
    }

    [Fact]
    public async Task A_conflict_reports_both_versions()
    {
        var log = new MemoryLog(new[] { Op.Add(1, 2) with { Id = "a" } });

        var conflict = await Should.ThrowAsync<StoreConflictException>(
            () => log.AppendAsync(5, new[] { Op.Add(3, 4) with { Id = "b" } }).AsTask());

        conflict.ExpectedVersion.ShouldBe(5);
        conflict.ActualVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Read_returns_the_operations_after_the_requested_version()
    {
        var log = new MemoryLog(new[] { Op.Add(1, 2), Op.Add(3, 4), Op.Add(5, 6) });

        var read = await log.ReadAsync(2);

        read.Version.ShouldBe(3);
        read.Ops.ShouldHaveSingleItem().Interval.ShouldBe(new Interval(5, 6));
    }

    [Fact]
    public async Task A_snapshot_round_trips_with_its_version()
    {
        var log = new MemoryLog();
        var payload = new byte[] { 1, 2, 3 };

        await log.SaveSnapshotAsync(payload, 7);
        var snapshot = await log.LoadSnapshotAsync();

        snapshot.Version.ShouldBe(7);
        snapshot.Data.ShouldBe(payload);
    }

    [Fact]
    public async Task Loading_without_a_snapshot_is_reported()
    {
        var log = new MemoryLog();

        await Should.ThrowAsync<SnapshotNotFoundException>(() => log.LoadSnapshotAsync().AsTask());
    }

    [Fact]
    public async Task Compaction_keeps_only_the_operations_the_snapshot_does_not_cover()
    {
        var log = new MemoryLog(new[] { Op.Add(1, 2), Op.Add(3, 4), Op.Add(5, 6) });
        var payload = new byte[] { 9 };

        await log.CompactAsync(payload, 1);

        log.Version.ShouldBe(2);
        (await log.LoadSnapshotAsync()).Version.ShouldBe(1);
    }
}
