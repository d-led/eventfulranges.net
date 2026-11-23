namespace EventfulRanges;

/// <summary>
/// The greedy adjacent merge without walking the cover.
/// </summary>
/// <remarks>
/// The rule it reproduces is the scan's: take the first mergeable pair in
/// canonical cover order, merge it, repeat. Finding that pair by testing every
/// pair, and re-normalizing the cover after each merge, costs a pass over the
/// cover per merge — and a partition hands this function thousands of cells, so
/// a pass per merge is the difference between milliseconds and minutes. Three
/// indexes replace the passes:
/// <list type="bullet">
/// <item>cross-section buckets answer "which boxes could this one merge with",
/// because two boxes can only merge along one axis and must therefore agree on
/// every other axis;</item>
/// <item>a queue of mergeable pairs answers "which pair is first" without a
/// scan, since pairs are ordered by the canonical order of their boxes;</item>
/// <item>a coarse uniform grid answers "which boxes overlap this one", which is
/// all a union needs in order to find the boxes it swallows.</item>
/// </list>
/// <para>
/// A disjoint cover — what a partition produces — takes the shortest path of
/// all: merging two boxes that share only a face leaves the result disjoint from
/// every other box, so no union can ever swallow one, and the search for
/// swallowed boxes is skipped entirely.
/// </para>
/// <para>
/// Both paths compute the same cover; the tests hold them to it. Which one runs
/// is only ever a matter of cost.
/// </para>
/// </remarks>
internal sealed class BoxMerger
{
    private readonly List<Box> boxes;
    private readonly List<bool> live;
    private readonly Dictionary<ulong, List<int>> buckets;
    private readonly CoverGrid grid;
    private readonly PriorityQueue<Pair, (Box First, Box Second)> pairs = new(BoxPairOrder.Instance);
    private readonly int dimensions;
    private readonly bool disjoint;

    private BoxMerger(IReadOnlyList<Box> cover)
    {
        boxes = [.. cover];
        live = [.. boxes.Select(box => !box.IsEmpty)];
        dimensions = boxes[0].Dimensions;
        buckets = new Dictionary<ulong, List<int>>(2 * boxes.Count * dimensions);
        for (var id = 0; id < boxes.Count; id++)
        {
            if (live[id])
            {
                Index(id);
            }
        }

        grid = new CoverGrid(boxes, dimensions);
        Dedupe();
        disjoint = DetectDisjoint();
        for (var id = 0; id < boxes.Count; id++)
        {
            if (live[id])
            {
                Offer(id);
            }
        }
    }

    /// <summary>The merged cover, in canonical order.</summary>
    public static IReadOnlyList<Box> Merge(IReadOnlyList<Box> cover)
    {
        ArgumentNullException.ThrowIfNull(cover);
        return cover.Count == 0 ? [] : new BoxMerger(cover).Run();
    }

    private List<Box> Run()
    {
        while (NextPair() is { } next)
        {
            Merge(next.Pair, next.Merged);
        }

        return Result();
    }

