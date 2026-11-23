namespace EventfulRanges;

/// <summary>
/// Set operations over collections of <see cref="Interval"/> values: the
/// canonical order, normalization, union, difference, and membership.
/// </summary>
public static partial class Intervals
{
    /// <summary>
    /// Whether <paramref name="first"/> sorts before <paramref name="second"/>
    /// in canonical order: by start, then by start inclusivity (closed first),
    /// then by end, then by end inclusivity (closed first).
    /// </summary>
    public static bool Less(Interval first, Interval second)
    {
        if (first.Start != second.Start)
        {
            return first.Start < second.Start;
        }

        if (first.StartBound != second.StartBound)
        {
            return first.StartBound == Bound.Closed;
        }

        if (first.End != second.End)
        {
            return first.End < second.End;
        }

        if (first.EndBound != second.EndBound)
        {
            return first.EndBound == Bound.Closed;
        }

        return false;
    }

    /// <summary>
    /// Sorts the intervals, merges overlapping or touching ones, and drops
    /// the ones that contain no point.
    /// </summary>
    public static IReadOnlyList<Interval> Normalize(IReadOnlyList<Interval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        if (intervals.Count == 0)
        {
            return Array.Empty<Interval>();
        }

        var sorted = intervals.ToArray();
        Array.Sort(sorted, Compare);

        var normalized = new List<Interval>(sorted.Length);
        foreach (var interval in sorted)
        {
            if (!interval.IsEmpty)
            {
                Append(normalized, interval);
            }
        }

        return normalized;
    }

    /// <summary>
    /// Adds an interval to a canonical set, merging it into the last interval
    /// when their union is contiguous and appending it otherwise.
    /// </summary>
    private static void Append(List<Interval> normalized, Interval interval)
    {
        if (normalized.Count > 0 && normalized[^1].Meets(interval))
        {
            normalized[^1] = Interval.Merge(normalized[^1], interval);
            return;
        }

        normalized.Add(interval);
    }

    /// <summary>Merges two interval sets into one canonical set.</summary>
    public static IReadOnlyList<Interval> Union(
        IReadOnlyList<Interval> first,
        IReadOnlyList<Interval> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var merged = new List<Interval>(first.Count + second.Count);
        merged.AddRange(first);
        merged.AddRange(second);
        return Normalize(merged);
    }

    /// <summary>
    /// The points of <paramref name="first"/> that <paramref name="second"/>
    /// does not cover, as a canonical set.
    /// </summary>
    public static IReadOnlyList<Interval> Difference(
        IReadOnlyList<Interval> first,
        IReadOnlyList<Interval> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count == 0 || second.Count == 0)
        {
            return Normalize(first);
        }

        var kept = Normalize(first);
        var cut = Normalize(second);
        var keptCount = kept.Count;

        var all = new List<Interval>(keptCount + cut.Count);
        all.AddRange(kept);
        all.AddRange(cut);

        // The second set is the cut, so it outranks the first wherever they meet.
        var covers = Sweep(all, (left, right) => left >= keptCount && right < keptCount);

        var difference = new List<Interval>(covers.Count);
        foreach (var cover in covers)
        {
            if (cover.Index < keptCount)
            {
                difference.Add(cover.Interval);
            }
        }

        return difference;
    }

    /// <summary>Whether <paramref name="value"/> belongs to any interval of the set.</summary>
    public static bool Contains(IReadOnlyList<Interval> set, double value)
    {
        ArgumentNullException.ThrowIfNull(set);

        foreach (var interval in set)
        {
            if (interval.Contains(value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether any interval of the set shares a point with
    /// <paramref name="interval"/>.
    /// </summary>
    public static bool Overlaps(IReadOnlyList<Interval> set, Interval interval)
    {
        ArgumentNullException.ThrowIfNull(set);

        foreach (var candidate in set)
        {
            if (candidate.Overlaps(interval))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the sets contain exactly the same intervals, in the same order.
    /// </summary>
    public static bool Equal(IReadOnlyList<Interval> first, IReadOnlyList<Interval> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    private static int Compare(Interval first, Interval second) =>
        Less(first, second) ? -1 : Less(second, first) ? 1 : 0;
}
