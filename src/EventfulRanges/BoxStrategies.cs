namespace EventfulRanges;

/// <summary>
/// The n-dimensional counterpart of <see cref="Strategies"/>: the same policies
/// over boxes, plus retraction.
/// </summary>
public static class BoxStrategies
{
    /// <summary>
    /// Projects the operations into a canonical cover of boxes under
    /// <paramref name="strategy"/>, joining metadata with <see cref="Meta.Union"/>.
    /// </summary>
    public static IReadOnlyList<Box> Materialize(StrategyKind strategy, IReadOnlyList<BoxOp> ops) =>
        Materialize(strategy, ops, Meta.Union);

    /// <summary>Projects the operations into a canonical cover of boxes.</summary>
    /// <param name="strategy">How concurrent operations resolve.</param>
    /// <param name="ops">The log, retraction included.</param>
    /// <param name="merge">
    /// How metadata joins for the union-based strategies. The ranking strategies
    /// carry the winning operation's metadata unchanged, so they never join.
    /// </param>
    public static IReadOnlyList<Box> Materialize(
        StrategyKind strategy,
        IReadOnlyList<BoxOp> ops,
        MetaMerge merge)
    {
        ArgumentNullException.ThrowIfNull(ops);
        ArgumentNullException.ThrowIfNull(merge);

        var live = Effective(ops);
        return strategy switch
        {
            StrategyKind.Lww or StrategyKind.Fww => ToBoxes(Segments(strategy, live)),
            StrategyKind.AdditiveWins => Boxes.Difference(
                BoxesOf(live, BoxOpKind.Add, merge),
                BoxesOf(live, BoxOpKind.Remove, merge),
                merge),
            StrategyKind.AdditiveWinsLww => Boxes.Difference(
                ToBoxes(Segments(StrategyKind.Lww, Of(live, BoxOpKind.Add))),
                BoxesOf(live, BoxOpKind.Remove, merge),
                merge),
            StrategyKind.GrowOnly => BoxesOf(live, BoxOpKind.Add, merge),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown strategy."),
        };
    }

    /// <summary>
    /// Projects the operations into winner-annotated regions: every point is
    /// labelled with the operation the strategy prefers there. Defined for
    /// <see cref="StrategyKind.Lww"/> and <see cref="StrategyKind.Fww"/>; the other
    /// strategies return an empty list, because they do not rank operations.
    /// </summary>
    public static IReadOnlyList<BoxSegment> Segments(StrategyKind strategy, IReadOnlyList<BoxOp> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);

        if (strategy is not (StrategyKind.Lww or StrategyKind.Fww))
        {
            return [];
        }

