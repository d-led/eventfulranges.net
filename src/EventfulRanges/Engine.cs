using System.Text.Json;

namespace EventfulRanges;

/// <summary>
/// Applies operations to an append-only log and materializes the converged view
/// under a conflict-resolution strategy. Safe for concurrent callers.
/// </summary>
/// <remarks>
/// History is the truth and the view is a cache: the log is read once when the
/// engine opens and replayed, and a snapshot only fast-forwards that replay.
/// Applying takes an async gate, so appends to one replica are serialized while
/// other replicas keep running.
/// </remarks>
public sealed class Engine : IDisposable
{
    private const int RetriesOnConflict = 3;

    private readonly Lock state = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IEventLog log;
    private readonly StrategyKind strategy;
    private readonly IClock clock;
    private readonly int snapshotEvery;

    private readonly Dictionary<string, Op> ops = new(StringComparer.Ordinal);
    private IReadOnlyList<Interval> view = Array.Empty<Interval>();
    private long version;
    private int sinceSnapshot;
    private bool viewIsStale = true;

    private Engine(IEventLog log, StrategyKind strategy, EngineOptions options)
    {
        this.log = log;
        this.strategy = strategy;
        clock = options.Clock;
        snapshotEvery = options.SnapshotEvery;
    }

    /// <summary>The conflict-resolution strategy this engine materializes with.</summary>
    public StrategyKind Strategy => strategy;

    /// <summary>
    /// Opens an engine over the log: reads the snapshot when there is a
    /// compatible one, replays the rest of the log, and materializes the view.
    /// </summary>
    public static async ValueTask<Engine> OpenAsync(
        IEventLog log,
        StrategyKind strategy,
        EngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        var engine = new Engine(log, strategy, options ?? new EngineOptions());
        await engine.ReloadAsync(cancellationToken);
        return engine;
    }

    /// <summary>
    /// Applies a single operation, ignoring a duplicate by identifier, and
    /// returns it as stored — with the timestamp the engine stamped.
    /// </summary>
    public async ValueTask<Op> ApplyAsync(Op op, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op);
        await ApplyAllAsync(new[] { op }, cancellationToken);

