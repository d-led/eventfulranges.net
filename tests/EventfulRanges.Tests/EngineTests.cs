using Shouldly;

namespace EventfulRanges.Tests;

public sealed class EngineTests
{
    private static readonly EngineOptions FastSnapshots = new()
    {
        Clock = new LamportClock(),
        SnapshotEvery = 1,
    };

    private static readonly EngineOptions NoAutoSnapshots = new()
    {
        Clock = new LamportClock(),
        SnapshotEvery = 0,
    };

    private static T Run<T>(ValueTask<T> task) => task.AsTask().GetAwaiter().GetResult();

    private static void Run(ValueTask task) => task.AsTask().GetAwaiter().GetResult();

    private static Engine Open(IEventLog log, StrategyKind strategy, EngineOptions options) =>
        Run(Engine.OpenAsync(log, strategy, options));

    [Fact]
    public void Applying_an_addition_puts_it_in_the_view()
    {
        using var engine = Open(new MemoryLog(), StrategyKind.Lww, NoAutoSnapshots);

        Run(engine.ApplyAsync(Op.Add(1, 10)));

        engine.Materialize().ShouldBe(new[] { new Interval(1, 10) });
        engine.Contains(5).ShouldBeTrue();
        engine.Contains(11).ShouldBeFalse();
    }

    [Fact]
    public void Applying_a_removal_cuts_the_view()
    {
        using var engine = Open(new MemoryLog(), StrategyKind.Lww, NoAutoSnapshots);

        Run(engine.ApplyAsync(Op.Add(1, 10)));
        Run(engine.ApplyAsync(Op.Remove(3, 5)));

        var expected = new[] { "[1,3)", "(5,10]" };
        engine.Materialize().Select(interval => interval.ToString()).ShouldBe(expected);
    }

    [Fact]
    public void Applying_returns_the_operation_as_stored_with_a_timestamp()
    {
        using var engine = Open(new MemoryLog(), StrategyKind.Lww, NoAutoSnapshots);
        var unstamped = Op.Add(1, 5);

        var stored = Run(engine.ApplyAsync(unstamped));

        stored.Id.ShouldBe(unstamped.Id);
        stored.Timestamp.ShouldBeGreaterThan(0);
        engine.Ops.ShouldHaveSingleItem().Timestamp.ShouldBe(stored.Timestamp);
    }

    [Fact]
    public void A_duplicate_identifier_is_ignored()
    {
        var log = new MemoryLog();
        using var engine = Open(log, StrategyKind.Lww, NoAutoSnapshots);
        var op = Op.Add(1, 5);

        Run(engine.ApplyAsync(op));
        Run(engine.ApplyAsync(op));

        engine.Ops.ShouldHaveSingleItem();
        log.Version.ShouldBe(1);
    }

    [Fact]
    public void A_malformed_operation_is_rejected_before_it_reaches_the_log()
    {
        var log = new MemoryLog();
        using var engine = Open(log, StrategyKind.Lww, NoAutoSnapshots);

        Should.Throw<InvalidOpException>(() => Run(engine.ApplyAsync(new Op("", OpKind.Add, new Interval(1, 2)))));

        log.Version.ShouldBe(0);
    }

    [Fact]
    public void Operations_are_listed_in_identifier_order()
    {
        using var engine = Open(new MemoryLog(), StrategyKind.Lww, NoAutoSnapshots);
        Run(engine.ApplyAsync(Op.Add(1, 2) with { Id = "c" }));
        Run(engine.ApplyAsync(Op.Add(1, 2) with { Id = "a" }));
        Run(engine.ApplyAsync(Op.Add(1, 2) with { Id = "b" }));

        var expectedOrder = new[] { "a", "b", "c" };
        engine.Ops.Select(op => op.Id).ShouldBe(expectedOrder);
    }

    [Fact]
    public void An_append_that_lost_the_race_is_retried_after_catching_up()
    {
        var log = new RacingLog();
        using var engine = Open(log, StrategyKind.Lww, NoAutoSnapshots);

        Run(engine.ApplyAsync(Op.Add(1, 10)));

        engine.Contains(5).ShouldBeTrue();
        engine.Contains(150).ShouldBeTrue();
        log.Version.ShouldBe(2);
    }

    [Fact]
    public void The_engine_reports_the_strategy_it_opened_with()
    {
        using var engine = Open(new MemoryLog(), StrategyKind.Fww, NoAutoSnapshots);

        engine.Strategy.ShouldBe(StrategyKind.Fww);
    }

    [Fact]
    public void Overlaps_answers_from_the_materialized_view()
    {
        using var engine = Open(new MemoryLog(), StrategyKind.Lww, NoAutoSnapshots);
        Run(engine.ApplyAsync(Op.Add(1, 10)));

        engine.Overlaps(new Interval(5, 15)).ShouldBeTrue();
        engine.Overlaps(new Interval(11, 15)).ShouldBeFalse();
    }