        var segments = ops
            .Select(op => new BoxSegment(op.Box, new BoxWinner(op.Timestamp, op.Id, op.Kind)))
            .ToList();
        return Normalize(strategy, segments);
    }

    /// <summary>Merges two canonical segment sets under the strategy.</summary>
    public static IReadOnlyList<BoxSegment> CombineSegments(
        StrategyKind strategy,
        IReadOnlyList<BoxSegment> first,
        IReadOnlyList<BoxSegment> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return Normalize(strategy, [.. first, .. second]);
    }

    /// <summary>The regions decided by additions, as a canonical cover.</summary>
    public static IReadOnlyList<Box> ToBoxes(IReadOnlyList<BoxSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return Boxes.Normalize(
            segments.Where(segment => segment.Winner.Kind == BoxOpKind.Add)
                .Select(segment => segment.Box)
                .ToList());
    }

    /// <summary>
    /// Projects the operations into a culled, layered front in bottom-to-top paint
    /// order under last-write-wins priority: each box kept whole, with every box a
    /// higher layer fully covers removed.
    /// </summary>
    /// <remarks>
    /// It is the painter's-algorithm counterpart of <c>Materialize</c>.
    /// Materialize returns a non-overlapping cover, so a small stroke inside a big box
    /// carves that box into strips; the front keeps the big box whole and layers the
    /// stroke on top. An erasing layer paints the background, and a later painting
    /// layer covers an earlier erase exactly as a later stroke covers an earlier one.
    /// </remarks>
    public static IReadOnlyList<Layer> Layers(IReadOnlyList<BoxOp> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);

        var front = new List<Layer>();
        var claimed = new List<Box>();
        foreach (var op in Effective(ops)
                     .OrderBy(op => new BoxWinner(op.Timestamp, op.Id, op.Kind), Comparer<BoxWinner>.Create(
                         (left, right) => Preferred(StrategyKind.Lww, left, right) ? -1
                             : Preferred(StrategyKind.Lww, right, left) ? 1
                             : 0)))
        {
            var visible = Boxes.Difference([op.Box], claimed).Count > 0;
            claimed = Boxes.InsertNormalized(claimed, op.Box).ToList();
            if (visible)
            {
                front.Add(new Layer(op.Box, op.Kind));
            }
        }

        front.Reverse();
        return front;
    }

    /// <summary>
    /// Resolves retractions: drops every retraction and the operation each one
    /// names, leaving the rest in their original order.
    /// </summary>
    /// <remarks>
    /// Retraction is single-level. Undoing an undone edit re-issues the original
    /// under a fresh identifier rather than retracting the retraction, so one pass
    /// over the log suffices and a retraction naming an unknown operation changes
    /// nothing.
    /// </remarks>
    public static IReadOnlyList<BoxOp> Effective(IReadOnlyList<BoxOp> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);

        var retracted = ops
            .Where(op => op.Kind == BoxOpKind.Retract)
            .Select(op => op.Ref)
            .ToHashSet(StringComparer.Ordinal);
        if (retracted.Count == 0)
        {
            return ops;
        }

        return ops
            .Where(op => op.Kind != BoxOpKind.Retract && !retracted.Contains(op.Id))
            .ToList();
    }

    /// <summary>The normalized boxes of the operations of one kind.</summary>
    private static IReadOnlyList<Box> BoxesOf(
        IReadOnlyList<BoxOp> ops,
        BoxOpKind kind,
        MetaMerge merge) =>
        Boxes.Normalize(Of(ops, kind).Select(op => op.Box).ToList(), merge);

    private static List<BoxOp> Of(IReadOnlyList<BoxOp> ops, BoxOpKind kind) =>
        ops.Where(op => op.Kind == kind).ToList();

    /// <summary>
    /// Reduces overlapping regions to disjoint winner-annotated pieces by applying
    /// operations in priority order: the preferred operation claims first and the
    /// others fill the gaps it leaves.
    /// </summary>
    private static List<BoxSegment> Normalize(
        StrategyKind strategy,
        IReadOnlyList<BoxSegment> segments)
    {
        var ranked = segments.OrderBy(segment => segment, Comparer<BoxSegment>.Create(
            (left, right) => Preferred(strategy, left.Winner, right.Winner) ? -1
                : Preferred(strategy, right.Winner, left.Winner) ? 1
                : 0)).ToList();

        var claimed = new List<Box>();
        var decided = new List<BoxSegment>();
        foreach (var segment in ranked)
        {
            foreach (var free in Boxes.Difference([segment.Box], claimed))
            {
                decided.Add(new BoxSegment(free, segment.Winner));
            }

            claimed = Boxes.InsertNormalized(claimed, segment.Box).ToList();
        }

        decided.Sort((left, right) => left.Box.CompareTo(right.Box));
        return decided;
    }

    /// <summary>Whether <paramref name="first"/> outranks <paramref name="second"/>.</summary>
    private static bool Preferred(StrategyKind strategy, BoxWinner first, BoxWinner second)
    {
        var compared = first.Timestamp.CompareTo(second.Timestamp);
        if (strategy == StrategyKind.Fww)
        {
            return compared != 0 ? compared < 0 : string.CompareOrdinal(first.Id, second.Id) < 0;
        }

        return compared != 0 ? compared > 0 : string.CompareOrdinal(first.Id, second.Id) > 0;
    }
}

