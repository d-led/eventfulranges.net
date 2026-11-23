using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// A line is a query shape: it asks which boxes a straight segment meets, in
/// order, and where along itself they sit. It follows the box's half-open
/// convention, so a segment that grazes a face belongs to the neighbouring box.
/// </summary>
public sealed class LineTests
{
    [Fact]
    public void A_line_with_equal_endpoints_is_a_valid_point_query()
    {
        new Line([0, 0], [1, 1]).Validate().ShouldBe(LineError.None);
        new Line([2, 2], [2, 2]).Validate().ShouldBe(LineError.None);
    }

    [Fact]
    public void Validation_names_the_reason_a_line_is_malformed()
    {
        new Line([0], [1, 1]).Validate().ShouldBe(LineError.Dimensions);
        new Line([], []).Validate().ShouldBe(LineError.NoDimensions);
        new Line([double.NaN], [1]).Validate().ShouldBe(LineError.NotANumber);
        new Line([0], [double.PositiveInfinity]).Validate().ShouldBe(LineError.NotFinite);
    }

    [Fact]
    public void A_malformed_line_is_rejected_where_a_well_formed_one_is_required()
    {
        var malformed = new Line([0], [1, 1]);

        var thrown = Should.Throw<InvalidLineException>(malformed.EnsureValid);
        thrown.Error.ShouldBe(LineError.Dimensions);
    }

    [Fact]
    public void A_point_in_between_is_interpolated_per_axis()
    {
        var line = new Line([0, 10], [4, -10]);

        line.Point(0).ShouldBe([0, 10]);
        line.Point(1).ShouldBe([4, -10]);
        line.Point(0.5).ShouldBe([2, 0]);
    }

    [Fact]
    public void The_diagonal_of_a_square_stays_inside_it_for_its_whole_run()
    {
        new Line([0, 0], [4, 4]).Crossing(Square(0, 4)).ShouldBe(new LineSpan(0, 1));
    }

    [Fact]
    public void The_diagonal_enters_and_leaves_an_inner_box_at_a_quarter_run()
    {
        new Line([0, 0], [4, 4]).Crossing(Square(1, 3)).ShouldBe(new LineSpan(0.25, 0.75));
    }

    [Fact]
    public void Walking_the_same_line_backwards_gives_the_same_crossing()
    {
        new Line([4, 4], [0, 0]).Crossing(Square(1, 3)).ShouldBe(new LineSpan(0.25, 0.75));
    }

    [Fact]
    public void An_axis_aligned_line_enters_immediately_and_leaves_halfway()
    {
        new Line([0, 0], [4, 0]).Crossing(Square(0, 2)).ShouldBe(new LineSpan(0, 0.5));
    }

    [Fact]
    public void A_line_through_the_middle_of_a_box_spans_the_whole_run()
    {
        new Line([2, 0], [2, 4]).Crossing(Square(0, 4)).ShouldBe(new LineSpan(0, 1));
    }

    [Fact]
    public void A_line_that_stays_away_crosses_nothing()
    {
        new Line([0, 0], [1, 1]).Crossing(new Box([10, 10], [20, 20])).ShouldBeNull();
    }

    [Fact]
    public void Running_along_an_upper_face_is_outside_the_box()
    {
        new Line([1, 0], [1, 1]).Crossing(new Box([0, 0], [1, 1])).ShouldBeNull();
    }

    [Fact]
    public void Touching_a_corner_is_a_single_point_and_not_a_crossing()
    {
        new Line([1, 1], [2, 2]).Crossing(new Box([0, 0], [1, 1])).ShouldBeNull();
    }

    [Fact]
    public void A_stationary_point_inside_a_box_crosses_it_for_the_whole_run()
    {
        new Line([2, 2], [2, 2]).Crossing(Square(0, 4)).ShouldBe(new LineSpan(0, 1));
    }

    [Fact]
    public void A_stationary_point_on_an_upper_face_is_outside()
    {
        new Line([4, 4], [4, 4]).Crossing(Square(0, 4)).ShouldBeNull();
    }