        lock (state)
        {
            return ops.TryGetValue(op.Id, out var stored) ? stored : op;
        }
    }

    /// <summary>Applies a batch of operations, ignoring duplicates by identifier.</summary>
    public async ValueTask ApplyAllAsync(
        IReadOnlyList<Op> newOps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newOps);
        if (newOps.Count == 0)
        {
            return;
        }

        Validate(newOps);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await ApplyGatedAsync(newOps, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The current canonical interval view.</summary>
    public IReadOnlyList<Interval> Materialize()
    {
        lock (state)
        {
            EnsureView();
            return view.ToArray();
        }
    }

    /// <summary>Whether <paramref name="value"/> belongs to the materialized set.</summary>
    public bool Contains(double value)
    {
        lock (state)
        {
            EnsureView();
            return Intervals.Contains(view, value);
        }
    }

    /// <summary>Whether any materialized interval shares a point with <paramref name="interval"/>.</summary>
    public bool Overlaps(Interval interval)
    {
        lock (state)
        {
            EnsureView();
            return Intervals.Overlaps(view, interval);
        }
    }

    /// <summary>The known operations, sorted by identifier, for anti-entropy exchange.</summary>
    public IReadOnlyList<Op> Ops
    {
        get
        {
            lock (state)
            {
                return OpsUnlocked();
            }
        }
    }

    /// <summary>
    /// Persists a snapshot of the current state. Backends that cannot store one
    /// skip this; the log alone is enough to rebuild.
    /// </summary>
    public async ValueTask SnapshotAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await SnapshotGatedAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Rewrites the log as a snapshot of the current state plus the operations
    /// it does not cover, collapsing the stream to its smallest form.
    /// </summary>
    /// <exception cref="NotSupportedException">The log cannot compact.</exception>
    public async ValueTask CompactAsync(CancellationToken cancellationToken = default)
    {
        if (log is not ICompactableLog compactable)
        {
            throw new NotSupportedException("The log cannot compact; implement ICompactableLog.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var document = BuildSnapshot();
            await compactable.CompactAsync(JsonSerializer.SerializeToUtf8Bytes(document, JsonContext.Default.SnapshotDocument), document.Version, cancellationToken);
            Interlocked.Exchange(ref sinceSnapshot, 0);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask ReloadAsync(CancellationToken cancellationToken)
    {
        var snapshot = CompatibleSnapshot(await TryLoadSnapshotAsync(cancellationToken));
        var read = await log.ReadAsync(snapshot?.Version ?? 0, cancellationToken);

        lock (state)
        {
            Seed(snapshot, read);
            EnsureView();
        }
    }

    /// <summary>The snapshot, when it was built with the strategy this engine uses.</summary>
    private SnapshotDocument? CompatibleSnapshot(SnapshotDocument? snapshot) =>
        snapshot is { } candidate && candidate.Strategy == strategy ? candidate : null;

    /// <summary>Folds a snapshot and the replayed tail into the state. Caller holds the state lock.</summary>
    private void Seed(SnapshotDocument? snapshot, LogReadResult read)
    {
        foreach (var op in snapshot?.Ops ?? Array.Empty<Op>())
        {
            ops[op.Id] = op;
        }

        foreach (var op in read.Ops)
        {
            ops[op.Id] = op;
            clock.Observe(op.Timestamp);
        }

        version = read.Version;
        viewIsStale = true;
    }

    private async ValueTask<SnapshotDocument?> TryLoadSnapshotAsync(CancellationToken cancellationToken)
    {
        if (log is not ISnapshotStore store)
        {
            return null;
        }

        try
        {
            var snapshot = await store.LoadSnapshotAsync(cancellationToken);
            return JsonSerializer.Deserialize(snapshot.Data, JsonContext.Default.SnapshotDocument);
        }
        catch (SnapshotNotFoundException)
        {
            return null;
        }
    }

    private async ValueTask ApplyGatedAsync(IReadOnlyList<Op> newOps, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var fresh = Stamp(Unseen(newOps));
            if (fresh.Count == 0)
            {
                return;
            }

            long expected;
            lock (state)
            {
                expected = version;
            }

            try
            {
                await log.AppendAsync(expected, fresh, cancellationToken);
            }
            catch (StoreConflictException) when (attempt < RetriesOnConflict)
            {
                await CatchUpAsync(cancellationToken);
                continue;
            }

            await FoldAsync(fresh, cancellationToken);
            return;
        }
    }

    private List<Op> Unseen(IReadOnlyList<Op> candidates)
    {
        lock (state)
        {
            var fresh = new List<Op>(candidates.Count);
            foreach (var op in candidates)
            {
                if (!ops.ContainsKey(op.Id))
                {
                    fresh.Add(op);
                }
            }

            return fresh;
        }
    }

    private List<Op> Stamp(List<Op> fresh)
    {
        var stamped = new List<Op>(fresh.Count);
        foreach (var op in fresh)
        {
            stamped.Add(op.Timestamp == 0 ? op with { Timestamp = clock.Tick() } : op);
        }

        return stamped;
    }

    private async ValueTask CatchUpAsync(CancellationToken cancellationToken)
    {
        long from;
        lock (state)
        {
            from = version;
        }

        var read = await log.ReadAsync(from, cancellationToken);

        lock (state)
        {
            foreach (var op in read.Ops)
            {
                ops[op.Id] = op;
                clock.Observe(op.Timestamp);
            }

            version = read.Version;
            viewIsStale = true;
        }
    }

    private async ValueTask FoldAsync(List<Op> fresh, CancellationToken cancellationToken)
    {
        lock (state)
        {
            foreach (var op in fresh)
            {
                clock.Observe(op.Timestamp);
                ops[op.Id] = op;
            }

            version += fresh.Count;
            sinceSnapshot += fresh.Count;
            viewIsStale = true;
        }

        if (snapshotEvery > 0 && Volatile.Read(ref sinceSnapshot) >= snapshotEvery)
        {
            await SnapshotGatedAsync(cancellationToken);
        }
    }

    private async ValueTask SnapshotGatedAsync(CancellationToken cancellationToken)
    {
        if (log is not ISnapshotStore store)
        {
            return;
        }

        var document = BuildSnapshot();
        await store.SaveSnapshotAsync(
            JsonSerializer.SerializeToUtf8Bytes(document, JsonContext.Default.SnapshotDocument),
            document.Version,
            cancellationToken);
        Interlocked.Exchange(ref sinceSnapshot, 0);
    }

    private SnapshotDocument BuildSnapshot()
    {
        lock (state)
        {
            EnsureView();
            return new SnapshotDocument(strategy, version, view.ToArray(), OpsUnlocked());
        }
    }

    private Op[] OpsUnlocked()
    {
        var ordered = new Op[ops.Count];
        ops.Values.CopyTo(ordered, 0);
        Array.Sort(ordered, (left, right) => string.CompareOrdinal(left.Id, right.Id));
        return ordered;
    }

    /// <summary>Rebuilds the cached view when it is stale. Caller holds the state lock.</summary>
    private void EnsureView()
    {
        if (!viewIsStale)
        {
            return;
        }

        view = Strategies.Materialize(strategy, OpsUnlocked());
        viewIsStale = false;
    }

    private static void Validate(IReadOnlyList<Op> candidates)
    {
        foreach (var op in candidates)
        {
            op.EnsureValid();
        }
    }

    /// <summary>Releases the semaphore the engine serializes appends with.</summary>
    public void Dispose()
    {
        gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
