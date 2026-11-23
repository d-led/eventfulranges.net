using Shouldly;

namespace EventfulRanges.Tests;

public sealed class IntervalsTests
{
    [Fact]
    public void Normalize_sorts_ranges_into_canonical_order()
    {
        var normalized = Intervals.Normalize(new[]
        {
            new Interval(20, 30),
            new Interval(1, 10),
        });

        var expected = new[] { new Interval(1, 10), new Interval(20, 30) };
        normalized.ShouldBe(expected);
    }

    [Fact]
    public void Normalize_merges_overlapping_and_touching_ranges()
    {
        var normalized = Intervals.Normalize(new[]
        {
            new Interval(5, 9),
            new Interval(1, 5),
            new Interval(3, 4),
        });

        normalized.ShouldBe(new[] { new Interval(1, 9) });
    }

    [Fact]
    public void Normalize_keeps_a_hole_where_two_ranges_do_not_touch()
    {
        var normalized = Intervals.Normalize(new[]
        {
            new Interval(5, 9, Bound.Open, Bound.Closed),
            new Interval(1, 5, Bound.Closed, Bound.Open),
        });

        var expected = new[]
        {
            new Interval(1, 5, Bound.Closed, Bound.Open),
            new Interval(5, 9, Bound.Open, Bound.Closed),
        };
        normalized.ShouldBe(expected);
    }

    [Fact]
    public void Normalize_drops_ranges_that_contain_no_point()
    {
        var normalized = Intervals.Normalize(new[]
        {
            new Interval(2, 2, Bound.Open, Bound.Closed),
            new Interval(1, 3),
        });

        normalized.ShouldBe(new[] { new Interval(1, 3) });
    }

    [Fact]
    public void Normalize_merges_a_point_interval_into_the_range_that_starts_after_it()
    {
        var normalized = Intervals.Normalize(new[]
        {
            new Interval(0, 0),
            new Interval(0, 7, Bound.Open, Bound.Closed),
        });

        normalized.ShouldBe(new[] { new Interval(0, 7) });
    }

    [Fact]
    public void Normalize_of_nothing_is_nothing()
    {
        Intervals.Normalize(Array.Empty<Interval>()).ShouldBeEmpty();
    }

    [Fact]
    public void Union_merges_both_sets_into_one_canonical_set()
    {
        var union = Intervals.Union(
            new[] { new Interval(1, 5) },
            new[] { new Interval(4, 9) });

        union.ShouldBe(new[] { new Interval(1, 9) });
    }

    [Fact]
    public void Difference_subtracts_the_cut_from_the_ranges()
    {
        var difference = Intervals.Difference(
            new[] { new Interval(1, 10) },
            new[] { new Interval(3, 5) });

        var expected = new[] { "[1,3)", "(5,10]" };
        difference.Select(interval => interval.ToString()).ShouldBe(expected);
    }

    [Fact]
    public void Difference_with_a_cut_that_overshoots_removes_the_range()
    {
        var difference = Intervals.Difference(
            new[] { new Interval(1, 10) },
            new[] { new Interval(-5, 15) });

        difference.ShouldBeEmpty();
    }

    [Fact]
    public void Difference_without_a_cut_returns_the_normalized_ranges()
    {
        var difference = Intervals.Difference(
            new[] { new Interval(3, 5), new Interval(1, 10) },
            Array.Empty<Interval>());

        difference.ShouldBe(new[] { new Interval(1, 10) });
    }

    [Fact]
    public void Difference_of_a_set_with_itself_is_empty()
    {
        var set = new[] { new Interval(1, 10), new Interval(20, 30) };

        Intervals.Difference(set, set).ShouldBeEmpty();
    }

    [Fact]
    public void Contains_reports_membership_in_any_range_of_the_set()
    {
        var set = new[] { new Interval(1, 5), new Interval(10, 20) };

        Intervals.Contains(set, 3).ShouldBeTrue();
        Intervals.Contains(set, 15).ShouldBeTrue();
        Intervals.Contains(set, 7).ShouldBeFalse();
    }

    [Fact]
    public void Overlaps_reports_whether_any_range_shares_a_point()
    {
        var set = new[] { new Interval(1, 5), new Interval(10, 20) };

        Intervals.Overlaps(set, new Interval(4, 11)).ShouldBeTrue();
        Intervals.Overlaps(set, new Interval(6, 9)).ShouldBeFalse();
    }

    [Fact]
    public void Equal_requires_the_same_ranges_in_the_same_order()
    {
        var set = new[] { new Interval(1, 5), new Interval(10, 20) };

        Intervals.Equal(set, new[] { new Interval(1, 5), new Interval(10, 20) }).ShouldBeTrue();
        Intervals.Equal(set, new[] { new Interval(10, 20), new Interval(1, 5) }).ShouldBeFalse();
        Intervals.Equal(set, new[] { new Interval(1, 5) }).ShouldBeFalse();
    }

    [Fact]
    public void Less_orders_by_start_then_inclusivity_then_end()
    {
        Intervals.Less(new Interval(1, 5), new Interval(2, 5)).ShouldBeTrue();

        var closedStart = new Interval(1, 5);
        var openStart = new Interval(1, 5, Bound.Open, Bound.Closed);
        Intervals.Less(closedStart, openStart).ShouldBeTrue();
        Intervals.Less(openStart, closedStart).ShouldBeFalse();
    }
}
