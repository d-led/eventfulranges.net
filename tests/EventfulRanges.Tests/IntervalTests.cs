using Shouldly;

namespace EventfulRanges.Tests;

public sealed class IntervalTests
{
    [Fact]
    public void The_default_value_is_the_point_interval_zero_to_zero()
    {
        var interval = default(Interval);

        interval.ShouldBe(new Interval(0, 0));
        interval.IsValid.ShouldBeTrue();
        interval.Contains(0).ShouldBeTrue();
        interval.Contains(1).ShouldBeFalse();
    }

    [Fact]
    public void A_constructed_interval_is_closed_on_both_sides()
    {
        var interval = new Interval(1, 10);

        interval.StartBound.ShouldBe(Bound.Closed);
        interval.EndBound.ShouldBe(Bound.Closed);
    }

    [Theory]
    [InlineData(double.NaN, 1, Bound.Closed, Bound.Closed, IntervalError.NotANumber)]
    [InlineData(0, double.PositiveInfinity, Bound.Closed, Bound.Closed, IntervalError.NotFinite)]
    [InlineData(5, 1, Bound.Closed, Bound.Closed, IntervalError.Inverted)]
    [InlineData(2, 2, Bound.Open, Bound.Closed, IntervalError.Empty)]
    [InlineData(2, 2, Bound.Closed, Bound.Open, IntervalError.Empty)]
    [InlineData(2, 2, Bound.Open, Bound.Open, IntervalError.Empty)]
    [InlineData(2, 2, Bound.Closed, Bound.Closed, IntervalError.None)]
    [InlineData(1, 2, Bound.Open, Bound.Open, IntervalError.None)]
    public void Validate_names_the_first_problem(
        double start,
        double end,
        Bound startBound,
        Bound endBound,
        IntervalError expected)
    {
        var interval = new Interval(start, end, startBound, endBound);

        interval.Validate().ShouldBe(expected);
        interval.IsValid.ShouldBe(expected == IntervalError.None);
    }

    [Fact]
    public void A_malformed_interval_is_rejected_with_its_reason()
    {
        var invalid = new Interval(5, 1);

        var exception = Should.Throw<InvalidIntervalException>(invalid.EnsureValid);

        exception.Error.ShouldBe(IntervalError.Inverted);
        exception.Interval.ShouldBe(invalid);
        exception.ParamName.ShouldBe("interval");
    }

    [Theory]
    [InlineData(Bound.Closed, Bound.Closed, 1, true)]
    [InlineData(Bound.Closed, Bound.Closed, 5, true)]
    [InlineData(Bound.Closed, Bound.Open, 5, false)]
    [InlineData(Bound.Open, Bound.Closed, 1, false)]
    [InlineData(Bound.Closed, Bound.Closed, 3, true)]
    [InlineData(Bound.Closed, Bound.Closed, 0.5, false)]
    [InlineData(Bound.Closed, Bound.Closed, 5.5, false)]
    public void Contains_honours_the_inclusivity_of_each_endpoint(
        Bound startBound,
        Bound endBound,
        double probe,
        bool expected)
    {
        var interval = new Interval(1, 5, startBound, endBound);

        interval.Contains(probe).ShouldBe(expected);
    }

    [Fact]
    public void Closed_intervals_sharing_an_endpoint_overlap_at_it()
    {
        new Interval(1, 5).Overlaps(new Interval(5, 9)).ShouldBeTrue();
    }

    [Fact]
    public void An_open_endpoint_keeps_the_intervals_apart_at_the_shared_point()
    {
        new Interval(1, 5, Bound.Closed, Bound.Open).Overlaps(new Interval(5, 9)).ShouldBeFalse();
    }

    [Fact]
    public void Disjoint_intervals_neither_overlap_nor_touch()
    {
        var left = new Interval(1, 5);

        left.Overlaps(new Interval(7, 9)).ShouldBeFalse();
        left.Touches(new Interval(7, 9)).ShouldBeFalse();
    }

    [Fact]
    public void Intervals_that_meet_at_one_closed_endpoint_touch_without_overlapping()
    {
        var left = new Interval(1, 5, Bound.Closed, Bound.Open);
        var right = new Interval(5, 9);

        left.Touches(right).ShouldBeTrue();
        left.Overlaps(right).ShouldBeFalse();
    }

    [Fact]
    public void Two_open_endpoints_at_the_same_point_do_not_touch()
    {
        var left = new Interval(1, 5, Bound.Closed, Bound.Open);
        var right = new Interval(5, 9, Bound.Open, Bound.Closed);

        left.Touches(right).ShouldBeFalse();
    }

    [Fact]
    public void Overlapping_intervals_do_not_touch()
    {
        new Interval(1, 5).Touches(new Interval(5, 9)).ShouldBeFalse();
    }

    [Fact]
    public void A_point_interval_does_not_overlap_the_range_that_leaves_that_point_out()
    {
        var point = new Interval(0, 0);
        var beyond = new Interval(0, 7, Bound.Open, Bound.Closed);

        point.Overlaps(beyond).ShouldBeFalse();
        point.Touches(beyond).ShouldBeTrue();
    }

