namespace EventfulRanges;

public static partial class Intervals
{
    /// <summary>
    /// Partitions the union of <paramref name="intervals"/> into maximal
    /// pieces, each labeled with the index of the best interval covering it.
    /// Points that no interval covers are gaps and are omitted. The result is
    /// canonical: pieces are disjoint and ordered, and each piece is the whole
    /// run of one winner.
    /// </summary>
    /// <param name="intervals">The intervals to sweep; every one must be well-formed.</param>
    /// <param name="better">
    /// Whether the first index outranks the second on the points both cover.
    /// </param>
    public static IReadOnlyList<Cover> Sweep(
        IReadOnlyList<Interval> intervals,
        Func<int, int, bool> better)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        ArgumentNullException.ThrowIfNull(better);

        var events = BuildEvents(intervals);
        var heap = new CoverHeap(better, intervals.Count);
        var runs = new List<FineRun>();
        var previous = default(FinePoint);
        var started = false;

        for (var index = 0; index < events.Count;)
        {
            var point = events[index].Point;
            if (started && heap.TryBest(out var winner))
            {
                AppendRun(runs, previous, point, winner);
            }

            index = ApplyEvents(heap, events, index, point);
            previous = point;
            started = true;
        }

        return Covers(runs);
    }

    private static List<SweepEvent> BuildEvents(IReadOnlyList<Interval> intervals)
    {
        var events = new List<SweepEvent>(2 * intervals.Count);
        for (var index = 0; index < intervals.Count; index++)
        {
            var (low, high) = Fine(intervals[index]);
            events.Add(new SweepEvent(low, index, IsStart: true));
            events.Add(new SweepEvent(high, index, IsStart: false));
        }

        events.Sort(CompareEvents);
        return events;
    }

    private static int CompareEvents(SweepEvent first, SweepEvent second)
    {
        if (FineLess(first.Point, second.Point))
        {
            return -1;
        }

        if (FineLess(second.Point, first.Point))
        {
            return 1;
        }

        return first.Index.CompareTo(second.Index);
    }

    private static int ApplyEvents(
        CoverHeap heap,
        List<SweepEvent> events,
        int from,
        FinePoint point)
    {
        var index = from;
        while (index < events.Count && events[index].Point == point)
        {
            if (events[index].IsStart)
            {
                heap.Push(events[index].Index);
            }
            else
            {
                heap.Retire(events[index].Index);
            }

            index++;
        }

        return index;
    }

    private static void AppendRun(List<FineRun> runs, FinePoint low, FinePoint high, int index)
    {
        if (runs.Count > 0 && runs[^1].Index == index && runs[^1].High == low)
        {
            runs[^1] = runs[^1] with { High = high };
            return;
        }

        runs.Add(new FineRun(low, high, index));
    }

    private static List<Cover> Covers(List<FineRun> runs)
    {
        var covers = new List<Cover>(runs.Count);
        foreach (var run in runs)
        {
            covers.Add(new Cover(FromFine(run.Low, run.High), run.Index));
        }

        return covers;
    }

    /// <summary>
    /// A point on the doubled line the sweep walks: every real coordinate
    /// appears twice, as the point itself and as the point just after it.
    /// Closed endpoints include the point, open endpoints do not, so the sweep
    /// needs no open/closed special cases.
    /// </summary>
    private readonly record struct FinePoint(double X, bool Past);

    /// <summary>
    /// Orders fine points: by coordinate, then the point itself before the
    /// point just after it.
    /// </summary>
    private static bool FineLess(FinePoint first, FinePoint second)
    {
        if (first.X != second.X)
        {
            return first.X < second.X;
        }

        return !first.Past && second.Past;
    }

    /// <summary>
    /// The half-open span of fine points an interval covers: a fine point
    /// <c>p</c> satisfies <c>low &lt;= p &lt; high</c> exactly when the real
    /// point <c>p.X</c> lies in the interval.
    /// </summary>
    private static (FinePoint Low, FinePoint High) Fine(Interval interval) =>
        (new FinePoint(interval.Start, interval.StartBound == Bound.Open),
         new FinePoint(interval.End, interval.EndBound == Bound.Closed));

    private static Interval FromFine(FinePoint low, FinePoint high) =>
        new(low.X,
            high.X,
            low.Past ? Bound.Open : Bound.Closed,
            high.Past ? Bound.Closed : Bound.Open);

    private readonly record struct SweepEvent(FinePoint Point, int Index, bool IsStart);

    private readonly record struct FineRun(FinePoint Low, FinePoint High, int Index);

    /// <summary>
    /// A binary heap of interval indices ordered by the caller's priority,
    /// with lazy deletion: a retired index is skipped when it reaches the
    /// root, which is cheaper than a decrease-key.
    /// </summary>
    private sealed class CoverHeap
    {
        private readonly Func<int, int, bool> better;
        private readonly bool[] retired;
        private readonly List<int> items;

        public CoverHeap(Func<int, int, bool> better, int size)
        {
            this.better = better;
            retired = new bool[size];
            items = new List<int>(size);
        }

        public void Push(int index)
        {
            items.Add(index);
            SiftUp(items.Count - 1);
        }

        public void Retire(int index) => retired[index] = true;

        public bool TryBest(out int index)
        {
            while (items.Count > 0)
            {
                var candidate = items[0];
                if (!retired[candidate])
                {
                    index = candidate;
                    return true;
                }

                RemoveRoot();
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Orders two indices by priority, falling back to the lower index so
        /// the order is total and the sweep stays deterministic.
        /// </summary>
        private bool Precedes(int first, int second)
        {
            if (better(first, second))
            {
                return true;
            }

            if (better(second, first))
            {
                return false;
            }

            return first < second;
        }

        private void SiftUp(int position)
        {
            while (position > 0)
            {
                var parent = (position - 1) / 2;
                if (!Precedes(items[position], items[parent]))
                {
                    return;
                }

                Swap(position, parent);
                position = parent;
            }
        }

        private void RemoveRoot()
        {
            var last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);
            if (items.Count > 0)
            {
                SiftDown(0);
            }
        }

        private void SiftDown(int position)
        {
            while (true)
            {
                var left = (2 * position) + 1;
                if (left >= items.Count)
                {
                    return;
                }

                var right = left + 1;
                var child = right < items.Count && Precedes(items[right], items[left]) ? right : left;
                if (!Precedes(items[child], items[position]))
                {
                    return;
                }

                Swap(position, child);
                position = child;
            }
        }

        private void Swap(int first, int second) =>
            (items[first], items[second]) = (items[second], items[first]);
    }
}