    [Fact]
    public void A_stationary_point_on_a_closed_upper_face_is_inside()
    {
        var closed = new Box([0, 0], [2, 2], [Bound.Closed, Bound.Closed], [Bound.Closed, Bound.Closed]);
        var open = new Box([0, 0], [2, 2], [Bound.Open, Bound.Open], [Bound.Open, Bound.Open]);

        new Line([2, 2], [2, 2]).Crossing(closed).ShouldBe(new LineSpan(0, 1));
        new Line([2, 2], [2, 2]).Crossing(open).ShouldBeNull();
    }

    [Fact]
    public void A_line_of_another_dimensionality_crosses_nothing()
    {
        new Line([0], [1]).Crossing(Square(0, 4)).ShouldBeNull();
    }

    [Fact]
    public void The_boxes_a_line_crosses_come_back_in_canonical_order()
    {
        var crossed = Boxes.Crossed([Square(10, 20), Square(0, 2)], new Line([0, 0], [1, 1]));

        crossed.ShouldBe([Square(0, 2)]);
    }

    [Fact]
    public void A_box_the_line_only_grazes_is_not_crossed()
    {
        Boxes.Crossed([new Box([0, 0], [1, 1])], new Line([1, 1], [2, 2])).ShouldBeEmpty();
    }

    [Fact]
    public void A_traversal_alternates_between_boxes_and_the_gaps_between_them()
    {
        var segments = Boxes.Traverse(
            [new Box([0], [10]), new Box([20], [30])],
            new Line([0], [40]));

        segments.ShouldBe([
            new LineSegment(0, 0.25, covered: true, [new Box([0], [10])]),
            new LineSegment(0.25, 0.5, covered: false),
            new LineSegment(0.5, 0.75, covered: true, [new Box([20], [30])]),
            new LineSegment(0.75, 1, covered: false),
        ]);
    }

    [Fact]
    public void An_overlapping_stretch_lists_both_boxes()
    {
        var first = new Box([0], [10]);
        var second = new Box([5], [15]);

        var segments = Boxes.Traverse([first, second], new Line([0], [20]));

        segments.ShouldBe([
            new LineSegment(0, 0.25, covered: true, [first]),
            new LineSegment(0.25, 0.5, covered: true, [first, second]),
            new LineSegment(0.5, 0.75, covered: true, [second]),
            new LineSegment(0.75, 1, covered: false),
        ]);
    }

    [Fact]
    public void Boxes_that_touch_leave_no_gap_between_them()
    {
        var first = new Box([0], [10]);
        var second = new Box([10], [20]);

        var segments = Boxes.Traverse([first, second], new Line([0], [20]));

        segments.ShouldBe([
            new LineSegment(0, 0.5, covered: true, [first]),
            new LineSegment(0.5, 1, covered: true, [second]),
        ]);
    }

    [Fact]
    public void A_cover_without_boxes_is_one_gap()
    {
        Boxes.Traverse([], new Line([0, 0], [1, 1]))
            .ShouldBe([new LineSegment(0, 1, covered: false)]);
    }

    [Fact]
    public void A_traversal_is_the_same_each_time()
    {
        var boxes = new[] { Square(0, 2), Square(1, 3) };
        var line = new Line([0, 0], [3, 3]);

        Boxes.Traverse(boxes, line).ShouldBe(Boxes.Traverse(boxes, line));
    }

    [Fact]
    public void A_span_knows_whether_it_has_length()
    {
        new LineSpan(0.25, 0.75).IsEmpty.ShouldBeFalse();
        new LineSpan(1, 1).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void A_segment_compares_and_renders_as_a_value()
    {
        var segment = new LineSegment(0, 0.5, covered: true, [new Box([0], [10])]);
        var same = new LineSegment(0, 0.5, covered: true, [new Box([0], [10])]);

        segment.Equals((object)same).ShouldBeTrue();
        segment.GetHashCode().ShouldBe(same.GetHashCode());
        segment.ToString().ShouldContain("covered by 1");
        new LineSegment(0, 1, covered: false).ToString().ShouldContain("empty");
    }

    [Fact]
    public void A_line_reports_validity_and_renders_its_endpoints()
    {
        var line = new Line([0, 10], [4, -10]);

        line.IsValid.ShouldBeTrue();
        line.ToString().ShouldBe("(0, 10) → (4, -10)");
        new Line([0], [1, 1]).IsValid.ShouldBeFalse();
    }

    private static Box Square(double start, double end) => new([start, start], [end, end]);
}
