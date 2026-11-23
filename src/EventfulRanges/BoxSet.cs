using System.Collections.Immutable;
using BoxAlgebra = EventfulRanges.Boxes;

namespace EventfulRanges;

/// <summary>
/// The n-dimensional facade: a replica you add boxes to, remove them from, undo
/// single edits in, and query by point, by box, or by a line through the space.
/// </summary>
/// <remarks>
/// It is the counterpart of <see cref="RangeSet"/> and the type most callers need.
/// Every edit is an operation appended to a log, so the replica survives a restart
/// and two replicas that saw the same operations answer the same cover.
/// <para>
/// Not here yet, and deliberately: compaction. The range facade has it because its
/// engine snapshots; this one gains it together with the engine's snapshot support,
/// rather than in a form nothing can use.
/// </para>
/// </remarks>
public sealed class BoxSet : IDisposable
{
    private readonly BoxEngine engine;

    private BoxSet(BoxEngine engine) => this.engine = engine;

    /// <summary>The conflict-resolution strategy this replica materializes with.</summary>
    public StrategyKind Strategy => engine.Strategy;

    /// <summary>The current canonical cover.</summary>
    public IReadOnlyList<Box> Boxes => engine.Materialize();

    /// <summary>
    /// The layered front of the known operations, bottom to top — the view a painter
    /// uses, where each region stays whole and later layers cover earlier ones.
    /// </summary>
    public IReadOnlyList<Layer> Layers => engine.Layers();

    /// <summary>Every known operation, sorted by identifier.</summary>
    public IReadOnlyList<BoxOp> Ops => engine.Ops;

    /// <summary>The number of dimensions of the replica's regions; -1 before any.</summary>
    public int Dimensions => engine.Dimensions;

    /// <summary>
    /// Opens a replica over a directory, keeping its history in
    /// <c>boxes.stream.jsonl</c>.
    /// </summary>
    public static async ValueTask<BoxSet> OpenAsync(
        string directory,
        StrategyKind strategy,
        BoxEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return await OpenLogAsync(BoxJsonlLog.Open(directory), strategy, options, cancellationToken);
    }

    /// <summary>Opens a replica over any box log.</summary>
    public static async ValueTask<BoxSet> OpenLogAsync(
        IBoxLog log,
        StrategyKind strategy,
        BoxEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        return new BoxSet(await BoxEngine.OpenAsync(log, strategy, options, cancellationToken));
    }

    /// <summary>Whether the cover holds <paramref name="point"/>.</summary>
    public bool Contains(IReadOnlyList<double> point) => engine.Contains(point);

    /// <summary>Whether the cover shares a point with <paramref name="box"/>.</summary>
    public bool Overlaps(Box box) => engine.Overlaps(box);

    /// <summary>The boxes <paramref name="line"/> crosses, in canonical order.</summary>
    public IReadOnlyList<Box> Crossed(Line line) => BoxAlgebra.Crossed(engine.Materialize(), line);

    /// <summary>How coverage changes along <paramref name="line"/>.</summary>
    public IReadOnlyList<LineSegment> Traverse(Line line) => BoxAlgebra.Traverse(engine.Materialize(), line);

    /// <summary>Applies one prepared operation, returning it as stored.</summary>
    public ValueTask<BoxOp> ApplyAsync(BoxOp op, CancellationToken cancellationToken = default) =>
        engine.ApplyAsync(op, cancellationToken);

    /// <summary>Applies a batch of prepared operations.</summary>
    public ValueTask ApplyAllAsync(
        IReadOnlyList<BoxOp> ops,
        CancellationToken cancellationToken = default) =>
        engine.ApplyAllAsync(ops, cancellationToken);

    /// <summary>Adds the half-open box <c>[min, max)</c>.</summary>
    public ValueTask<BoxOp> AddAsync(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(BoxOp.Add(min, max), cancellationToken);

    /// <summary>Adds a box with explicit inclusivity per face.</summary>
    public ValueTask<BoxOp> AddWithBoundsAsync(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        IReadOnlyList<Bound>? minBound,
        IReadOnlyList<Bound>? maxBound,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(BoxOp.AddWithBounds(min, max, minBound, maxBound), cancellationToken);

    /// <summary>Adds a box carrying metadata, which merged boxes join.</summary>
    public ValueTask<BoxOp> AddWithMetaAsync(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        ImmutableArray<byte> meta,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(BoxOp.Create(BoxOpKind.Add, new Box(min, max).WithMeta(meta)), cancellationToken);

    /// <summary>Removes the half-open box <c>[min, max)</c>.</summary>
    public ValueTask<BoxOp> RemoveAsync(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(BoxOp.Remove(min, max), cancellationToken);

    /// <summary>Removes a box with explicit inclusivity per face.</summary>
    public ValueTask<BoxOp> RemoveWithBoundsAsync(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        IReadOnlyList<Bound>? minBound,
        IReadOnlyList<Bound>? maxBound,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(BoxOp.RemoveWithBounds(min, max, minBound, maxBound), cancellationToken);

    /// <summary>Removes a box carrying metadata.</summary>
    public ValueTask<BoxOp> RemoveWithMetaAsync(
        IReadOnlyList<double> min,
        IReadOnlyList<double> max,
        ImmutableArray<byte> meta,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(BoxOp.Create(BoxOpKind.Remove, new Box(min, max).WithMeta(meta)), cancellationToken);

    /// <summary>
    /// Cancels the operation with this identifier, carrying the region it applied
    /// to so the retraction is expressed in the same space.
    /// </summary>
    /// <exception cref="ArgumentException">No operation carries that identifier.</exception>
    public ValueTask<BoxOp> RetractAsync(string operationId, CancellationToken cancellationToken = default) =>
        ApplyAsync(Retraction(null, operationId), cancellationToken);

    /// <summary>
    /// Cancels the operation with this identifier under a chosen identifier, so the
    /// same retraction can be issued twice without appending it twice.
    /// </summary>
    public ValueTask<BoxOp> RetractWithIdAsync(
        string id,
        string operationId,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(Retraction(id, operationId), cancellationToken);

    /// <summary>Releases the engine; do not use the facade afterwards.</summary>
    public void Dispose() => engine.Dispose();

    private BoxOp Retraction(string? id, string operationId)
    {
        var referenced = engine.Op(operationId)
            ?? throw new ArgumentException(
                FormattableString.Invariant($"No operation carries the identifier {operationId}."),
                nameof(operationId));

        var retraction = BoxOp.Retract(operationId, referenced.Box);
        return id is null ? retraction : retraction with { Id = id };
    }
}
