namespace EventfulRanges;

/// <summary>
/// The high-level API: a replica backed by a log, where callers add and remove
/// ranges and read the converged set. Every write is applied to the log before
/// it reaches the view.
/// </summary>
public sealed class RangeSet : IDisposable
{
    private readonly Engine engine;

    private RangeSet(Engine engine) => this.engine = engine;

    /// <summary>
    /// Opens a replica whose operations live in <paramref name="directory"/> as a
    /// JSON Lines stream.
    /// </summary>
    public static async ValueTask<RangeSet> OpenAsync(
        string directory,
        StrategyKind strategy,
        EngineOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new(await Engine.OpenAsync(JsonlLog.Open(directory), strategy, options, cancellationToken));

    /// <summary>Opens a replica backed by the given log.</summary>
    public static async ValueTask<RangeSet> OpenStoreAsync(
        IEventLog log,
        StrategyKind strategy,
        EngineOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new(await Engine.OpenAsync(log, strategy, options, cancellationToken));

    /// <summary>The conflict-resolution strategy this replica materializes with.</summary>
    public StrategyKind Strategy => engine.Strategy;

    /// <summary>The current canonical interval view.</summary>
    public IReadOnlyList<Interval> Ranges => engine.Materialize();

    /// <summary>Whether <paramref name="value"/> belongs to the materialized set.</summary>
    public bool Contains(double value) => engine.Contains(value);

    /// <summary>Whether any materialized interval shares a point with <paramref name="interval"/>.</summary>
    public bool Overlaps(Interval interval) => engine.Overlaps(interval);

    /// <summary>The known operations, sorted by identifier, for anti-entropy exchange.</summary>
    public IReadOnlyList<Op> Ops => engine.Ops;

    /// <summary>Applies an operation and returns it as stored, timestamp included.</summary>
    public ValueTask<Op> ApplyAsync(Op op, CancellationToken cancellationToken = default) =>
        engine.ApplyAsync(op, cancellationToken);

    /// <summary>Applies a batch, ignoring duplicates by identifier.</summary>
    public ValueTask ApplyAllAsync(IReadOnlyList<Op> ops, CancellationToken cancellationToken = default) =>
        engine.ApplyAllAsync(ops, cancellationToken);

    /// <summary>Adds the closed range <c>[start, end]</c> and returns the applied operation.</summary>
    public ValueTask<Op> AddAsync(double start, double end, CancellationToken cancellationToken = default) =>
        engine.ApplyAsync(Op.Add(start, end), cancellationToken);

    /// <summary>Adds a range with explicit inclusivity for each endpoint.</summary>
    public ValueTask<Op> AddWithBoundsAsync(
        double start,
        double end,
        Bound startBound,
        Bound endBound,
        CancellationToken cancellationToken = default) =>
        engine.ApplyAsync(Op.Add(start, end, startBound, endBound), cancellationToken);

    /// <summary>Removes the closed range <c>[start, end]</c> and returns the applied operation.</summary>
    public ValueTask<Op> RemoveAsync(double start, double end, CancellationToken cancellationToken = default) =>
        engine.ApplyAsync(Op.Remove(start, end), cancellationToken);

    /// <summary>Removes a range with explicit inclusivity for each endpoint.</summary>
    public ValueTask<Op> RemoveWithBoundsAsync(
        double start,
        double end,
        Bound startBound,
        Bound endBound,
        CancellationToken cancellationToken = default) =>
        engine.ApplyAsync(Op.Remove(start, end, startBound, endBound), cancellationToken);

    /// <summary>Persists a snapshot of the current state.</summary>
    public ValueTask SnapshotAsync(CancellationToken cancellationToken = default) =>
        engine.SnapshotAsync(cancellationToken);

    /// <summary>Collapses the log to a snapshot plus the operations it does not cover.</summary>
    public ValueTask CompactAsync(CancellationToken cancellationToken = default) =>
        engine.CompactAsync(cancellationToken);

    /// <summary>Releases the replica's engine.</summary>
    public void Dispose()
    {
        engine.Dispose();
        GC.SuppressFinalize(this);
    }
}
