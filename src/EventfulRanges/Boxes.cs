namespace EventfulRanges;

/// <summary>
/// The set algebra of boxes: what a cover is, what two covers cover together,
/// and what is left when one cover is taken out of another.
/// </summary>
/// <remarks>
/// A cover is a list of boxes; it stands for every point at least one of them
/// holds. Normalization is canonical: empty boxes and boxes another box already
/// covers are dropped, the survivors are sorted, and the metadata of a dropped
/// box is joined into the box that absorbed it. Difference is exact — a point
/// belongs to the result exactly when it belongs to the first cover and not the
/// second — so both sides of a boundary keep the face inclusivity they had.
/// </remarks>
public static class Boxes
{
    /// <summary>
    /// The canonical cover of <paramref name="boxes"/>, joining metadata with
    /// <see cref="Meta.Union"/>.
    /// </summary>
    public static IReadOnlyList<Box> Normalize(IReadOnlyList<Box> boxes) =>
        Normalize(boxes, Meta.Union);

    /// <summary>The canonical cover of <paramref name="boxes"/>.</summary>
    /// <param name="boxes">The boxes to normalize.</param>
    /// <param name="merge">How the metadata of absorbed boxes joins.</param>
    public static IReadOnlyList<Box> Normalize(IReadOnlyList<Box> boxes, MetaMerge merge)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(merge);

        var kept = new List<Box>(boxes.Count);
        foreach (var candidate in boxes)
        {
            if (candidate.IsEmpty)
            {
                continue;
            }

            var survivor = Absorb(candidate, kept, merge);
            if (survivor is not null)
            {
                kept.Add(survivor);
            }
        }

