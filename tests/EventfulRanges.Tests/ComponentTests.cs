using CsCheck;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// Components answer "how many separate regions does this cover make?" — which
/// is what a reader of a drawing asks, and what a spatial index needs to know.
/// </summary>
public sealed class ComponentTests
{
    [Fact]
    public void Boxes_that_do_not_reach_each_other_are_separate_regions()
    {
        Boxes.Components([Square(0, 2), Square(3, 5)]).Count.ShouldBe(2);
    }

    [Fact]
    public void Overlapping_boxes_are_one_region()
    {
        var components = Boxes.Components([Square(0, 2), Square(1, 3)]);

        components.ShouldHaveSingleItem().Count.ShouldBe(2);
    }

    [Fact]
    public void Boxes_that_share_a_face_are_one_region()
    {
        var left = new Box([0, 0], [2, 4]);
        var right = new Box([2, 0], [4, 3]);

        Boxes.Components([left, right]).Count.ShouldBe(1);
    }

    [Fact]
    public void Boxes_that_share_only_a_corner_are_separate_regions()
    {
        var first = new Box([0, 0], [2, 2]);
        var second = new Box([2, 2], [4, 4]);

        Boxes.Components([first, second]).Count.ShouldBe(2);
    }

    [Fact]
    public void A_chain_of_touching_boxes_is_one_region()
    {
        var components = Boxes.Components([Span(0, 2), Span(2, 4), Span(4, 6)]);

        components.ShouldHaveSingleItem().Count.ShouldBe(3);
    }

    [Fact]
    public void Diagonal_squares_touch_only_at_a_corner_and_stay_separate()
    {
        Boxes.Components([Square(0, 2), Square(2, 4), Square(4, 6)]).Count.ShouldBe(3);
    }

    [Fact]
    public void A_cover_without_boxes_has_no_regions()
    {
        Boxes.Components([]).ShouldBeEmpty();
    }

    [Fact]
    public void The_components_partition_the_normalized_cover()
    {
        GenCover.Sample(boxes =>
        {
            var components = Boxes.Components(boxes);

            components.Sum(component => component.Count).ShouldBe(Boxes.Normalize(boxes).Count);
            components.ShouldAllBe(component => component.Count > 0);
        });
    }

    private static Box Square(double start, double end) => new([start, start], [end, end]);

    private static Box Span(double start, double end) => new([start], [end]);

    private static readonly Gen<Box> GenSquare =
        from left in Gen.Int[0, 4]
        from right in Gen.Int[0, 4]
        from bottom in Gen.Int[0, 4]
        from top in Gen.Int[0, 4]
        let minX = Math.Min(left, right)
        let maxX = Math.Max(left, right)
        let minY = Math.Min(bottom, top)
        let maxY = Math.Max(bottom, top)
        select new Box([minX, minY], [maxX, maxY]);

    private static readonly Gen<IReadOnlyList<Box>> GenCover =
        GenSquare.List[0, 6].Select(boxes => (IReadOnlyList<Box>)boxes);
}
