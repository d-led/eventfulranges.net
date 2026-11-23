using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The public surface refuses a null argument by name instead of failing later
/// with a null reference somewhere inside. Mutation testing found these guards
/// unexercised: every one of them could have been deleted with the suite green.
/// </summary>
public sealed class NullArgumentTests
{
    private static readonly Box AnyBox = new([0], [1]);

    private static readonly Interval AnyInterval = new(0, 1);

    private static readonly Line AnyLine = new([0], [1]);

    public static TheoryData<string, Action> Synchronous => new()
    {
        { "new Box(min, max)", () => _ = new Box(null!, [1]) },
        { "new Box(min, max)", () => _ = new Box([0], null!) },
        { "new Line(from, to)", () => _ = new Line(null!, [1]) },
        { "Box.Contains(box)", () => AnyBox.Contains((Box)null!) },
        { "Box.Contains(point)", () => AnyBox.Contains((IReadOnlyList<double>)null!) },
        { "Box.Overlaps(box)", () => AnyBox.Overlaps(null!) },
        { "Line.Crossing(box)", () => AnyLine.Crossing(null!) },
        { "Boxes.Normalize(boxes)", () => Boxes.Normalize(null!) },
        { "Boxes.Normalize(boxes, merge)", () => Boxes.Normalize([AnyBox], null!) },
        { "Boxes.Union(first, second)", () => Boxes.Union(null!, [AnyBox]) },
        { "Boxes.Difference(first, second)", () => Boxes.Difference(null!, [AnyBox]) },
        { "Boxes.InsertNormalized(cover, box)", () => Boxes.InsertNormalized(null!, AnyBox) },
        { "Boxes.MergeAdjacent(boxes)", () => Boxes.MergeAdjacent(null!) },
        { "Boxes.Contains(cover, point)", () => Boxes.Contains(null!, [0]) },
        { "Boxes.Overlaps(cover, box)", () => Boxes.Overlaps(null!, AnyBox) },
        { "Boxes.Equal(first, second)", () => Boxes.Equal(null!, [AnyBox]) },
        { "Boxes.Components(boxes)", () => Boxes.Components(null!) },
        { "Boxes.Crossed(cover, line)", () => Boxes.Crossed(null!, AnyLine) },
        { "Boxes.Traverse(cover, line)", () => Boxes.Traverse(null!, AnyLine) },
        { "Canonicalizers.Chain(canonicalizers)", () => Canonicalizers.Chain(null!) },
        { "Intervals.Normalize(intervals)", () => Intervals.Normalize(null!) },
        { "Intervals.Union(first, second)", () => Intervals.Union(null!, [AnyInterval]) },
        { "Intervals.Difference(first, second)", () => Intervals.Difference(null!, [AnyInterval]) },
        { "Intervals.Contains(set, value)", () => Intervals.Contains(null!, 0) },
        { "Intervals.Overlaps(set, interval)", () => Intervals.Overlaps(null!, AnyInterval) },
        { "Intervals.Equal(first, second)", () => Intervals.Equal(null!, [AnyInterval]) },
        { "Strategies.Segments(strategy, ops)", () => Strategies.Segments(StrategyKind.Lww, null!) },
        { "Strategies.Materialize(strategy, ops)", () => Strategies.Materialize(StrategyKind.Lww, null!) },
        { "Strategies.CombineSegments(strategy, first, second)", () => Strategies.CombineSegments(StrategyKind.Lww, null!, []) },
        { "Strategies.ToIntervals(segments)", () => Strategies.ToIntervals(null!) },
        { "Strategies.IntervalsOf(ops, kind)", () => Strategies.IntervalsOf(null!, OpKind.Add) },
        { "BoxStrategies.Segments(strategy, ops)", () => BoxStrategies.Segments(StrategyKind.GrowOnly, null!) },
        { "BoxStrategies.Materialize(strategy, ops)", () => BoxStrategies.Materialize(StrategyKind.GrowOnly, null!) },
        { "BoxStrategies.CombineSegments(strategy, first, second)", () => BoxStrategies.CombineSegments(StrategyKind.GrowOnly, null!, []) },
        { "BoxStrategies.ToBoxes(segments)", () => BoxStrategies.ToBoxes(null!) },
        { "BoxStrategies.Effective(ops)", () => BoxStrategies.Effective(null!) },
        { "BoxStrategies.Layers(ops)", () => BoxStrategies.Layers(null!) },
        { "RTree.Build(boxes)", () => RTree.Build(null!) },
        { "RTree.Search(query)", () => RTree.Build([AnyBox]).Search(null!) },
        { "RTree.SearchInTraversalOrder(query)", () => RTree.Build([AnyBox]).SearchInTraversalOrder(null!) },
    };

    public static TheoryData<string, Func<Task>> Asynchronous => new()
    {
        { "Engine.OpenAsync(log)", () => Engine.OpenAsync(null!, StrategyKind.Lww).AsTask() },
        {
            "Engine.ApplyAsync(op)",
            async () =>
            {
                using var engine = await Engine.OpenAsync(new MemoryLog(), StrategyKind.Lww);
                await engine.ApplyAsync(null!);
            }
        },
        {
            "Engine.ApplyAllAsync(ops)",
            async () =>
            {
                using var engine = await Engine.OpenAsync(new MemoryLog(), StrategyKind.Lww);
                await engine.ApplyAllAsync(null!);
            }
        },
        { "MemoryLog.AppendAsync(ops)", () => new MemoryLog().AppendAsync(0, null!).AsTask() },
        { "MemoryLog.SaveSnapshotAsync(data)", () => new MemoryLog().SaveSnapshotAsync(null!, 0).AsTask() },
        { "MemoryLog.CompactAsync(data)", () => new MemoryLog().CompactAsync(null!, 0).AsTask() },
        { "BoxMemoryLog.AppendAsync(ops)", () => new BoxMemoryLog().AppendAsync(0, null!).AsTask() },
        { "BoxEngine.OpenAsync(log)", () => BoxEngine.OpenAsync(null!, StrategyKind.GrowOnly).AsTask() },
        {
            "BoxEngine.ApplyAsync(op)",
            async () =>
            {
                using var engine = await BoxEngine.OpenAsync(new BoxMemoryLog(), StrategyKind.GrowOnly);
                await engine.ApplyAsync(null!);
            }
        },
        {
            "BoxEngine.Op(id)",
            async () =>
            {
                using var engine = await BoxEngine.OpenAsync(new BoxMemoryLog(), StrategyKind.GrowOnly);
                engine.Op(null!);
            }
        },
        { "BoxSet.OpenAsync(directory)", () => BoxSet.OpenAsync(null!, StrategyKind.GrowOnly).AsTask() },
        { "BoxSet.OpenLogAsync(log)", () => BoxSet.OpenLogAsync(null!, StrategyKind.GrowOnly).AsTask() },
    };

    [Theory]
    [MemberData(nameof(Synchronous))]
    public void The_public_surface_refuses_null(string entryPoint, Action call) =>
        Should.Throw<ArgumentNullException>(call, $"{entryPoint} should have refused a null argument");

    [Theory]
    [MemberData(nameof(Asynchronous))]
    public async Task The_public_surface_refuses_null_asynchronously(string entryPoint, Func<Task> call) =>
        await Should.ThrowAsync<ArgumentNullException>(call, $"{entryPoint} should have refused a null argument");
}
