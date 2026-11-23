using System.Collections.Immutable;
using System.Text;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The facade is what a caller actually uses: add and remove regions, undo a single
/// edit, and ask what a point, a box or a line meets.
/// </summary>
public sealed class BoxSetTests
{
    [Fact]
    public async Task Adding_and_removing_shapes_the_cover()
    {
        using var set = await OpenSet(StrategyKind.AdditiveWins);

        await set.AddAsync([0, 0], [10, 10]);
        await set.RemoveAsync([3, 3], [5, 5]);

        set.Contains([1, 1]).ShouldBeTrue();
        set.Contains([4, 4]).ShouldBeFalse();
        set.Overlaps(new Box([4, 4], [4.5, 4.5])).ShouldBeFalse();
    }

    [Fact]
    public async Task A_line_through_the_cover_names_what_it_crosses()
    {
        using var set = await OpenSet(StrategyKind.GrowOnly);
        await set.AddAsync([0, 0], [2, 2]);
        await set.AddAsync([5, 0], [7, 2]);

        var crossed = set.Crossed(new Line([0, 1], [10, 1]));
        var segments = set.Traverse(new Line([0, 1], [10, 1]));

        crossed.Count.ShouldBe(2);
        segments.Count(segment => segment.Covered).ShouldBe(2);
        segments.Count(segment => !segment.Covered).ShouldBe(2);
        segments[0].Start.ShouldBe(0);
        segments[^1].End.ShouldBe(1);
    }

    [Fact]
    public async Task A_retraction_cancels_the_edit_it_names()
    {
        using var set = await OpenSet(StrategyKind.GrowOnly);
        var addition = await set.AddAsync([0], [10]);

        await set.RetractAsync(addition.Id);

        set.Contains([5]).ShouldBeFalse();
        set.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_retraction_cannot_be_undone_by_retracting_it()
    {
        using var set = await OpenSet(StrategyKind.GrowOnly);
        var addition = await set.AddAsync([0], [10]);
        var undo = await set.RetractAsync(addition.Id);

        await set.RetractAsync(undo.Id);

        set.Contains([5]).ShouldBeFalse();
    }

    [Fact]
    public async Task Retracting_under_a_chosen_identifier_twice_appends_once()
    {
        using var set = await OpenSet(StrategyKind.GrowOnly);
        var addition = await set.AddAsync([0], [10]);

        await set.RetractWithIdAsync("undo", addition.Id);
        await set.RetractWithIdAsync("undo", addition.Id);

        set.Ops.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Retracting_an_unknown_operation_is_a_caller_error()
    {
        using var set = await OpenSet(StrategyKind.GrowOnly);

        await Should.ThrowAsync<ArgumentException>(async () => await set.RetractAsync("missing"));
    }

    [Fact]
    public async Task A_closed_box_holds_its_corners()
    {
        using var set = await OpenSet(StrategyKind.GrowOnly);

        await set.AddWithBoundsAsync([0, 0], [2, 2], [Bound.Closed, Bound.Closed], [Bound.Closed, Bound.Closed]);

        set.Contains([0, 0]).ShouldBeTrue();
        set.Contains([2, 2]).ShouldBeTrue();
        set.Contains([2.5, 2]).ShouldBeFalse();
    }

    [Fact]
    public async Task Metadata_travels_through_the_facade()
    {
        using var set = await OpenSet(StrategyKind.GrowOnly);
        await set.AddWithMetaAsync([0], [10], Raw("""{"color":"red"}"""));

        set.Boxes.ShouldHaveSingleItem().Meta.IsDefaultOrEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task A_replica_in_a_directory_survives_a_restart()
    {
        using var directory = new TemporaryDirectory();
        IReadOnlyList<Box> before;
        using (var running = await BoxSet.OpenAsync(directory.DirectoryPath, StrategyKind.AdditiveWins))
        {
            await running.AddAsync([0], [10]);
            await running.RemoveAsync([3], [5]);
            before = running.Boxes;
        }

        using var reopened = await BoxSet.OpenAsync(directory.DirectoryPath, StrategyKind.AdditiveWins);

        reopened.Boxes.ShouldBe(before);
    }

    private static ValueTask<BoxSet> OpenSet(StrategyKind strategy) =>
        BoxSet.OpenLogAsync(new BoxMemoryLog(), strategy, new BoxEngineOptions { Clock = new LamportClock() });

    private static ImmutableArray<byte> Raw(string json) =>
        ImmutableArray.Create(Encoding.UTF8.GetBytes(json));
}
