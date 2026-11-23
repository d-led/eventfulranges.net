using CsCheck;
using Shouldly;

namespace EventfulRanges.Tests.Properties;

/// <summary>
/// Differential properties: every answer is compared with a brute-force
/// oracle over a probe grid, so a bug cannot hide behind a shared algorithm.
/// </summary>
public sealed class IntervalAlgebraProperties
{
    // Endpoints come from a small integer grid, so shared endpoints, point
    // intervals, and open/closed disagreements are common rather than rare.
    private static readonly Gen<Interval> GenInterval =
        from a in Gen.Int[0, 10]
        from b in Gen.Int[0, 10]
        from startClosed in Gen.Bool
        from endClosed in Gen.Bool
        let start = Math.Min(a, b)
        let end = Math.Max(a, b)
        select start == end
            ? new Interval(start, end)
            : new Interval(
                start,
                end,
                startClosed ? Bound.Closed : Bound.Open,
                endClosed ? Bound.Closed : Bound.Open);

    private static readonly Gen<IReadOnlyList<Interval>> GenIntervalSet =
        GenInterval.List[0, 5].Select(list => (IReadOnlyList<Interval>)list);

    // Coverage changes only at an endpoint, and every endpoint is an integer,
    // so probing each integer and the point just after it is exhaustive.
    private static readonly double[] ProbePoints =
        Enumerable.Range(-1, 13).SelectMany(k => new[] { k + 0.5, k + 1.0 }).ToArray();

    [Fact]
    public void Normalize_preserves_which_points_are_covered()
    {
        GenIntervalSet.Sample(intervals =>
        {
            var normalized = Intervals.Normalize(intervals);

            foreach (var point in ProbePoints)
            {
                Intervals.Contains(normalized, point)
                    .ShouldBe(Intervals.Contains(intervals, point));
            }
        });
    }

    [Fact]
    public void Normalize_produces_sorted_ranges_that_neither_overlap_nor_touch()
    {
        GenIntervalSet.Sample(intervals =>
        {
            var normalized = Intervals.Normalize(intervals);

            for (var index = 1; index < normalized.Count; index++)
            {
                Intervals.Less(normalized[index - 1], normalized[index]).ShouldBeTrue();
                normalized[index - 1].Overlaps(normalized[index]).ShouldBeFalse();
                normalized[index - 1].Touches(normalized[index]).ShouldBeFalse();
            }
        });
    }

    [Fact]
    public void Normalize_is_idempotent()
    {
        GenIntervalSet.Sample(intervals =>
        {
            var once = Intervals.Normalize(intervals);

            Intervals.Equal(once, Intervals.Normalize(once)).ShouldBeTrue();
        });
    }

    [Fact]
    public void Normalize_does_not_depend_on_the_order_of_its_input()
    {
        Gen.Select(GenIntervalSet, Gen.Int[0, 5]).Sample(pair =>
        {
            var (intervals, rotation) = pair;
            var rotated = intervals.Skip(rotation).Concat(intervals.Take(rotation)).ToList();

            Intervals.Equal(Intervals.Normalize(intervals), Intervals.Normalize(rotated)).ShouldBeTrue();
        });
    }

    [Fact]
    public void Union_covers_a_point_exactly_when_either_input_does()
    {
        Gen.Select(GenIntervalSet, GenIntervalSet).Sample(pair =>
        {
            var (first, second) = pair;
            var union = Intervals.Union(first, second);

            foreach (var point in ProbePoints)
            {
                Intervals.Contains(union, point).ShouldBe(
                    Intervals.Contains(first, point) || Intervals.Contains(second, point));
            }
        });
    }

    [Fact]
    public void Union_is_commutative()
    {
        Gen.Select(GenIntervalSet, GenIntervalSet).Sample(pair =>
        {
            var (first, second) = pair;

            Intervals.Equal(Intervals.Union(first, second), Intervals.Union(second, first))
                .ShouldBeTrue();
        });
    }

    [Fact]
    public void Difference_covers_a_point_exactly_when_the_first_set_does_and_the_second_does_not()
    {
        Gen.Select(GenIntervalSet, GenIntervalSet).Sample(pair =>
        {
            var (first, second) = pair;
            var difference = Intervals.Difference(first, second);

            foreach (var point in ProbePoints)
            {
                Intervals.Contains(difference, point).ShouldBe(
                    Intervals.Contains(first, point) && !Intervals.Contains(second, point));
            }
        });
    }

    [Fact]
    public void Sweep_partitions_the_union_into_ordered_disjoint_pieces()
    {
        GenIntervalSet.Sample(intervals =>
        {
            var covers = Intervals.Sweep(intervals, (left, right) => left < right);
            var union = Intervals.Normalize(intervals);
            var pieces = covers.Select(cover => cover.Interval).ToList();

            foreach (var point in ProbePoints)
            {
                Intervals.Contains(pieces, point)
                    .ShouldBe(Intervals.Contains(union, point));
            }

            for (var index = 1; index < covers.Count; index++)
            {
                Intervals.Less(covers[index - 1].Interval, covers[index].Interval).ShouldBeTrue();
                covers[index - 1].Interval.Overlaps(covers[index].Interval).ShouldBeFalse();
            }
        });
    }

    [Fact]
    public void Sweep_names_the_lowest_covering_index_as_the_winner()
    {
        GenIntervalSet.Sample(intervals =>
        {
            var covers = Intervals.Sweep(intervals, (left, right) => left < right);

            foreach (var cover in covers)
            {
                foreach (var point in ProbePoints.Where(cover.Interval.Contains))
                {
                    var covering = intervals
                        .Select((interval, index) => (interval, index))
                        .Where(candidate => candidate.interval.Contains(point))
                        .Select(candidate => candidate.index)
                        .DefaultIfEmpty(-1)
                        .Min();

                    cover.Index.ShouldBe(covering);
                }
            }
        });
    }
}