    [Fact]
    public void Meets_is_true_for_overlapping_and_touching_intervals()
    {
        var half = new Interval(1, 5, Bound.Closed, Bound.Open);

        half.Meets(new Interval(3, 9)).ShouldBeTrue();
        half.Meets(new Interval(5, 9)).ShouldBeTrue();
        half.Meets(new Interval(7, 9)).ShouldBeFalse();
    }

    [Fact]
    public void Meets_is_false_when_two_open_endpoints_leave_a_gap()
    {
        var left = new Interval(1, 5, Bound.Closed, Bound.Open);

        left.Meets(new Interval(5, 9, Bound.Open, Bound.Closed)).ShouldBeFalse();
    }

    [Fact]
    public void Touches_is_symmetric_at_a_shared_endpoint()
    {
        var half = new Interval(1, 5, Bound.Closed, Bound.Open);
        var closed = new Interval(5, 9);

        half.Touches(closed).ShouldBeTrue();
        closed.Touches(half).ShouldBeTrue();
    }

    [Fact]
    public void Intervals_sharing_a_start_meet_at_the_stricter_bound()
    {
        var shared = Interval.Intersection(
            new Interval(0, 5),
            new Interval(0, 5, Bound.Closed, Bound.Open));

        shared.ShouldBe(new Interval(0, 5, Bound.Closed, Bound.Open));
    }

    [Fact]
    public void Merge_prefers_the_closed_bound_when_endpoints_coincide()
    {
        var merged = Interval.Merge(
            new Interval(1, 5, Bound.Closed, Bound.Open),
            new Interval(1, 9, Bound.Open, Bound.Closed));

        merged.ShouldBe(new Interval(1, 9));
    }

    [Fact]
    public void Removing_an_inner_range_leaves_a_hole()
    {
        var remaining = new Interval(1, 10).Subtract(new Interval(3, 5));

        var expected = new[]
        {
            new Interval(1, 3, Bound.Closed, Bound.Open),
            new Interval(5, 10, Bound.Open, Bound.Closed),
        };
        remaining.ShouldBe(expected);
    }

    [Fact]
    public void Removing_an_open_range_inside_a_closed_one_leaves_its_endpoints()
    {
        var remaining = new Interval(1, 5).Subtract(new Interval(1, 5, Bound.Open, Bound.Open));

        var expected = new[] { new Interval(1, 1), new Interval(5, 5) };
        remaining.ShouldBe(expected);
    }

    [Fact]
    public void Removing_a_closed_range_from_a_half_open_one_removes_everything()
    {
        var remaining = new Interval(1, 5, Bound.Closed, Bound.Open).Subtract(new Interval(1, 5));

        remaining.ShouldBeEmpty();
    }

    [Fact]
    public void A_disjoint_cut_leaves_the_interval_untouched()
    {
        var interval = new Interval(1, 5);

        interval.Subtract(new Interval(7, 9)).ShouldBe(new[] { interval });
    }

    [Fact]
    public void Intersection_returns_the_shared_part()
    {
        var shared = Interval.Intersection(new Interval(1, 5), new Interval(3, 9));

        shared.ShouldBe(new Interval(3, 5));
    }

    [Fact]
    public void Intersection_of_disjoint_intervals_is_rejected()
    {
        Should.Throw<InvalidOperationException>(
            () => Interval.Intersection(new Interval(1, 2), new Interval(3, 4)));
    }

    [Fact]
    public void TryIntersection_reports_whether_the_intervals_meet()
    {
        Interval.TryIntersection(new Interval(1, 2), new Interval(3, 4), out _).ShouldBeFalse();

        Interval.TryIntersection(new Interval(1, 5), new Interval(5, 9), out var shared).ShouldBeTrue();
        shared.ShouldBe(new Interval(5, 5));
    }

    [Fact]
    public void Intervals_render_in_standard_notation()
    {
        new Interval(1, 3, Bound.Closed, Bound.Open).ToString().ShouldBe("[1,3)");
        new Interval(1, 9, Bound.Open, Bound.Closed).ToString().ShouldBe("(1,9]");
        new Interval(1, 5).ToString().ShouldBe("[1,5]");
    }

    [Fact]
    public void Rendering_does_not_follow_the_current_culture()
    {
        using var culture = new CultureScope("de-DE");

        new Interval(1.5, 2.25).ToString().ShouldBe("[1.5,2.25]");
    }

    [Fact]
    public void Bounds_round_trip_through_their_wire_names()
    {
        Bound.Closed.ToWireName().ShouldBe("closed");
        Bound.Open.ToWireName().ShouldBe("open");

        BoundNames.TryParse("open", out var parsed).ShouldBeTrue();
        parsed.ShouldBe(Bound.Open);
        BoundNames.TryParse("half-open", out _).ShouldBeFalse();
    }
}
