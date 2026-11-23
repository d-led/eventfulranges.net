using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The n-dimensional engine is the range engine's counterpart: it owns the log's
/// version, stamps what it accepts, and materializes the cover a strategy decides.
/// </summary>
public sealed class BoxEngineTests
{
    private static readonly BoxEngineOptions Deterministic = new()
    {
        Clock = new LamportClock(),
    };

    [Fact]
    public void An_operation_is_stored_with_a_stamp_and_a_stamped_one_keeps_its_own()
    {
        using var engine = Open(StrategyKind.AdditiveWins);

        var stamped = Run(engine.ApplyAsync(BoxOp.Add([0], [5])));
        var kept = Run(engine.ApplyAsync(BoxOp.Add([10], [20]) with { Timestamp = 7 }));

        stamped.Timestamp.ShouldBeGreaterThan(0);
        kept.Timestamp.ShouldBe(7);
    }

    [Fact]
    public void Additions_and_removals_converge_under_the_strategy()
    {
        using var engine = Open(StrategyKind.AdditiveWins);

        Run(engine.ApplyAllAsync([BoxOp.Add([0], [10]), BoxOp.Remove([3], [5])]));

        engine.Materialize().ShouldBe([new Box([0], [3]), new Box([5], [10])]);
        engine.Contains([1]).ShouldBeTrue();
        engine.Contains([4]).ShouldBeFalse();
    }

    [Fact]
    public void The_canonicalizer_shapes_the_cover_it_returns()
    {
        using var engine = Open(StrategyKind.GrowOnly, new BoxEngineOptions
        {
            Clock = new LamportClock(),
            Canonicalizer = Boxes.MergeAdjacent,
        });

        Run(engine.ApplyAllAsync([
            BoxOp.AddWithBounds([0], [1], [Bound.Closed], [Bound.Closed]),
            BoxOp.AddWithBounds([1], [2], [Bound.Closed], [Bound.Closed]),
        ]));

        engine.Materialize().ShouldBe([new Box([0], [2], [Bound.Closed], [Bound.Closed])]);
    }

    [Fact]
    public void A_custom_metadata_join_is_used_when_boxes_combine()
    {
        var keepFirst = new MetaMerge((first, _) => first);
        using var engine = Open(StrategyKind.GrowOnly, new BoxEngineOptions
        {
            Clock = new LamportClock(),
            MetaMerge = keepFirst,
        });

        Run(engine.ApplyAllAsync([
            BoxOp.AddWithBounds([0], [10], [Bound.Closed], [Bound.Closed]) with { Id = "a" },
            BoxOp.AddWithBounds([2], [3], [Bound.Closed], [Bound.Closed]) with { Id = "b" },
        ]));

        engine.Materialize().ShouldHaveSingleItem().Meta.IsDefaultOrEmpty.ShouldBeTrue();
    }

    [Fact]
    public void An_operation_of_another_dimensionality_is_rejected()
    {
        using var engine = Open(StrategyKind.GrowOnly);
        Run(engine.ApplyAsync(BoxOp.Add([0, 0], [1, 1])));

        var thrown = Should.Throw<DimensionMismatchException>(
            () => Run(engine.ApplyAsync(BoxOp.Add([5], [9]))));

        thrown.Expected.ShouldBe(2);
        thrown.Actual.ShouldBe(1);
        engine.Materialize().ShouldBe([new Box([0, 0], [1, 1])]);
    }

    [Fact]
    public void A_duplicate_identifier_is_ignored()
    {
        var log = new BoxMemoryLog();
        using var engine = Open(StrategyKind.GrowOnly, log: log);
        var op = BoxOp.Add([0], [5]);

        Run(engine.ApplyAsync(op));
        Run(engine.ApplyAsync(op));

        engine.Ops.ShouldHaveSingleItem();
        log.Version.ShouldBe(1);
    }

