namespace EventfulRanges;

/// <summary>
/// A coarse uniform grid over a cover, answering "which boxes overlap this one".
/// </summary>
/// <remarks>
/// Bins are sized to hold about one box each. A box too wide to list is kept
/// aside and returned by every query, so an answer is always a superset of the
/// real overlaps however the cover is shaped — a caller filters the answer, and
/// never misses a box it needed.
/// </remarks>
internal sealed class CoverGrid
{
    /// <summary>
    /// How far a registered box may reach: four bins per axis covers a box up to
    /// four times the typical extent. Beyond that a box is cheaper to return on
    /// every query than to list in every bin it touches.
    /// </summary>
    private const int MaxBinsPerAxis = 4;

    private readonly double[] cell;
    private readonly double[] origin;
    private readonly Dictionary<ulong, List<int>> bins;
    private readonly List<List<ulong>> binOf;
    private readonly List<int> wide = [];
    private readonly Dictionary<int, int> wideAt = [];

    public CoverGrid(IReadOnlyList<Box> boxes, int dimensions)
    {
        var (corner, extent) = Bounds(boxes, dimensions);
        origin = corner;
        cell = new double[dimensions];
        bins = new Dictionary<ulong, List<int>>(4 * boxes.Count);
        binOf = [];
        var side = BinsPerAxis(boxes.Count, dimensions);
        for (var axis = 0; axis < dimensions; axis++)
        {
            var width = extent[axis] / side;
            // A cover flat on this axis still needs a bin width to divide by.
            cell[axis] = width > 0 ? width : 1;
        }

        for (var id = 0; id < boxes.Count; id++)
        {
            Add(id, boxes[id]);
        }
    }

    /// <summary>Registers a box under every bin it touches.</summary>
    public void Add(int id, Box box)
    {
        Grow(id);
        var (low, high, fits) = BinRange(box);
        if (!fits)
        {
            wideAt[id] = wide.Count;
            wide.Add(id);
            return;
        }

        var keys = new List<ulong>(BinsBudget(cell.Length));
        EachBin(low, high, key =>
        {
            BucketFor(key).Add(id);
            keys.Add(key);
        });
        binOf[id] = keys;
    }

    /// <summary>Unregisters a box from wherever it was registered.</summary>
    public void Remove(int id)
    {
        if (wideAt.Remove(id, out var at))
        {
            wide[at] = wide[^1];
            wide.RemoveAt(wide.Count - 1);
            if (at < wide.Count)
            {
                wideAt[wide[at]] = at;
            }

            return;
        }

        foreach (var key in binOf[id])
        {
            if (bins.TryGetValue(key, out var ids))
            {
                ids.Remove(id);
            }
        }

        binOf[id] = [];
    }

    /// <summary>
    /// The boxes that may share a point with the given one, with a flag saying
    /// the box was too wide to look up and the caller has to consider the whole
    /// cover.
    /// </summary>
    public (List<int> Ids, bool Exhaustive) Overlapping(Box box)
    {
        var (low, high, fits) = BinRange(box);
        if (!fits)
        {
            return ([], true);
        }

        var ids = new List<int>();
        EachBin(low, high, key =>
        {
            if (bins.TryGetValue(key, out var found))
            {
                ids.AddRange(found);
            }
        });
        ids.AddRange(wide);
        return (ids, false);
    }

    private void Grow(int id)
    {
        while (binOf.Count <= id)
        {
            binOf.Add([]);
        }
    }

    /// <summary>
    /// The inclusive bin index the box touches on each axis, reporting false when
    /// it touches too many bins to list.
    /// </summary>
    private (int[] Low, int[] High, bool Fits) BinRange(Box box)
    {
        var low = new int[cell.Length];
        var high = new int[cell.Length];
        var reached = 1;
        var budget = BinsBudget(cell.Length);
        for (var axis = 0; axis < cell.Length; axis++)
        {
            low[axis] = Bin(box.Min[axis], axis);
            high[axis] = Bin(box.Max[axis], axis);
            reached *= high[axis] - low[axis] + 1;
            if (reached > budget)
            {
                return ([], [], false);
            }
        }

        return (low, high, true);
    }

    private int Bin(double coordinate, int axis) =>
        (int)Math.Floor((coordinate - origin[axis]) / cell[axis]);

    /// <summary>Calls the visit with the key of every bin in the inclusive range.</summary>
    private static void EachBin(int[] low, int[] high, Action<ulong> visit)
    {
        var at = new int[low.Length];
        Array.Copy(low, at, low.Length);
        while (true)
        {
            visit(BinKey(at));
            var axis = 0;
            for (; axis < at.Length; axis++)
            {
                at[axis]++;
                if (at[axis] <= high[axis])
                {
                    break;
                }

                at[axis] = low[axis];
            }

            if (axis == at.Length)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Hashes a bin coordinate. The coordinate is mixed in as the whole number it
    /// is, because bins are counted, never measured; a separator keeps [1,2] and
    /// [12] apart, and a coincidental collision only adds a candidate the caller
    /// rejects.
    /// </summary>
    private static ulong BinKey(int[] at)
    {
        var hash = Hashing.OffsetBasis;
        foreach (var coordinate in at)
        {
            hash = Hashing.Mix(hash, Hashing.CanonicalBits(coordinate));
            hash = Hashing.Separate(hash);
        }

        return hash;
    }

    /// <summary>
    /// The smallest bin count whose grid holds one box per bin, capped so the
    /// grid stays coarse.
    /// </summary>
    private static int BinsPerAxis(int count, int dimensions)
    {
        var side = 1;
        var capacity = 1;
        while (capacity < count && side < 64)
        {
            side++;
            capacity = 1;
            for (var axis = 0; axis < dimensions; axis++)
            {
                capacity *= side;
            }
        }

        return side;
    }

    /// <summary>How many bins one box may be listed in.</summary>
    private static int BinsBudget(int dimensions)
    {
        var budget = 1;
        for (var axis = 0; axis < dimensions; axis++)
        {
            budget *= MaxBinsPerAxis;
        }

        return budget;
    }

    /// <summary>The lower corner of the cover, and its extent per axis.</summary>
    private static (double[] Corner, double[] Extent) Bounds(IReadOnlyList<Box> boxes, int dimensions)
    {
        var corner = new double[dimensions];
        var extent = new double[dimensions];
        if (boxes.Count == 0)
        {
            return (corner, extent);
        }

        var upper = new double[dimensions];
        boxes[0].Min.CopyTo(corner, 0);
        boxes[0].Max.CopyTo(upper, 0);
        foreach (var box in boxes)
        {
            for (var axis = 0; axis < dimensions; axis++)
            {
                corner[axis] = Math.Min(corner[axis], box.Min[axis]);
                upper[axis] = Math.Max(upper[axis], box.Max[axis]);
            }
        }

        for (var axis = 0; axis < dimensions; axis++)
        {
            extent[axis] = upper[axis] - corner[axis];
        }

        return (corner, extent);
    }

    private List<int> BucketFor(ulong key)
    {
        if (bins.TryGetValue(key, out var ids))
        {
            return ids;
        }

        ids = [];
        bins[key] = ids;
        return ids;
    }
}
