using System.Collections.Immutable;
using System.Text;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The box algebra answers the same questions the interval algebra answers in
/// one dimension: what a cover is, what the union covers, and what is left when
/// one cover is taken out of another.
/// </summary>
public sealed class BoxesTests
{
    [Fact]
    public void Normalizing_drops_boxes_that_hold_nothing_and_sorts_the_rest()
    {
        var cover = Boxes.Normalize([
            new Box([5, 5], [6, 6]),
            new Box([0, 0], [1, 1]),
            new Box([2, 2], [2, 2], [Bound.Open, Bound.Open], [Bound.Open, Bound.Open]),
        ]);

        cover.ShouldBe([new Box([0, 0], [1, 1]), new Box([5, 5], [6, 6])]);
    }

    [Fact]
    public void A_box_inside_another_is_absorbed_with_its_metadata()
    {
        var covering = new Box([0, 0], [10, 10]).WithMeta(Raw("""{"color":"red"}"""));
        var inner = new Box([2, 2], [3, 3]).WithMeta(Raw("""{"author":"alice"}"""));

        var cover = Boxes.Normalize([covering, inner]);

        cover.ShouldHaveSingleItem().ShouldBe(covering);
        Text(cover[0].Meta).ShouldBe("""{"author":"alice","color":"red"}""");
    }

    [Fact]
    public void A_cover_that_contains_another_absorbs_it()
    {
        var inner = new Box([2, 2], [3, 3]).WithMeta(Raw("""{"author":"alice"}"""));

        var cover = Boxes.Normalize([inner, new Box([0, 0], [10, 10])]);

        cover.ShouldHaveSingleItem().ShouldBe(new Box([0, 0], [10, 10]));
        Text(cover[0].Meta).ShouldBe("""{"author":"alice"}""");
    }

    [Fact]
    public void Union_keeps_every_covered_point()
    {
        var cover = Boxes.Union([new Box([0], [5])], [new Box([5], [10])]);

        cover.ShouldBe([new Box([0], [5]), new Box([5], [10])]);
        Boxes.Contains(cover, [5]).ShouldBeTrue();
        Boxes.Contains(cover, [10]).ShouldBeFalse();
        Boxes.Contains(cover, [4.5]).ShouldBeTrue();
    }

    [Fact]
    public void Difference_carves_the_second_cover_out_of_the_first()
    {
        var rest = Boxes.Difference([new Box([0], [10])], [new Box([3], [5])]);

        rest.ShouldBe([new Box([0], [3]), new Box([5], [10])]);
    }

    [Fact]
    public void Difference_of_a_cover_by_a_cover_that_covers_it_leaves_nothing()
    {
        Boxes.Difference([new Box([4], [6])], [new Box([0], [10])]).ShouldBeEmpty();
    }

    [Fact]
    public void Difference_of_disjoint_covers_is_the_first_cover()
    {
        var first = new Box([0], [1]);

        Boxes.Difference([first], [new Box([2], [3])]).ShouldBe([first]);
    }

    [Fact]
    public void Difference_keeps_the_endpoints_the_second_cover_excludes()
    {
        var rest = Boxes.Difference(
            [new Box([0], [10], [Bound.Closed], [Bound.Closed])],
            [new Box([0], [10], [Bound.Open], [Bound.Open])]);

        rest.ShouldBe([
            new Box([0], [0], [Bound.Closed], [Bound.Closed]),
            new Box([10], [10], [Bound.Closed], [Bound.Closed]),
        ]);
    }

    [Fact]
    public void Difference_in_two_dimensions_leaves_the_remaining_rectangles()
    {
        var rest = Boxes.Difference(
            [new Box([0, 0], [10, 10])],
            [new Box([3, 3], [7, 7])]);

        rest.ShouldBe([
            new Box([0, 0], [3, 10]),
            new Box([3, 0], [7, 3]),
            new Box([3, 7], [7, 10]),
            new Box([7, 0], [10, 10]),
        ]);
    }

    [Fact]
    public void Two_covers_are_equal_when_they_cover_the_same_points()
    {
        Boxes.Equal([new Box([0], [5]), new Box([5], [10])], [new Box([5], [10]), new Box([0], [5])])
            .ShouldBeTrue();
        Boxes.Equal([new Box([0], [5])], [new Box([0], [6])]).ShouldBeFalse();
    }

    [Fact]
    public void Inserting_into_a_normalized_cover_keeps_it_normalized()
    {
        var cover = Boxes.InsertNormalized([new Box([0], [1]), new Box([5], [6])], new Box([2], [3]));

        cover.ShouldBe([new Box([0], [1]), new Box([2], [3]), new Box([5], [6])]);

        var absorbed = Boxes.InsertNormalized([new Box([0], [1]), new Box([2], [3])], new Box([0], [3]));
        absorbed.ShouldBe([new Box([0], [3])]);

        var boxedIn = Boxes.InsertNormalized([new Box([0], [10])], new Box([2], [3]));
        boxedIn.ShouldBe([new Box([0], [10])]);
    }

    [Fact]
    public void A_cover_answers_whether_it_covers_a_point_or_touches_a_box()
    {
        IReadOnlyList<Box> cover = [new Box([0, 0], [10, 10])];

        Boxes.Contains(cover, [5, 5]).ShouldBeTrue();
        Boxes.Contains(cover, [10, 0]).ShouldBeFalse();
        Boxes.Overlaps(cover, new Box([9, 9], [20, 20])).ShouldBeTrue();
        Boxes.Overlaps(cover, new Box([10, 0], [20, 10])).ShouldBeFalse();
    }

    private static ImmutableArray<byte> Raw(string json) =>
        ImmutableArray.Create(Encoding.UTF8.GetBytes(json));

    private static string Text(ImmutableArray<byte> raw) => Encoding.UTF8.GetString(raw.AsSpan());
}
