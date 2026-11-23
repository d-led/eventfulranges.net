using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// A retraction cancels one specific edit rather than removing a region, so the
/// log keeps the history and the projection drops both the retraction and what it
/// names.
/// </summary>
public sealed class BoxStrategyTests
{
    [Fact]
    public void A_retraction_drops_itself_and_the_operation_it_names()
    {
        var first = BoxOp.Add([0], [10]) with { Id = "first" };
        var second = BoxOp.Add([20], [30]) with { Id = "second" };
        var undo = BoxOp.Retract("first", new Box([0], [10])) with { Id = "undo" };

        BoxStrategies.Effective([first, second, undo]).ShouldBe([second]);
    }

    [Fact]
    public void A_log_without_retractions_is_left_as_it_is()
    {
        var ops = new[] { BoxOp.Add([0], [10]), BoxOp.Remove([2], [3]) };

        BoxStrategies.Effective(ops).ShouldBe(ops);
    }

    [Fact]
    public void A_retraction_naming_nothing_known_still_disappears()
    {
        var kept = BoxOp.Add([0], [10]) with { Id = "kept" };
        var undo = BoxOp.Retract("unknown", new Box([0], [10])) with { Id = "undo" };

        BoxStrategies.Effective([kept, undo]).ShouldBe([kept]);
    }

    [Fact]
    public void Retraction_is_single_level_so_undoing_the_undo_changes_nothing()
    {
        var edit = BoxOp.Add([0], [10]) with { Id = "edit" };
        var undo = BoxOp.Retract("edit", new Box([0], [10])) with { Id = "undo" };
        var redo = BoxOp.Retract("undo", new Box([0], [10])) with { Id = "redo" };

        BoxStrategies.Effective([edit, undo, redo]).ShouldBeEmpty();
    }

    [Fact]
    public void A_range_cannot_honour_the_n_imensional_strategy()
    {
        var thrown = Should.Throw<NotSupportedException>(
            () => Strategies.Materialize(StrategyKind.AdditiveWinsLww, []));

        thrown.Message.ShouldContain("n-dimensional");
    }

    [Fact]
    public void The_n_imensional_strategy_has_its_wire_name()
    {
        StrategyKind.AdditiveWinsLww.ToWireName().ShouldBe("additive-wins-lww");
        StrategyNames.TryParse("additive-wins-lww", out var parsed).ShouldBeTrue();
        parsed.ShouldBe(StrategyKind.AdditiveWinsLww);
    }
}
