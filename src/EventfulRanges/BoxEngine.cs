namespace EventfulRanges;

/// <summary>
/// Applies box operations to an append-only log and materializes the converged
/// cover under a conflict-resolution strategy. Safe for concurrent callers of one
/// engine.
/// </summary>
/// <remarks>
/// History is the truth and the cover is a cache: the log is read once when the
/// engine opens and replayed. Applying takes an async gate, so one engine's
/// appends are serialized. A store that has moved on — another writer appended
/// first — surfaces a <see cref="StoreConflictException"/> and leaves the cover
/// untouched, rather than guessing which history won.
/// <para>
/// The first operation fixes the dimensionality: a replica is one space, so an
/// operation of another shape is rejected with
/// <see cref="DimensionMismatchException"/>.
/// </para>
/// </remarks>
public sealed class BoxEngine : IDisposable
{
    private readonly Lock state = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IBoxLog log;
    private readonly StrategyKind strategy;
    private readonly IClock clock;
    private readonly Canonicalizer canonicalizer;
    private readonly MetaMerge metaMerge;

    private readonly Dictionary<string, BoxOp> ops = new(StringComparer.Ordinal);
    private IReadOnlyList<Box> view = [];
    private long version;
    private int dimensions = -1;
    private bool viewIsStale = true;

    private BoxEngine(IBoxLog log, StrategyKind strategy, BoxEngineOptions options)
    {
        this.log = log;
        this.strategy = strategy;
        clock = options.Clock;
        canonicalizer = options.Canonicalizer;
        metaMerge = options.MetaMerge;
    }

    /// <summary>The conflict-resolution strategy this engine materializes with.</summary>
    public StrategyKind Strategy => strategy;

    /// <summary>The number of dimensions of the engine's regions; -1 before any.</summary>
    public int Dimensions
    {
        get
        {
            lock (state)
            {
                return dimensions;
            }
        }
    }

    /// <summary>Every known operation, sorted by identifier.</summary>
    public IReadOnlyList<BoxOp> Ops
    {
        get
        {
            lock (state)
            {
                return ops.Values.OrderBy(op => op.Id, StringComparer.Ordinal).ToList();
            }
        }
    }

    /// <summary>Opens an engine over the log and replays what it holds.</summary>
    public static async ValueTask<BoxEngine> OpenAsync(
        IBoxLog log,
        StrategyKind strategy,
        BoxEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        var engine = new BoxEngine(log, strategy, options ?? new BoxEngineOptions());
        await engine.ReloadAsync(cancellationToken);
        return engine;
    }

    /// <summary>
    /// Applies one operation, ignoring a duplicate by identifier, and returns it as
    /// stored — with the timestamp the engine stamped.
    /// </summary>
    public async ValueTask<BoxOp> ApplyAsync(BoxOp op, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(op);

        await ApplyAllAsync([op], cancellationToken);
        lock (state)
        {
            return ops.TryGetValue(op.Id, out var stored) ? stored : op;
        }
    }

    /// <summary>Applies a batch of operations, ignoring duplicates by identifier.</summary>
    /// <exception cref="InvalidBoxOpException">An operation is not well-formed.</exception>
    /// <exception cref="DimensionMismatchException">An operation has another number of dimensions.</exception>
    /// <exception cref="StoreConflictException">The log moved on before this batch landed.</exception>
    public async ValueTask ApplyAllAsync(
        IReadOnlyList<BoxOp> newOps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newOps);
        if (newOps.Count == 0)
        {
            return;
        }

        foreach (var op in newOps)
        {
            op.EnsureValid();
        }

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

    /// <summary>The current canonical cover.</summary>
    public IReadOnlyList<Box> Materialize()
    {
        lock (state)
        {
            EnsureView();
            return view.ToArray();
        }
    }

    /// <summary>Whether the materialized cover holds <paramref name="point"/>.</summary>
    public bool Contains(IReadOnlyList<double> point)
    {
        lock (state)
        {
            EnsureView();
            return Boxes.Contains(view, point);
        }
    }

    /// <summary>Whether the materialized cover shares a point with <paramref name="box"/>.</summary>
    public bool Overlaps(Box box)
    {
        lock (state)
        {
            EnsureView();
            return Boxes.Overlaps(view, box);
        }
    }

    /// <summary>The operation with this identifier, or <c>null</c> when unknown.</summary>
    public BoxOp? Op(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (state)
        {
            return ops.TryGetValue(id, out var op) ? op : null;
        }
    }

    /// <summary>Releases the async gate; do not use the engine afterwards.</summary>
    public void Dispose() => gate.Dispose();

    private async ValueTask ReloadAsync(CancellationToken cancellationToken)
    {
        var read = await log.ReadAsync(0, cancellationToken);
        lock (state)
        {
            foreach (var op in read.Ops)
            {
                Adopt(op);
            }

            version = read.Version;
            viewIsStale = true;
        }
    }

    private async ValueTask ApplyGatedAsync(IReadOnlyList<BoxOp> newOps, CancellationToken cancellationToken)
    {
        var fresh = newOps.Where(op => !ops.ContainsKey(op.Id)).ToList();
        if (fresh.Count == 0)
        {
            return;
        }

        EnsureDimensions(fresh);
        var stamped = fresh.Select(Stamp).ToList();

        long expected;
        lock (state)
        {
            expected = version;
        }

        await log.AppendAsync(expected, stamped, cancellationToken);

        lock (state)
        {
            foreach (var op in stamped)
            {
                Adopt(op);
            }

            version = log.Version;
            viewIsStale = true;
        }
    }

    /// <summary>
    /// Records one operation and keeps the dimensionality guard and the clock in
    /// step with it.
    /// </summary>
    private void Adopt(BoxOp op)
    {
        ops[op.Id] = op;
        dimensions = dimensions < 0 ? op.Box.Dimensions : dimensions;
        clock.Observe(op.Timestamp);
    }

    /// <summary>Rejects a batch whose boxes do not fit the engine's space.</summary>
    private void EnsureDimensions(List<BoxOp> fresh)
    {
        if (dimensions < 0)
        {
            dimensions = fresh[0].Box.Dimensions;
        }

        foreach (var op in fresh)
        {
            if (op.Box.Dimensions != dimensions)
            {
                throw new DimensionMismatchException(dimensions, op.Box.Dimensions);
            }
        }
    }

    private BoxOp Stamp(BoxOp op) =>
        op.Timestamp == 0 ? op with { Timestamp = clock.Tick() } : op;

    private void EnsureView()
    {
        if (!viewIsStale)
        {
            return;
        }

        var live = ops.Values.OrderBy(op => op.Id, StringComparer.Ordinal).ToList();
        view = canonicalizer(BoxStrategies.Materialize(strategy, live, metaMerge));
        viewIsStale = false;
    }
}
