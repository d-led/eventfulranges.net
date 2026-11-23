using Shouldly;

namespace EventfulRanges.Tests;

public sealed class RangeSetTests
{
    private static readonly EngineOptions Deterministic = new()
    {
        Clock = new LamportClock(),
        SnapshotEvery = 0,
    };

    private static T Run<T>(ValueTask<T> task) => task.AsTask().GetAwaiter().GetResult();

    private static void Run(ValueTask task) => task.AsTask().GetAwaiter().GetResult();

    [Fact]
    public void A_replica_keeps_what_it_applied_across_restarts()
    {
        using var directory = new TemporaryDirectory();

        using (var set = Run(RangeSet.OpenAsync(directory.DirectoryPath, StrategyKind.Lww, Deterministic)))
        {
            var added = Run(set.AddAsync(1, 10));
            added.Timestamp.ShouldBeGreaterThan(0);
            Run(set.RemoveAsync(3, 5));

            var expected = new[] { "[1,3)", "(5,10]" };
            set.Ranges.Select(interval => interval.ToString()).ShouldBe(expected);
            set.Contains(4).ShouldBeFalse();
        }

        using var reopened = Run(RangeSet.OpenAsync(directory.DirectoryPath, StrategyKind.Lww, Deterministic));

        var persisted = new[] { "[1,3)", "(5,10]" };
        reopened.Ranges.Select(interval => interval.ToString()).ShouldBe(persisted);
        reopened.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public void Adding_with_bounds_keeps_the_bounds()
    {
        using var set = Run(RangeSet.OpenStoreAsync(new MemoryLog(), StrategyKind.Lww, Deterministic));

        Run(set.AddWithBoundsAsync(1, 10, Bound.Closed, Bound.Open));

        set.Ranges.ShouldBe(new[] { new Interval(1, 10, Bound.Closed, Bound.Open) });
    }

    [Fact]
    public void Two_replicas_converge_by_exchanging_operations()
    {
        using var left = Run(RangeSet.OpenStoreAsync(new MemoryLog(), StrategyKind.AdditiveWins, Deterministic));
        using var right = Run(RangeSet.OpenStoreAsync(new MemoryLog(), StrategyKind.AdditiveWins, Deterministic));

        Run(left.AddAsync(1, 10));
        Run(right.AddAsync(5, 15));
        Run(right.RemoveAsync(12, 14));

        Run(left.ApplyAllAsync(right.Ops));
        Run(right.ApplyAllAsync(left.Ops));

        left.Ranges.ShouldBe(right.Ranges);
        left.Contains(13).ShouldBeFalse();
        left.Contains(11).ShouldBeTrue();
    }

    [Fact]
    public async Task The_facade_reports_its_strategy_and_answers_overlaps()
    {
        using var set = await RangeSet.OpenStoreAsync(new MemoryLog(), StrategyKind.Fww, Deterministic);
        await set.AddAsync(1, 10);

        set.Strategy.ShouldBe(StrategyKind.Fww);
        set.Overlaps(new Interval(9, 20)).ShouldBeTrue();
        set.Overlaps(new Interval(11, 20)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_prepared_operation_can_be_applied_later()
    {
        using var set = await RangeSet.OpenStoreAsync(new MemoryLog(), StrategyKind.Lww, Deterministic);
        var prepared = Op.Add(1, 5) with { Timestamp = 10 };

        var applied = await set.ApplyAsync(prepared);

        applied.Id.ShouldBe(prepared.Id);
        applied.Timestamp.ShouldBe(10);
        set.Ranges.ShouldBe(new[] { new Interval(1, 5) });
    }

    [Fact]
    public async Task Removing_with_bounds_keeps_them()
    {
        using var set = await RangeSet.OpenStoreAsync(new MemoryLog(), StrategyKind.Lww, Deterministic);
        await set.AddAsync(1, 10);

        await set.RemoveWithBoundsAsync(3, 5, Bound.Closed, Bound.Open);

        var expected = new[] { "[1,3)", "[5,10]" };
        set.Ranges.Select(interval => interval.ToString()).ShouldBe(expected);
    }

    [Fact]
    public async Task A_snapshot_and_a_compaction_can_be_requested_from_the_facade()
    {
        using var directory = new TemporaryDirectory();
        using var set = await RangeSet.OpenAsync(directory.DirectoryPath, StrategyKind.Lww, Deterministic);
        await set.AddAsync(1, 10);

        await set.SnapshotAsync();
        await set.CompactAsync();

        set.Ranges.ShouldBe(new[] { new Interval(1, 10) });
        var stream = JsonlLog.Open(directory.DirectoryPath).FilePath;
        var line = File.ReadAllLines(stream).ShouldHaveSingleItem();
        line.ShouldStartWith("{\"snapshot\":{\"version\":1,\"data\":");
    }
}
