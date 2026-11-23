using CsCheck;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// A tree answers the same question a scan answers, only faster: which indexed boxes
/// share a point with the query. The oracle is the scan.
/// </summary>
public sealed class RTreeTests
{
    [Fact]
    public void An_empty_tree_matches_nothing()
    {
        var tree = RTree.Build([]);

        tree.Len.ShouldBe(0);
        tree.Dimensions.ShouldBe(0);
        tree.Search(new Box([0], [10])).ShouldBeEmpty();
    }

    [Fact]
    public void Boxes_that_hold_no_point_are_not_indexed()
    {
        var tree = RTree.Build([new Box([1], [1], [Bound.Open], [Bound.Open]), new Box([0], [1])]);

        tree.Len.ShouldBe(1);
    }

    [Fact]
    public void A_search_returns_the_boxes_the_query_meets_in_canonical_order()
    {
        var first = new Box([0], [2]);
        var second = new Box([5], [7]);
        var tree = RTree.Build([second, first]);

        tree.Search(new Box([1], [6])).ShouldBe([first, second]);
        tree.Search(new Box([10], [20])).ShouldBeEmpty();
    }

    [Fact]
    public void A_query_of_another_dimensionality_matches_nothing()
    {
        var tree = RTree.Build([new Box([0, 0], [1, 1])]);

        tree.Search(new Box([0], [1])).ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_query_matches_nothing()
    {
        var tree = RTree.Build([new Box([0], [10])]);

        tree.Search(new Box([5], [5], [Bound.Open], [Bound.Open])).ShouldBeEmpty();
    }

    [Fact]
    public void A_box_that_is_not_well_formed_is_rejected_at_build()
    {
        Should.Throw<InvalidBoxException>(() => RTree.Build([new Box([double.NaN], [1])]));
    }

    [Fact]
    public void An_inverted_box_holds_no_point_and_is_dropped()
    {
        RTree.Build([new Box([2], [1])]).Len.ShouldBe(0);
    }

    [Fact]
    public void Boxes_of_two_dimensionalities_cannot_share_a_tree()
    {
        Should.Throw<ArgumentException>(() => RTree.Build([new Box([0], [1]), new Box([0, 0], [1, 1])]));
    }

    [Fact]
    public void A_tree_deep_enough_to_split_answers_like_a_scan()
    {
        var boxes = Enumerable.Range(0, 40)
            .Select(index => new Box([index * 2], [(index * 2) + 1]))
            .ToList();
        var tree = RTree.Build(boxes);

        tree.Len.ShouldBe(40);
        tree.Search(new Box([10], [20])).ShouldBe(Scan(boxes, new Box([10], [20])));
        tree.Search(new Box([79], [100])).ShouldBe(Scan(boxes, new Box([79], [100])));
    }

    [Fact]
    public void Traversal_order_carries_the_same_matches_as_the_sorted_search()
    {
        var boxes = Squares(20);
        var tree = RTree.Build(boxes);
        var query = new Box([2, 2], [6, 6]);

        tree.SearchInTraversalOrder(query).Order().ShouldBe(tree.Search(query));
    }

    [Fact]
    public void A_search_answers_exactly_what_a_scan_answers()
    {
        GenQuery.Sample(query =>
        {
            var boxes = Squares(24);
            var tree = RTree.Build(boxes);

            tree.Search(query).ShouldBe(Scan(boxes, query));
        });
    }

    private static List<Box> Scan(IReadOnlyList<Box> boxes, Box query) =>
        boxes.Where(box => box.Overlaps(query)).Order().ToList();

    private static List<Box> Squares(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new Box([index % 5, index / 5], [(index % 5) + 1.5, (index / 5) + 1.5]))
            .ToList();

    private static readonly Gen<Box> GenQuery =
        from left in Gen.Int[0, 6]
        from bottom in Gen.Int[0, 6]
        from width in Gen.Int[1, 4]
        from height in Gen.Int[1, 4]
        select new Box([left, bottom], [left + width, bottom + height]);
}