    /// <summary>
    /// The first pair that can still be merged, with the box it becomes; pairs
    /// whose boxes an earlier merge has already consumed are discarded.
    /// </summary>
    private (Pair Pair, Box Merged)? NextPair()
    {
        while (pairs.TryDequeue(out var pair, out _))
        {
            if (!live[pair.First] || !live[pair.Second])
            {
                continue;
            }

            if (Boxes.MergePair(boxes[pair.First], boxes[pair.Second]) is Box merged)
            {
                return (pair, merged);
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces two boxes with their union, then offers the pairs the union
    /// itself forms.
    /// </summary>
    private void Merge(Pair pair, Box merged)
    {
        Kill(pair.First);
        Kill(pair.Second);
        var union = Add(merged);
        Absorb(union);
        Offer(union);
    }

    /// <summary>
    /// Drops every live box another live box covers, folding the metadata of the
    /// dropped box into its subsumer. It is what normalization does to a cover
    /// before a merge, done through the grid instead of by testing every pair: a
    /// box can only be covered by a box it overlaps.
    /// </summary>
    private void Dedupe()
    {
        for (var id = 0; id < boxes.Count; id++)
        {
            if (!live[id])
            {
                continue;
            }

            var subsumer = Subsumer(id);
            if (subsumer < 0)
            {
                continue;
            }

            boxes[subsumer] = boxes[subsumer].WithMeta(Meta.Union(boxes[subsumer].Meta, boxes[id].Meta));
            Kill(id);
        }
    }

    /// <summary>
    /// A live box covering the given one, or -1 when none does. The lowest
    /// identifier wins, so boxes with identical geometry collapse to one however
    /// they arrived.
    /// </summary>
    private int Subsumer(int id)
    {
        var best = -1;
        foreach (var candidate in Candidates(id))
        {
            if (IsCoveredBy(id, candidate))
            {
                best = best < 0 ? candidate : Math.Min(best, candidate);
            }
        }

        return best;
    }

    private bool IsCoveredBy(int covered, int coverer) =>
        coverer != covered && live[coverer] && boxes[coverer].Contains(boxes[covered]);

    /// <summary>The lives boxes that may share a point with the given one.</summary>
    private List<int> Candidates(int id)
    {
        var (ids, exhaustive) = grid.Overlapping(boxes[id]);
        return exhaustive ? AllIds(boxes.Count) : ids;
    }

    /// <summary>
    /// Whether no two live boxes share a point. Merging preserves that — a union
    /// covers only points its halves already covered — so it is established once.
    /// </summary>
    private bool DetectDisjoint()
    {
        for (var id = 0; id < boxes.Count; id++)
        {
            if (!live[id])
            {
                continue;
            }

            foreach (var candidate in Candidates(id))
            {
                if (SharesAPointWith(id, candidate))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool SharesAPointWith(int id, int candidate) =>
        candidate != id && live[candidate] && boxes[id].Overlaps(boxes[candidate]);

    /// <summary>Offers every pair the given box forms with a partner.</summary>
    private void Offer(int id)
    {
        foreach (var partner in Partners(id))
        {
            var pair = OrderedPair(id, partner);
            pairs.Enqueue(pair, (boxes[pair.First], boxes[pair.Second]));
        }
    }

    /// <summary>
    /// The live boxes that can merge with the given one. Two boxes can only
    /// merge along one axis, so a partner has to share a cross-section with it,
    /// and the buckets keyed by those cross-sections hold every candidate.
    /// </summary>
    private List<int> Partners(int id)
    {
        var box = boxes[id];
        var partners = new List<int>();
        for (var axis = 0; axis < dimensions; axis++)
        {
            foreach (var other in Bucket(CrossSectionKey(box, axis)))
            {
                if (other != id && live[other] && Boxes.MergePair(box, boxes[other]) is not null)
                {
                    partners.Add(other);
                }
            }
        }

        return partners;
    }

    /// <summary>The pair with the canonically first box first.</summary>
    private Pair OrderedPair(int first, int second) =>
        boxes[second].CompareTo(boxes[first]) < 0 ? new Pair(second, first) : new Pair(first, second);

    /// <summary>
    /// Folds away every live box the union covers. A disjoint cover has nothing
    /// to fold: the union consists only of points its two halves already
    /// covered, so it cannot cover a box that was disjoint from both of them.
    /// </summary>
    /// <remarks>
    /// The union always survives, too. A box covering it would have covered both
    /// halves, and normalization would have dropped those before the merge.
    /// </remarks>
    private void Absorb(int union)
    {
        if (disjoint)
        {
            return;
        }

        foreach (var candidate in Candidates(union))
        {
            if (candidate != union && live[candidate] && boxes[union].Contains(boxes[candidate]))
            {
                boxes[union] = boxes[union].WithMeta(Meta.Union(boxes[union].Meta, boxes[candidate].Meta));
                Kill(candidate);
            }
        }
    }

    /// <summary>Appends a box, indexes it, and returns its identifier.</summary>
    private int Add(Box box)
    {
        var id = boxes.Count;
        boxes.Add(box);
        live.Add(true);
        Index(id);
        grid.Add(id, box);
        return id;
    }

    /// <summary>
    /// Retires a box: it leaves the indexes and can never be merged again. The
    /// pairs and bucket entries that mention it are dropped when they are next
    /// looked at, and its geometry stays, because the queue orders pairs by it.
    /// </summary>
    private void Kill(int id)
    {
        live[id] = false;
        Unindex(id);
        grid.Remove(id);
    }

    private List<Box> Result()
    {
        var surviving = new List<Box>(boxes.Count);
        for (var id = 0; id < boxes.Count; id++)
        {
            if (live[id])
            {
                surviving.Add(boxes[id]);
            }
        }

        surviving.Sort();
        return surviving;
    }

    /// <summary>Records a box under every axis it could merge along.</summary>
    private void Index(int id)
    {
        for (var axis = 0; axis < dimensions; axis++)
        {
            BucketFor(CrossSectionKey(boxes[id], axis)).Add(id);
        }
    }

    private void Unindex(int id)
    {
        for (var axis = 0; axis < dimensions; axis++)
        {
            Bucket(CrossSectionKey(boxes[id], axis)).Remove(id);
        }
    }

    private List<int> Bucket(ulong key) => buckets.TryGetValue(key, out var ids) ? ids : [];

    private List<int> BucketFor(ulong key)
    {
        if (buckets.TryGetValue(key, out var ids))
        {
            return ids;
        }

        ids = [];
        buckets[key] = ids;
        return ids;
    }

    private static List<int> AllIds(int count)
    {
        var ids = new List<int>(count);
        for (var id = 0; id < count; id++)
        {
            ids.Add(id);
        }

        return ids;
    }

    /// <summary>
    /// Hashes everything about a box except its span on one axis: the corners
    /// and face inclusivity of every other axis. Equal keys are the necessary
    /// condition for a candidate pair, so a pair that can merge always shares a
    /// bucket; <see cref="Boxes.MergePair"/> remains the judge of the rest.
    /// </summary>
    /// <remarks>
    /// The axis is not part of the key: two boxes that differ on one axis hash
    /// the same remaining axes, so a collision costs a rejected candidate check
    /// and never a missed merge. Zeros are folded to positive zero before their
    /// bits are mixed in, because -0 and 0 merge equal but do not share a bit
    /// pattern.
    /// </remarks>
    private static ulong CrossSectionKey(Box box, int axis)
    {
        var hash = Hashing.OffsetBasis;
        for (var dimension = 0; dimension < box.Dimensions; dimension++)
        {
            if (dimension == axis)
            {
                continue;
            }

            hash = Hashing.Mix(hash, Hashing.CanonicalBits(box.Min[dimension]));
            hash = Hashing.Mix(hash, Hashing.CanonicalBits(box.Max[dimension]));
            hash = Hashing.Mix(hash, (ulong)box.LowestBound(dimension) | ((ulong)box.HighestBound(dimension) << 8));
        }

        return hash;
    }

    /// <summary>Two boxes that can merge, in canonical cover order.</summary>
    private readonly record struct Pair(int First, int Second);

    /// <summary>
    /// Orders pairs the way the pairwise scan would meet them: by the first box,
    /// then by the second. Pairs whose boxes compare equal are interchangeable —
    /// that can only happen once a merge has retired one of them, and retired
    /// pairs are discarded without being merged.
    /// </summary>
    private sealed class BoxPairOrder : IComparer<(Box First, Box Second)>
    {
        public static readonly BoxPairOrder Instance = new();

        public int Compare((Box First, Box Second) first, (Box First, Box Second) second)
        {
            var byFirst = first.First.CompareTo(second.First);
            return byFirst != 0 ? byFirst : first.Second.CompareTo(second.Second);
        }
    }
}
