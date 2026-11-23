using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// A box is an axis-aligned rectangle whose faces are included or excluded one
/// by one. Built from two corners it is half-open — every lower face closed,
/// every upper face open — and explicit bounds buy the closed forms.
/// </summary>
public sealed class BoxTests
{
    [Fact]
    public void A_box_built_from_two_corners_includes_its_lower_faces_only()
    {
        var box = new Box([0, 0], [10, 5]);

        box.LowestBound(0).ShouldBe(Bound.Closed);
        box.LowestBound(1).ShouldBe(Bound.Closed);
        box.HighestBound(0).ShouldBe(Bound.Open);
        box.HighestBound(1).ShouldBe(Bound.Open);
        box.Contains([10, 5]).ShouldBeFalse();
        box.Contains([9.9, 4.9]).ShouldBeTrue();
    }

    [Fact]
    public void Explicit_bounds_include_or_exclude_a_face()
    {
        var closed = new Box([0], [10], [Bound.Closed], [Bound.Closed]);

        closed.HighestBound(0).ShouldBe(Bound.Closed);
        closed.Contains([10]).ShouldBeTrue();
        closed.Contains([10.5]).ShouldBeFalse();
    }

    [Fact]
    public void A_box_whose_corners_meet_on_an_open_face_holds_no_point()
    {
        new Box([1], [1], [Bound.Open], [Bound.Open]).IsEmpty.ShouldBeTrue();
        new Box([1], [1], [Bound.Closed], [Bound.Closed]).IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void A_box_that_stops_before_it_starts_holds_no_point()
    {
        new Box([2], [1]).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Validation_names_the_reason_a_box_is_malformed()
    {
        new Box([0, 0], [1]).Validate().ShouldBe(BoxError.Dimensions);
        new Box([], []).Validate().ShouldBe(BoxError.NoDimensions);
        new Box([0], [1], [Bound.Closed, Bound.Open], null).Validate().ShouldBe(BoxError.Bounds);
        new Box([double.NaN], [1]).Validate().ShouldBe(BoxError.NotANumber);
        new Box([0], [double.PositiveInfinity]).Validate().ShouldBe(BoxError.NotFinite);
        new Box([2], [1]).Validate().ShouldBe(BoxError.Inverted);
        new Box([0, 0], [1, 1]).Validate().ShouldBe(BoxError.None);
    }

    [Fact]
    public void An_empty_box_is_well_formed_because_normalizing_drops_it()
    {
        new Box([1], [1], [Bound.Open], [Bound.Open]).Validate().ShouldBe(BoxError.None);
    }

    [Fact]
    public void A_malformed_box_is_rejected_where_a_well_formed_one_is_required()
    {
        var inverted = new Box([2], [1]);

        var thrown = Should.Throw<InvalidBoxException>(inverted.EnsureValid);
        thrown.Error.ShouldBe(BoxError.Inverted);
        thrown.Box.ShouldBeSameAs(inverted);
    }

    [Fact]
    public void Two_boxes_share_a_point_when_the_shared_corner_belongs_to_both()
    {
        var right = new Box([1], [2], [Bound.Closed], [Bound.Closed]);

        new Box([0], [1], [Bound.Closed], [Bound.Closed]).Overlaps(right).ShouldBeTrue();
        new Box([0], [1]).Overlaps(right).ShouldBeFalse();
    }

    [Fact]
    public void A_box_shares_a_point_with_itself_only_when_it_holds_one()
    {
        var empty = new Box([1], [1], [Bound.Open], [Bound.Open]);

        empty.Overlaps(empty).ShouldBeFalse();
        new Box([0], [1]).Overlaps(new Box([0], [1])).ShouldBeTrue();
    }

    [Fact]
    public void A_box_contains_another_only_when_every_point_of_it_is_inside()
    {
        var outer = new Box([0], [10]);

        outer.Contains(new Box([0], [10], [Bound.Open], [Bound.Open])).ShouldBeTrue();
        outer.Contains(new Box([-1], [5])).ShouldBeFalse();
    }

    [Fact]
    public void An_open_face_does_not_cover_the_closed_one()
    {
        var open = new Box([0], [10], [Bound.Open], [Bound.Open]);

        open.Contains(new Box([0], [10])).ShouldBeFalse();
        open.Contains(new Box([0], [10], [Bound.Open], [Bound.Open])).ShouldBeTrue();
    }

    [Fact]
    public void Two_boxes_with_the_same_corners_and_faces_are_the_same_box()
    {
        var one = new Box([0, 1], [10, 2], [Bound.Closed, Bound.Closed], [Bound.Open, Bound.Closed]);
        var other = new Box([0, 1], [10, 2], [Bound.Closed, Bound.Closed], [Bound.Open, Bound.Closed]);

        one.ShouldBe(other);
        one.GetHashCode().ShouldBe(other.GetHashCode());
        one.ShouldNotBe(new Box([0, 1], [10, 2]));
    }

    [Fact]
    public void Canonical_order_runs_from_the_lower_corner_up()
    {
        var first = new Box([0], [1]);
        var second = new Box([0], [2]);
        var third = new Box([1], [2]);

        first.CompareTo(second).ShouldBeLessThan(0);
        second.CompareTo(third).ShouldBeLessThan(0);
        third.CompareTo(first).ShouldBeGreaterThan(0);
        first.CompareTo(new Box([0], [1])).ShouldBe(0);
    }

    [Fact]
    public void Order_falls_back_to_the_faces_when_the_corners_agree()
    {
        var closed = new Box([0], [1], [Bound.Closed], [Bound.Closed]);
        var excluded = new Box([0], [1], [Bound.Open], [Bound.Open]);

        closed.CompareTo(excluded).ShouldBeLessThan(0);
        excluded.CompareTo(closed).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void The_comparison_operators_state_the_same_order()
    {
        var low = new Box([0], [1]);
        var high = new Box([1], [2]);
        var twin = new Box([0], [1]);

        (low < high).ShouldBeTrue();
        (low <= twin).ShouldBeTrue();
        (high > low).ShouldBeTrue();
        (twin >= low).ShouldBeTrue();
        (low == twin).ShouldBeTrue();
        (low != high).ShouldBeTrue();
        (low < null).ShouldBeFalse();
        (low > (Box?)null).ShouldBeTrue();
        ((Box?)null == null).ShouldBeTrue();
    }

    [Fact]
    public void A_box_renders_each_axis_between_its_faces()
    {
        new Box([0, 2], [10, 5]).ToString().ShouldBe("[0 10) x [2 5)");
        new Box([0, 2], [10, 5], [Bound.Open, Bound.Open], [Bound.Closed, Bound.Closed])
            .ToString()
            .ShouldBe("(0 10] x (2 5]");
    }

    [Fact]
    public void Metadata_travels_with_the_box()
    {
        var box = new Box([0], [1]).WithMeta(Meta.Union(default, default));

        box.Meta.IsDefaultOrEmpty.ShouldBeTrue();
        box.WithMeta(default).ShouldBe(box);
    }
}
