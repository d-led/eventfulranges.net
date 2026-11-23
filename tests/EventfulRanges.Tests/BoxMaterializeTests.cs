using System.Collections.Immutable;
using System.Text;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// Materializing a box log is the n-dimensional counterpart of materializing a
/// range log: the same four policies, plus one that resolves a point's metadata by
/// the latest addition covering it, so the newest stroke paints over the older.
/// </summary>
public sealed class BoxMaterializeTests
{
    private static readonly Box Small = new([0], [5]);
    private static readonly Box Large = new([3], [10]);

    [Fact]
    public void Last_write_wins_keeps_every_region_for_the_claim_it_wins()
    {
        var cover = BoxStrategies.Materialize(
            StrategyKind.Lww,
            [Stamped(BoxOpKind.Add, Small, 1), Stamped(BoxOpKind.Add, Large, 2)]);

        Boxes.Contains(cover, [1]).ShouldBeTrue();
        Boxes.Contains(cover, [8]).ShouldBeTrue();
        Boxes.Contains(cover, [-1]).ShouldBeFalse();
    }

    [Fact]
    public void A_later_removal_removes_the_region_it_claims()
    {
        var cover = BoxStrategies.Materialize(
            StrategyKind.Lww,
            [Stamped(BoxOpKind.Add, Small, 1), Stamped(BoxOpKind.Remove, Large, 2)]);

        Boxes.Contains(cover, [4]).ShouldBeFalse();
        Boxes.Contains(cover, [1]).ShouldBeTrue();
    }

    [Fact]
    public void First_write_wins_prefers_the_earlier_operation()
    {
        var cover = BoxStrategies.Materialize(
            StrategyKind.Fww,
            [
                Stamped(BoxOpKind.Add, Small, 1),
                Stamped(BoxOpKind.Remove, Large, 2),
            ]);

        Boxes.Contains(cover, [4]).ShouldBeTrue();
        Boxes.Contains(cover, [8]).ShouldBeFalse();
    }

    [Fact]
    public void Additive_wins_subtracts_every_removal_whatever_its_stamp()
    {
        var cover = BoxStrategies.Materialize(
            StrategyKind.AdditiveWins,
            [
                Stamped(BoxOpKind.Add, Small, 5),
                Stamped(BoxOpKind.Remove, Large, 1),
            ]);

        Boxes.Contains(cover, [1]).ShouldBeTrue();
        Boxes.Contains(cover, [4]).ShouldBeFalse();
        Boxes.Contains(cover, [8]).ShouldBeFalse();
    }

    [Fact]
    public void Grow_only_ignores_removals()
    {
        var cover = BoxStrategies.Materialize(
            StrategyKind.GrowOnly,
            [Stamped(BoxOpKind.Add, Small, 1), Stamped(BoxOpKind.Remove, Large, 2)]);

        Boxes.Contains(cover, [1]).ShouldBeTrue();
        Boxes.Contains(cover, [4]).ShouldBeTrue();
    }

    [Fact]
    public void The_latest_addition_paints_its_metadata_over_the_older_one()
    {
        var red = Stamped(BoxOpKind.Add, Small, 1, Raw("""{"color":"red"}"""));
        var blue = Stamped(BoxOpKind.Add, Large, 2, Raw("""{"color":"blue"}"""));

        var cover = BoxStrategies.Materialize(StrategyKind.AdditiveWinsLww, [red, blue]);

        Text(At(cover, [4]).Meta).ShouldBe("""{"color":"blue"}""");
        Text(At(cover, [1]).Meta).ShouldBe("""{"color":"red"}""");
    }

    [Fact]
    public void Additive_wins_with_the_latest_metadata_still_subtracts_removals()
    {
        var red = Stamped(BoxOpKind.Add, Small, 1, Raw("""{"color":"red"}"""));
        var removal = Stamped(BoxOpKind.Remove, new Box([1], [2]), 3);

        var cover = BoxStrategies.Materialize(StrategyKind.AdditiveWinsLww, [red, removal]);

        Boxes.Contains(cover, [1.5]).ShouldBeFalse();
        Boxes.Contains(cover, [4]).ShouldBeTrue();
    }

    [Fact]
    public void A_retracted_addition_changes_nothing()
    {
        var kept = Stamped(BoxOpKind.Add, Small, 1) with { Id = "kept" };
        var dropped = Stamped(BoxOpKind.Add, Large, 2) with { Id = "dropped" };
        var undo = BoxOp.Retract("dropped", Large) with { Id = "undo" };

        var cover = BoxStrategies.Materialize(StrategyKind.GrowOnly, [kept, dropped, undo]);

        Boxes.Equal(cover, [Small]).ShouldBeTrue();
    }

    [Fact]
    public void Segments_carry_the_winning_operation_per_region()
    {
        var segments = BoxStrategies.Segments(
            StrategyKind.Lww,
            [Stamped(BoxOpKind.Add, Small, 1), Stamped(BoxOpKind.Remove, Large, 2)]);

        segments.Count.ShouldBe(2);
        segments[0].Box.ShouldBe(new Box([0], [3]));
        segments[0].Winner.Kind.ShouldBe(BoxOpKind.Add);
        segments[1].Winner.Kind.ShouldBe(BoxOpKind.Remove);
        segments[1].Winner.Timestamp.ShouldBe(2);
    }

    [Fact]
    public void Combining_segment_sets_ranks_them_together()
    {
        var first = BoxStrategies.Segments(StrategyKind.Lww, [Stamped(BoxOpKind.Add, Small, 5)]);
        var second = BoxStrategies.Segments(StrategyKind.Lww, [Stamped(BoxOpKind.Remove, Large, 1)]);

        var combined = BoxStrategies.CombineSegments(StrategyKind.Lww, first, second);

        BoxStrategies.ToBoxes(combined).ShouldBe([Small]);
    }

    [Fact]
    public void A_malformed_strategy_is_reported_by_name()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => BoxStrategies.Materialize((StrategyKind)99, []));
    }

    private static BoxOp Stamped(BoxOpKind kind, Box box, long timestamp, ImmutableArray<byte> meta = default) =>
        new($"op-{timestamp}-{kind.ToWireName()}", kind, meta.IsDefaultOrEmpty ? box : box.WithMeta(meta), timestamp);

    private static Box At(IReadOnlyList<Box> cover, IReadOnlyList<double> point) =>
        cover.First(box => box.Contains(point));

    private static ImmutableArray<byte> Raw(string json) =>
        ImmutableArray.Create(Encoding.UTF8.GetBytes(json));

    private static string Text(ImmutableArray<byte> raw) => Encoding.UTF8.GetString(raw.AsSpan());
}
