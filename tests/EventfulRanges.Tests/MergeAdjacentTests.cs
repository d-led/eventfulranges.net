using System.Collections.Immutable;
using System.Text;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// Merging adjacent boxes turns a cover that touches edge to edge into one that
/// does not, without moving a single covered point. It is what a canonicalizer
/// seam is for: the shape of the cover is presentation, the covered set is not.
/// </summary>
public sealed class MergeAdjacentTests
{
    [Fact]
    public void Two_closed_boxes_that_touch_become_one()
    {
        var merged = Boxes.MergeAdjacent([ClosedSpan(0, 1), ClosedSpan(1, 2)]);

        merged.ShouldHaveSingleItem().ShouldBe(ClosedSpan(0, 2));
    }

    [Fact]
    public void The_order_the_boxes_arrive_in_does_not_matter()
    {
        var merged = Boxes.MergeAdjacent([ClosedSpan(1, 2), ClosedSpan(0, 1)]);

        merged.ShouldHaveSingleItem().ShouldBe(ClosedSpan(0, 2));
    }

    [Fact]
    public void A_pinhole_between_two_open_faces_keeps_them_apart()
    {
        var left = new Box([0], [1], [Bound.Closed], [Bound.Open]);
        var right = new Box([1], [2], [Bound.Open], [Bound.Closed]);

        Boxes.MergeAdjacent([left, right]).Count.ShouldBe(2);
    }

    [Fact]
    public void Boxes_with_the_same_span_but_incomparable_faces_stay_apart()
    {
        var leftOpen = new Box([0], [1], [Bound.Closed], [Bound.Open]);
        var rightOpen = new Box([0], [1], [Bound.Open], [Bound.Closed]);

        Boxes.MergeAdjacent([leftOpen, rightOpen]).Count.ShouldBe(2);
    }

    [Fact]
    public void A_nested_neighbour_is_absorbed_before_the_merge()
    {
        var merged = Boxes.MergeAdjacent([ClosedSpan(0, 2), ClosedSpan(2, 3), ClosedSpan(0, 1)]);

        merged.ShouldHaveSingleItem().ShouldBe(ClosedSpan(0, 3));
    }

    [Fact]
    public void A_chain_of_touching_boxes_collapses_in_repeated_passes()
    {
        var merged = Boxes.MergeAdjacent([ClosedSpan(0, 1), ClosedSpan(1, 2), ClosedSpan(2, 3)]);

        merged.ShouldHaveSingleItem().ShouldBe(ClosedSpan(0, 3));
    }

    [Fact]
    public void The_metadata_of_both_boxes_survives_the_merge()
    {
        var left = ClosedSpan(0, 1).WithMeta(Raw("""{"color":"red"}"""));
        var right = ClosedSpan(1, 2).WithMeta(Raw("""{"author":"alice"}"""));

        var merged = Boxes.MergeAdjacent([left, right]);

        Text(merged.ShouldHaveSingleItem().Meta).ShouldBe("""{"author":"alice","color":"red"}""");
    }

    [Fact]
    public void Two_dimensional_boxes_merge_along_the_axis_they_touch_on()
    {
        var merged = Boxes.MergeAdjacent([
            new Box([0, 0], [1, 1]),
            new Box([1, 0], [2, 1]),
        ]);

        merged.ShouldHaveSingleItem().ShouldBe(new Box([0, 0], [2, 1]));
    }

    [Fact]
    public void Boxes_that_differ_on_two_axes_do_not_merge()
    {
        var merged = Boxes.MergeAdjacent([
            new Box([0, 0], [1, 1]),
            new Box([1, 1], [2, 2]),
        ]);

        merged.Count.ShouldBe(2);
    }

    [Fact]
    public void A_chain_of_canonicalizers_runs_left_to_right_and_skips_the_missing_ones()
    {
        var canonicalizer = Canonicalizers.Chain(
            null,
            boxes => boxes,
            Boxes.MergeAdjacent,
            null);

        canonicalizer([ClosedSpan(0, 1), ClosedSpan(1, 2)]).ShouldHaveSingleItem();
    }

    private static Box ClosedSpan(double start, double end) =>
        new([start], [end], [Bound.Closed], [Bound.Closed]);

    private static ImmutableArray<byte> Raw(string json) =>
        ImmutableArray.Create(Encoding.UTF8.GetBytes(json));

    private static string Text(ImmutableArray<byte> raw) => Encoding.UTF8.GetString(raw.AsSpan());
}