    [Fact]
    public void Snapshotting_on_demand_writes_the_current_version()
    {
        var log = new RecordingLog();
        using var engine = Open(log, StrategyKind.Lww, NoAutoSnapshots);
        Run(engine.ApplyAsync(Op.Add(1, 10)));

        Run(engine.SnapshotAsync());

        log.SavedSnapshotVersion.ShouldBe(1);
    }

    [Fact]
    public void A_compatible_snapshot_fast_forwards_the_replay()
    {
        var log = new RecordingLog();
        using (var writer = Open(log, StrategyKind.AdditiveWins, FastSnapshots))
        {
            Run(writer.ApplyAsync(Op.Add(1, 10)));
            Run(writer.ApplyAsync(Op.Remove(3, 5)));
        }

        log.LastReadFromVersion = -1;
        using var reader = Open(log, StrategyKind.AdditiveWins, NoAutoSnapshots);

        log.LastReadFromVersion.ShouldBe(2);
        reader.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public void Compaction_keeps_the_view_and_the_operations()
    {
        var log = new MemoryLog();
        using var engine = Open(log, StrategyKind.Lww, NoAutoSnapshots);
        Run(engine.ApplyAsync(Op.Add(1, 10)));
        Run(engine.ApplyAsync(Op.Remove(3, 5)));
        var before = engine.Materialize();

        Run(engine.CompactAsync());

        using var reopened = Open(log, StrategyKind.Lww, NoAutoSnapshots);
        Intervals.Equal(before, reopened.Materialize()).ShouldBeTrue();
        reopened.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public void Compaction_needs_a_log_that_can_compact()
    {
        using var engine = Open(new MinimalLog(), StrategyKind.Lww, NoAutoSnapshots);

        Should.Throw<NotSupportedException>(() => Run(engine.CompactAsync()));
    }

    [Fact]
    public async Task Concurrent_writers_to_one_engine_converge_on_one_answer()
    {
        using var engine = Open(new MemoryLog(), StrategyKind.AdditiveWins, NoAutoSnapshots);
        var ops = Enumerable.Range(0, 40)
            .Select(index => Op.Add(index, index + 1))
            .ToList();

        await Task.WhenAll(ops.Select(op => Task.Run(async () => await engine.ApplyAsync(op))));

        engine.Ops.Count.ShouldBe(40);
        engine.Materialize().ShouldBe(new[] { new Interval(0, 40) });
        engine.Contains(40).ShouldBeTrue();
    }

    /// <summary>A log that lets another writer slip in before the first append.</summary>
    private sealed class RacingLog : IEventLog
    {
        private readonly MemoryLog inner = new();
        private bool raced;

        public long Version => inner.Version;

        public async ValueTask AppendAsync(
            long expectedVersion,
            IReadOnlyList<Op> ops,
            CancellationToken cancellationToken = default)
        {
            if (!raced)
            {
                raced = true;
                await inner.AppendAsync(
                    expectedVersion,
                    new[] { Op.Add(100, 200) with { Id = "other", Timestamp = 5 } },
                    CancellationToken.None);
            }

            await inner.AppendAsync(expectedVersion, ops, cancellationToken);
        }

        public ValueTask<LogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(fromVersion, cancellationToken);
    }

    /// <summary>A store that remembers which version the last read started from.</summary>
    private sealed class RecordingLog : IEventStore, ICompactableLog
    {
        private readonly MemoryLog inner = new();

        public long LastReadFromVersion { get; set; } = -1;

        public long SavedSnapshotVersion { get; private set; } = -1;

        public ValueTask AppendAsync(
            long expectedVersion,
            IReadOnlyList<Op> ops,
            CancellationToken cancellationToken = default) =>
            inner.AppendAsync(expectedVersion, ops, cancellationToken);

        public ValueTask<LogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default)
        {
            LastReadFromVersion = fromVersion;
            return inner.ReadAsync(fromVersion, cancellationToken);
        }

        public ValueTask SaveSnapshotAsync(byte[] data, long version, CancellationToken cancellationToken = default)
        {
            SavedSnapshotVersion = version;
            return inner.SaveSnapshotAsync(data, version, cancellationToken);
        }

        public ValueTask<SnapshotReadResult> LoadSnapshotAsync(CancellationToken cancellationToken = default) =>
            inner.LoadSnapshotAsync(cancellationToken);

        public ValueTask CompactAsync(byte[] snapshot, long version, CancellationToken cancellationToken = default) =>
            inner.CompactAsync(snapshot, version, cancellationToken);
    }

    /// <summary>A stream-only log: no snapshots, no compaction.</summary>
    private sealed class MinimalLog : IEventLog
    {
        private readonly MemoryLog inner = new();

        public ValueTask AppendAsync(
            long expectedVersion,
            IReadOnlyList<Op> ops,
            CancellationToken cancellationToken = default) =>
            inner.AppendAsync(expectedVersion, ops, cancellationToken);

        public ValueTask<LogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(fromVersion, cancellationToken);
    }
}
