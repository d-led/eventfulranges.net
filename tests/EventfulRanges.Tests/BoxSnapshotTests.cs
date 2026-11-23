using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// A snapshot is a cache of history, never a replacement for it: a replica that
/// reads one still answers the same cover, and compaction only removes what the
/// snapshot already covers.
/// </summary>
public sealed class BoxSnapshotTests
{
    [Fact]
    public async Task A_replica_that_snapshotted_still_answers_the_same_after_a_restart()
    {
        using var directory = new TemporaryDirectory();
        IReadOnlyList<Box> before;

        using (var running = await OpenIn(directory.DirectoryPath))
        {
            await running.ApplyAllAsync([BoxOp.Add([0], [10]), BoxOp.Remove([3], [5])]);
            before = running.Materialize();
            await running.SnapshotAsync();
        }

        using var reopened = await OpenIn(directory.DirectoryPath);

        reopened.Materialize().ShouldBe(before);
        reopened.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_snapshot_of_another_strategy_is_ignored_and_the_log_is_replayed()
    {
        using var directory = new TemporaryDirectory();
        using (var growOnly = await BoxSet.OpenAsync(directory.DirectoryPath, StrategyKind.GrowOnly))
        {
            await growOnly.AddAsync([0], [10]);
            await growOnly.RemoveAsync([3], [5]);
            growOnly.Dispose();
        }

        using var other = await BoxEngine.OpenAsync(
            BoxJsonlLog.Open(directory.DirectoryPath),
            StrategyKind.AdditiveWins,
            new BoxEngineOptions { Clock = new LamportClock() });

        other.Materialize().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Compaction_replaces_the_covered_operations_with_the_snapshot()
    {
        using var directory = new TemporaryDirectory();
        var log = BoxJsonlLog.Open(directory.DirectoryPath);
        using var engine = await BoxEngine.OpenAsync(log, StrategyKind.GrowOnly, Deterministic());

        await engine.ApplyAllAsync([BoxOp.Add([0], [10]), BoxOp.Remove([3], [5])]);
        var before = engine.Materialize();
        await engine.CompactAsync();

        log.Version.ShouldBe(0);
        (await log.LoadSnapshotAsync()).Version.ShouldBe(2);

        using var reopened = await BoxEngine.OpenAsync(BoxJsonlLog.Open(directory.DirectoryPath), StrategyKind.GrowOnly, Deterministic());
        reopened.Materialize().ShouldBe(before);
        reopened.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_memory_log_without_a_snapshot_says_so()
    {
        var log = new BoxMemoryLog();

        await Should.ThrowAsync<SnapshotNotFoundException>(async () => await log.LoadSnapshotAsync());
    }

    [Fact]
    public async Task Snapshotting_a_log_that_cannot_keep_one_changes_nothing()
    {
        using var engine = await BoxEngine.OpenAsync(new PlainLog(), StrategyKind.GrowOnly, Deterministic());

        await engine.ApplyAsync(BoxOp.Add([0], [10]));
        await engine.SnapshotAsync();

        engine.Materialize().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Compacting_a_log_that_cannot_compact_is_a_caller_error()
    {
        using var engine = await BoxEngine.OpenAsync(new PlainLog(), StrategyKind.GrowOnly, Deterministic());

        await Should.ThrowAsync<NotSupportedException>(async () => await engine.CompactAsync());
    }

    private static BoxEngineOptions Deterministic() => new() { Clock = new LamportClock() };

    private static ValueTask<BoxEngine> OpenIn(string directory) =>
        BoxEngine.OpenAsync(BoxJsonlLog.Open(directory), StrategyKind.AdditiveWins, Deterministic());

    /// <summary>A log with no snapshot or compaction support, as a minimal backend would be.</summary>
    private sealed class PlainLog : IBoxLog
    {
        private readonly BoxMemoryLog inner = new();

        public long Version => inner.Version;

        public ValueTask AppendAsync(long expectedVersion, IReadOnlyList<BoxOp> ops, CancellationToken cancellationToken = default) =>
            inner.AppendAsync(expectedVersion, ops, cancellationToken);

        public ValueTask<BoxLogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(fromVersion, cancellationToken);
    }
}
