using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The layered front is what a painter draws: whole regions in paint order, later
/// ones over earlier ones, with anything completely hidden culled.
/// </summary>
public sealed class LayerTests
{
    private static readonly Box Large = new([0], [10]);
    private static readonly Box Small = new([3], [4]);

    [Fact]
    public void A_stroke_inside_a_box_leaves_the_box_whole_and_paints_on_top()
    {
        var layers = BoxStrategies.Layers([Stamped(BoxOpKind.Add, Large, 1), Stamped(BoxOpKind.Add, Small, 2)]);

        layers.ShouldBe([new Layer(Large, BoxOpKind.Add), new Layer(Small, BoxOpKind.Add)]);
    }

    [Fact]
    public void A_box_hidden_by_a_later_layer_is_culled()
    {
        var layers = BoxStrategies.Layers([Stamped(BoxOpKind.Add, Small, 1), Stamped(BoxOpKind.Add, Large, 2)]);

        layers.ShouldBe([new Layer(Large, BoxOpKind.Add)]);
    }

    [Fact]
    public void An_erase_paints_the_background_and_a_later_box_repaints_over_it()
    {
        var layers = BoxStrategies.Layers([
            Stamped(BoxOpKind.Add, Large, 1),
            Stamped(BoxOpKind.Remove, new Box([2], [8]), 2),
            Stamped(BoxOpKind.Add, Small, 3),
        ]);

        layers.ShouldBe([
            new Layer(Large, BoxOpKind.Add),
            new Layer(new Box([2], [8]), BoxOpKind.Remove),
            new Layer(Small, BoxOpKind.Add),
        ]);
    }

    [Fact]
    public void A_retracted_painting_never_reaches_the_front()
    {
        var painting = Stamped(BoxOpKind.Add, Large, 1) with { Id = "painting" };
        var undo = BoxOp.Retract("painting", Large) with { Id = "undo" };

        BoxStrategies.Layers([painting, undo]).ShouldBeEmpty();
    }

    [Fact]
    public void A_log_without_operations_has_an_empty_front()
    {
        BoxStrategies.Layers([]).ShouldBeEmpty();
    }

    [Fact]
    public void The_front_runs_from_the_bottom_to_the_top()
    {
        var layers = BoxStrategies.Layers([
            Stamped(BoxOpKind.Add, Large, 1),
            Stamped(BoxOpKind.Add, new Box([5], [12]), 7),
        ]);

        layers[^1].ShouldBe(new Layer(new Box([5], [12]), BoxOpKind.Add));
    }

    [Fact]
    public async Task The_facade_exposes_the_layered_front()
    {
        using var set = await BoxSet.OpenLogAsync(
            new BoxMemoryLog(),
            StrategyKind.GrowOnly,
            new BoxEngineOptions { Clock = new LamportClock() });

        await set.AddAsync([0], [10]);
        await set.AddAsync([3], [4]);

        set.Layers.Count.ShouldBe(2);
        set.Boxes.ShouldHaveSingleItem().ShouldBe(new Box([0], [10]));
    }

    private static BoxOp Stamped(BoxOpKind kind, Box box, long timestamp) =>
        new(FormattableString.Invariant($"op-{timestamp}"), kind, box, timestamp);
}