        kept.Sort();
        return kept;
    }

    /// <summary>The canonical cover of the boxes of both covers.</summary>
    public static IReadOnlyList<Box> Union(IReadOnlyList<Box> first, IReadOnlyList<Box> second) =>
        Union(first, second, Meta.Union);

    /// <summary>The canonical cover of the boxes of both covers.</summary>
    public static IReadOnlyList<Box> Union(
        IReadOnlyList<Box> first,
        IReadOnlyList<Box> second,
        MetaMerge merge)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return Normalize([.. first, .. second], merge);
    }

    /// <summary>
    /// The canonical cover of every point in <paramref name="first"/> that is
    /// not in <paramref name="second"/>. Pieces carved out of a box keep its
    /// metadata.
    /// </summary>
    public static IReadOnlyList<Box> Difference(IReadOnlyList<Box> first, IReadOnlyList<Box> second) =>
        Difference(first, second, Meta.Union);

    /// <summary>
    /// The canonical cover of every point in <paramref name="first"/> that is
    /// not in <paramref name="second"/>.
    /// </summary>
    public static IReadOnlyList<Box> Difference(
        IReadOnlyList<Box> first,
        IReadOnlyList<Box> second,
        MetaMerge merge)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        IReadOnlyList<Box> rest = Normalize(first, merge);
        foreach (var carved in Normalize(second, merge))
        {
            rest = Normalize(rest.SelectMany(box => Subtract(box, carved)).ToList(), merge);
        }

        return rest;
    }

    /// <summary>
    /// Adds one box to a normalized cover without re-normalizing it: boxes the
    /// new box covers are dropped and the new box lands in canonical order,
    /// unless a box already covers it.
    /// </summary>
    public static IReadOnlyList<Box> InsertNormalized(IReadOnlyList<Box> cover, Box box)
    {
        ArgumentNullException.ThrowIfNull(cover);
        ArgumentNullException.ThrowIfNull(box);

        if (box.IsEmpty)
        {
            return cover;
        }

        var kept = new List<Box>(cover.Count + 1);
        var covered = false;
        foreach (var existing in cover)
        {
            if (box.Contains(existing))
            {
                continue;
            }

            covered |= existing.Contains(box);
            kept.Add(existing);
        }

        if (covered)
        {
            return kept;
        }

        kept.Add(box);
        kept.Sort();
        return kept;
    }

    /// <summary>Whether any box of the cover holds <paramref name="point"/>.</summary>
    public static bool Contains(IReadOnlyList<Box> cover, IReadOnlyList<double> point)
    {
        ArgumentNullException.ThrowIfNull(cover);
        ArgumentNullException.ThrowIfNull(point);

        return cover.Any(box => box.Contains(point));
    }

    /// <summary>Whether any box of the cover shares a point with <paramref name="box"/>.</summary>
    public static bool Overlaps(IReadOnlyList<Box> cover, Box box)
    {
        ArgumentNullException.ThrowIfNull(cover);
        ArgumentNullException.ThrowIfNull(box);

        return cover.Any(candidate => candidate.Overlaps(box));
    }

    /// <summary>Whether the two covers hold exactly the same points.</summary>
    public static bool Equal(IReadOnlyList<Box> first, IReadOnlyList<Box> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var left = Normalize(first);
        var right = Normalize(second);
        return left.SequenceEqual(right);
    }

    /// <summary>
    /// Folds <paramref name="candidate"/> into the boxes already kept, and
    /// returns the box to keep — carrying the metadata of every box it covers —
    /// or <c>null</c> when a kept box already covers it. A box is a reference
    /// type, so the replacement has to come back as a return value.
    /// </summary>
    private static Box? Absorb(Box candidate, List<Box> kept, MetaMerge merge)
    {
        var covered = false;
        for (var index = kept.Count - 1; index >= 0; index--)
        {
            if (candidate.Contains(kept[index]))
            {
                candidate = candidate.WithMeta(merge(candidate.Meta, kept[index].Meta));
                kept.RemoveAt(index);
            }
            else if (kept[index].Contains(candidate))
            {
                kept[index] = kept[index].WithMeta(merge(kept[index].Meta, candidate.Meta));
                covered = true;
            }
        }

        return covered ? null : candidate;
    }

    /// <summary>
    /// Merges every pair of boxes that touch edge to edge and agree on every
    /// other axis, so the cover no longer contains such a pair. The covered
    /// points do not change, and the metadata of two merged boxes is joined into
    /// the box that replaces them.
    /// </summary>
    /// <remarks>
    /// The greedy takes the first mergeable pair in canonical cover order and
    /// repeats until none is left, which makes the result deterministic but not
    /// a provably minimal rectangle cover. It is safe to use as a
    /// <see cref="Canonicalizer"/> for that reason. A pair of boxes that touch
    /// with both faces open leaves a pinhole between them, which one box cannot
    /// represent, so such a pair stays apart.
    /// </remarks>
    public static IReadOnlyList<Box> MergeAdjacent(IReadOnlyList<Box> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        var cover = Normalize(boxes).ToList();
        while (FirstMergeablePair(cover) is { } pair)
        {
            cover.RemoveAt(pair.Second);
            cover.RemoveAt(pair.First);
            cover = InsertNormalized(cover, pair.Merged).ToList();
        }

        return cover;
    }

    /// <summary>
    /// The first pair in canonical order that merges, with the box it becomes;
    /// <c>null</c> when the cover holds no such pair.
    /// </summary>
    private static (int First, int Second, Box Merged)? FirstMergeablePair(List<Box> cover)
    {
        for (var first = 0; first < cover.Count; first++)
        {
            for (var second = first + 1; second < cover.Count; second++)
            {
                if (Merge(cover[first], cover[second]) is Box merged)
                {
                    return (first, second, merged);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The box the pair becomes, or <c>null</c> when they do not merge: two boxes
    /// merge when they differ on exactly one axis and meet there without leaving
    /// a pinhole between them.
    /// </summary>
    private static Box? Merge(Box first, Box second)
    {
        if (first.Dimensions != second.Dimensions)
        {
            return null;
        }

        var axis = OnlyDifferingAxis(first, second);
        if (axis < 0 || Touching(first, second, axis) is not { } pair || LeavesAPinhole(pair, axis))
        {
            return null;
        }

        return new Box(pair.Low.Min, pair.High.Max, pair.Low.MinBound, pair.High.MaxBound)
            .WithMeta(Meta.Union(first.Meta, second.Meta));
    }

    /// <summary>
    /// The pair ordered so that <paramref name="axis"/> ends for the low box where
    /// it begins for the high one, or <c>null</c> when they leave a gap or overlap
    /// there.
    /// </summary>
    private static (Box Low, Box High)? Touching(Box first, Box second, int axis)
    {
        if (first.Max[axis] == second.Min[axis])
        {
            return (first, second);
        }

        return second.Max[axis] == first.Min[axis] ? (second, first) : null;
    }

    /// <summary>
    /// Whether both boxes exclude the face they share, which leaves a point out of
    /// the union that no single box can represent.
    /// </summary>
    private static bool LeavesAPinhole((Box Low, Box High) pair, int axis) =>
        pair.Low.HighestBound(axis) == Bound.Open && pair.High.LowestBound(axis) == Bound.Open;

    /// <summary>
    /// The one axis on which the boxes differ, or -1 when they differ on no axis
    /// or on more than one. Boxes that agree on a span but not on its faces count
    /// as differing there, so they never merge.
    /// </summary>
    private static int OnlyDifferingAxis(Box first, Box second)
    {
        var differing = -1;
        for (var axis = 0; axis < first.Dimensions; axis++)
        {
            if (AgreeOnSpan(first, second, axis))
            {
                if (!AgreeOnFaces(first, second, axis))
                {
                    return -1;
                }

                continue;
            }

            if (differing >= 0)
            {
                return -1;
            }

            differing = axis;
        }

        return differing;
    }

    private static bool AgreeOnSpan(Box first, Box second, int axis) =>
        first.Min[axis] == second.Min[axis] && first.Max[axis] == second.Max[axis];

    private static bool AgreeOnFaces(Box first, Box second, int axis) =>
        first.LowestBound(axis) == second.LowestBound(axis)
        && first.HighestBound(axis) == second.HighestBound(axis);

    /// <summary>
    /// The disjoint boxes covering <paramref name="box"/> without
    /// <paramref name="carved"/>. The sweep visits one axis at a time and keeps
    /// a worklist of the pieces still inside the carved box on the axes already
    /// swept: on each axis a piece splits into the parts outside the carved
    /// span, which are final, and the part inside it, which is deferred. The
    /// worklist makes this a bounded loop, never recursion.
    /// </summary>
    private static List<Box> Subtract(Box box, Box carved)
    {
        if (!box.Overlaps(carved))
        {
            return [box];
        }

        var result = new List<Box>();
        var pending = new List<Box> { box };
        for (var axis = 0; axis < box.Dimensions; axis++)
        {
            var deferred = new List<Box>(pending.Count);
            foreach (var piece in pending)
            {
                if (carved.CoversOnAxis(piece, axis))
                {
                    deferred.Add(piece);
                    continue;
                }

                var span = piece.Slice(axis);
                foreach (var outside in Intervals.Difference([span], [carved.Slice(axis)]))
                {
                    result.Add(piece.WithSpan(axis, outside));
                }

                deferred.Add(piece.WithSpan(axis, Interval.Intersection(span, carved.Slice(axis))));
            }

            pending = deferred;
        }

        return result;
    }
}