    [Fact]
    public void A_reopened_engine_answers_the_same()
    {
        var log = new BoxMemoryLog();
        IReadOnlyList<Box> before;
        using (var first = Open(StrategyKind.AdditiveWins, log: log))
        {
            Run(first.ApplyAllAsync([BoxOp.Add([0], [10]), BoxOp.Remove([3], [5])]));
            before = first.Materialize();
        }

        using var second = Open(StrategyKind.AdditiveWins, log: log);

        second.Materialize().ShouldBe(before);
        second.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public void A_conflicting_append_surfaces_and_leaves_the_cover_untouched()
    {
        var log = new ContendedLog();
        using var engine = Open(StrategyKind.GrowOnly, log: log);

        Should.Throw<StoreConflictException>(() => Run(engine.ApplyAsync(BoxOp.Add([0], [5]))));

        engine.Materialize().ShouldBeEmpty();
    }

    [Fact]
    public void Operations_are_listed_by_identifier_and_can_be_looked_up()
    {
        using var engine = Open(StrategyKind.GrowOnly);
        Run(engine.ApplyAllAsync([
            BoxOp.Add([9], [10]) with { Id = "b" },
            BoxOp.Add([0], [1]) with { Id = "a" },
        ]));

        engine.Ops.Select(op => op.Id).ShouldBe(["a", "b"]);
        engine.Op("a").ShouldNotBeNull().Box.ShouldBe(new Box([0], [1]));
        engine.Op("missing").ShouldBeNull();
    }

    [Fact]
    public void The_cover_answers_whether_it_holds_a_point_or_touches_a_box()
    {
        using var engine = Open(StrategyKind.GrowOnly);
        Run(engine.ApplyAsync(BoxOp.Add([0, 0], [10, 10])));

        engine.Contains([5, 5]).ShouldBeTrue();
        engine.Contains([10, 0]).ShouldBeFalse();
        engine.Overlaps(new Box([9, 9], [20, 20])).ShouldBeTrue();
        engine.Overlaps(new Box([10, 0], [20, 10])).ShouldBeFalse();
    }

    [Fact]
    public void An_operation_that_is_not_well_formed_is_rejected_before_it_is_stored()
    {
        using var engine = Open(StrategyKind.GrowOnly);

        Should.Throw<InvalidBoxOpException>(
            () => Run(engine.ApplyAsync(BoxOp.Add([5], [1]))));

        engine.Ops.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_replica_on_a_stream_survives_a_restart()
    {
        using var directory = new TemporaryDirectory();
        IReadOnlyList<Box> before;

        using (var running = await BoxEngine.OpenAsync(
            BoxJsonlLog.Open(directory.DirectoryPath),
            StrategyKind.AdditiveWins,
            Deterministic))
        {
            await running.ApplyAllAsync([BoxOp.Add([0, 0], [10, 10]), BoxOp.Remove([3, 3], [5, 5])]);
            before = running.Materialize();
        }

        using var reopened = await BoxEngine.OpenAsync(
            BoxJsonlLog.Open(directory.DirectoryPath),
            StrategyKind.AdditiveWins,
            Deterministic);

        reopened.Materialize().ShouldBe(before);
        reopened.Ops.Count.ShouldBe(2);
    }

    private static BoxEngine Open(StrategyKind strategy, BoxEngineOptions? options = null, IBoxLog? log = null) =>
        Run(BoxEngine.OpenAsync(log ?? new BoxMemoryLog(), strategy, options ?? Deterministic));

    private static T Run<T>(ValueTask<T> task) => task.AsTask().GetAwaiter().GetResult();

    private static void Run(ValueTask task) => task.AsTask().GetAwaiter().GetResult();

    /// <summary>A log that is always a version behind, as a faster writer would leave it.</summary>
    private sealed class ContendedLog : IBoxLog
    {
        public long Version => 0;

        public ValueTask AppendAsync(
            long expectedVersion,
            IReadOnlyList<BoxOp> ops,
            CancellationToken cancellationToken = default) =>
            throw new StoreConflictException(expectedVersion, expectedVersion + 1);

        public ValueTask<BoxLogReadResult> ReadAsync(
            long fromVersion,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new BoxLogReadResult([], 0));
    }
}
