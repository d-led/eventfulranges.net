using Shouldly;

namespace EventfulRanges.Kurrent.Tests;

/// <summary>
/// The point of a store is a replica that outlives the process. These tests drive
/// the range facade over real KurrentDB streams, so the log's version, conflict
/// and snapshot behaviour is exercised as an engine sees it rather than as a
/// stream dump.
/// </summary>
public sealed class ReplicaOverKurrentTests
{
    [KurrentFact]
    public async Task A_replica_over_kurrentdb_answers_the_same_after_a_restart()
    {
        using var writer = OpenIsolated();
        IReadOnlyList<Interval> before;

        using (var replica = await RangeSet.OpenStoreAsync(writer, StrategyKind.Lww))
        {
            await replica.AddAsync(0, 10);
            await replica.RemoveAsync(3, 5);
            before = replica.Ranges;
        }

        using var readerLog = KurrentLog.Open(
            KurrentFactAttribute.Connection!,
            writer.OperationStream,
            writer.SnapshotStream);
        using var reopened = await RangeSet.OpenStoreAsync(readerLog, StrategyKind.Lww);

        reopened.Ranges.ShouldBe(before);
        reopened.Contains(7).ShouldBeTrue();
    }

    [KurrentFact]
    public async Task A_replica_that_snapshotted_replays_only_the_tail()
    {
        using var writer = OpenIsolated();
        IReadOnlyList<Interval> before;

        using (var replica = await RangeSet.OpenStoreAsync(writer, StrategyKind.Lww))
        {
            await replica.AddAsync(0, 10);
            await replica.RemoveAsync(3, 5);
            before = replica.Ranges;
            await replica.SnapshotAsync();
        }

        (await writer.LoadSnapshotAsync()).Version.ShouldBe(2);

        using var readerLog = KurrentLog.Open(
            KurrentFactAttribute.Connection!,
            writer.OperationStream,
            writer.SnapshotStream);
        using var reopened = await RangeSet.OpenStoreAsync(readerLog, StrategyKind.Lww);

        reopened.Ranges.ShouldBe(before);
    }

    [KurrentFact]
    public async Task Compacting_a_stream_backed_log_says_it_cannot()
    {
        using var log = OpenIsolated();
        using var replica = await RangeSet.OpenStoreAsync(log, StrategyKind.Lww);
        await replica.AddAsync(0, 10);

        await Should.ThrowAsync<NotSupportedException>(async () => await replica.CompactAsync());
    }

    [KurrentFact]
    public async Task A_replica_that_loses_the_race_catches_up_instead_of_failing()
    {
        using var first = OpenIsolated();
        using var second = KurrentLog.Open(
            KurrentFactAttribute.Connection!,
            first.OperationStream,
            first.SnapshotStream);

        using var left = await RangeSet.OpenStoreAsync(first, StrategyKind.Lww);
        using var right = await RangeSet.OpenStoreAsync(second, StrategyKind.Lww);

        await left.AddAsync(0, 5);
        await right.AddAsync(5, 10);

        using var readerLog = KurrentLog.Open(
            KurrentFactAttribute.Connection!,
            first.OperationStream,
            first.SnapshotStream);
        using var reopened = await RangeSet.OpenStoreAsync(readerLog, StrategyKind.Lww);
        reopened.Ranges.ShouldBe([new Interval(0, 10)]);
    }

    private static KurrentLog OpenIsolated()
    {
        var suffix = Guid.NewGuid().ToString("n");
        return KurrentLog.Open(KurrentFactAttribute.Connection!, $"itest-ops-{suffix}", $"itest-snap-{suffix}");
    }
}
