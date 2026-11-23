namespace EventfulRanges;

/// <summary>
/// Materializes a set of range operations into canonical interval sets under a
/// conflict-resolution strategy. Every method is pure.
/// </summary>
public static class Strategies
{
    /// <summary>
    /// Projects the operations into the canonical interval set the strategy
    /// prescribes. The result does not depend on the order of
    /// <paramref name="ops"/>, so two replicas holding the same operations
    /// produce the same set, whatever order they arrived in.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The strategy is not defined.</exception>
    public static IReadOnlyList<Interval> Materialize(StrategyKind strategy, IReadOnlyList<Op> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);

        return strategy switch
        {
            StrategyKind.Lww or StrategyKind.Fww => ToIntervals(Segments(strategy, ops)),
            StrategyKind.AdditiveWins => Intervals.Difference(
                IntervalsOf(ops, OpKind.Add),
                IntervalsOf(ops, OpKind.Remove)),
            StrategyKind.GrowOnly => IntervalsOf(ops, OpKind.Add),
            StrategyKind.AdditiveWinsLww => throw new NotSupportedException(
                "AdditiveWinsLww resolves each point's metadata and is the n-dimensional strategy; " +
                "a range carries no metadata for a point to resolve."),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown strategy."),
        };
    }

    /// <summary>
    /// Projects the operations into winner-annotated segments: every point is
    /// labelled with the operation the strategy prefers there. Defined for
    /// <see cref="StrategyKind.Lww"/> and <see cref="StrategyKind.Fww"/>; other
    /// strategies return an empty list, because they do not rank operations.
    /// </summary>
    public static IReadOnlyList<Segment> Segments(StrategyKind strategy, IReadOnlyList<Op> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);

        if (!RanksOperations(strategy))
        {
            return Array.Empty<Segment>();
        }

        var segments = new List<Segment>(ops.Count);
        foreach (var op in ops)
        {
            segments.Add(new Segment(op.Interval, new Winner(op.Timestamp, op.Id, op.Kind)));
        }

        return Normalize(strategy, segments);
    }

    /// <summary>
    /// Merges two canonical segment sets under the strategy.
    /// </summary>
    public static IReadOnlyList<Segment> CombineSegments(
        StrategyKind strategy,
        IReadOnlyList<Segment> first,
        IReadOnlyList<Segment> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var combined = new List<Segment>(first.Count + second.Count);
        combined.AddRange(first);
        combined.AddRange(second);
        return Normalize(strategy, combined);
    }

    /// <summary>
    /// Extracts the regions decided by additions as a canonical interval set.
    /// </summary>
    public static IReadOnlyList<Interval> ToIntervals(IReadOnlyList<Segment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var intervals = new List<Interval>(segments.Count);
        foreach (var segment in segments)
        {
            if (segment.Winner.Kind == OpKind.Add)
            {
                intervals.Add(segment.Interval);
            }
        }

        return Intervals.Normalize(intervals);
    }

    /// <summary>
    /// The normalized intervals of the operations of one kind.
    /// </summary>
    public static IReadOnlyList<Interval> IntervalsOf(IReadOnlyList<Op> ops, OpKind kind)
    {
        ArgumentNullException.ThrowIfNull(ops);

        var intervals = new List<Interval>(ops.Count);
        foreach (var op in ops)
        {
            if (op.Kind == kind)
            {
                intervals.Add(op.Interval);
            }
        }

        return Intervals.Normalize(intervals);
    }

    /// <summary>
    /// Whether the strategy ranks operations point by point. It is the one place
    /// that decides which strategies own a sweep.
    /// </summary>
    private static bool RanksOperations(StrategyKind strategy) =>
        strategy is StrategyKind.Lww or StrategyKind.Fww;

    /// <summary>
    /// Reduces segments to disjoint winner-annotated pieces by sweeping: every
    /// point is decided by the best segment covering it, so each maximal run
    /// becomes one piece.
    /// </summary>
    private static IReadOnlyList<Segment> Normalize(
        StrategyKind strategy,
        List<Segment> segments)
    {
        if (segments.Count == 0)
        {
            return Array.Empty<Segment>();
        }

        var intervals = new Interval[segments.Count];
        for (var index = 0; index < segments.Count; index++)
        {
            intervals[index] = segments[index].Interval;
        }

        var covers = Intervals.Sweep(
            intervals,
            (left, right) => Outranks(strategy, segments[left], segments[right]));

        var normalized = new List<Segment>(covers.Count);
        foreach (var cover in covers)
        {
            normalized.Add(new Segment(cover.Interval, segments[cover.Index].Winner));
        }

        return normalized;
    }

    /// <summary>
    /// Whether the first segment is preferred to the second: the timestamp
    /// decides, the identifier breaks the tie, and when both are equal — the
    /// same operation appearing twice — the canonical interval order decides, so
    /// the answer never depends on the order the operations arrived in.
    /// </summary>
    private static bool Outranks(StrategyKind strategy, Segment first, Segment second)
    {
        var rank = Rank(strategy, first.Winner, second.Winner);
        return rank != 0 ? rank > 0 : Intervals.Less(first.Interval, second.Interval);
    }

    /// <summary>
    /// Positive when the first winner is preferred, negative when the second is,
    /// zero when neither outranks the other. The comparison is ordinal, so it
    /// cannot follow the culture.
    /// </summary>
    private static int Rank(StrategyKind strategy, Winner first, Winner second)
    {
        var ordered = first.Timestamp != second.Timestamp
            ? first.Timestamp.CompareTo(second.Timestamp)
            : string.CompareOrdinal(first.Id, second.Id);

        return strategy == StrategyKind.Fww ? -ordered : ordered;
    }
}
