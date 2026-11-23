using System.Collections.Immutable;
using CsCheck;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The merge has two implementations — a pairwise scan and a set of indexes — and
/// only one contract: whichever runs, the cover is the same. The indexes are what
/// keep a partition of thousands of cells from taking minutes, so the agreement
/// has to be checked rather than assumed.
/// </summary>
public sealed class MergeIndexTests
{
    /// <summary>
    /// Covers drawn from a coarse grid, so boxes overlap and nest as often as they
    /// miss, with faces mixed so that a pinhole has to keep a pair apart.
    /// </summary>
    private static readonly Gen<List<Box>> Covers =
        Gen.Int[1, 3].SelectMany(dimensions =>
            Gen.Int[0, 4].Array[dimensions].SelectMany(low =>
                Gen.Int[0, 3].Array[dimensions].SelectMany(size =>
                    Gen.Int[0, 3].Array[dimensions].Select(faces =>
                        new Box(
                            [.. low.Select(value => (double)value)],
                            [.. low.Zip(size, (start, length) => (double)(start + length))],
                            [.. faces.Select(code => code is 0 or 2 ? Bound.Closed : Bound.Open)],
                            [.. faces.Select(code => code is 0 or 1 ? Bound.Closed : Bound.Open)]))))
            .List[0, 8]);

    [Fact]
    public void The_indexed_merge_agrees_with_the_scan_on_random_covers()
    {
        Covers.Sample(cover =>
        {
            var indexed = BoxMerger.Merge(cover);
            var scanned = Boxes.MergeAdjacentByScan(cover);

            Boxes.Equal(indexed, scanned).ShouldBeTrue(
                $"cover: {Render(cover)}\n  indexed: {Render(indexed)}\n  scanned: {Render(scanned)}");
            TotalMeta(indexed).ShouldBe(TotalMeta(scanned));
        });
    }

    [Fact]
    public void One_merge_can_swallow_a_box_neither_half_covered()
    {
        var cover = new List<Box>
        {
            Cell(0, 0, 5, 10),
            Cell(5, 0, 10, 10),
            Cell(2, 3, 8, 7),
        };

        BoxMerger.Merge(cover).ShouldHaveSingleItem().ShouldBe(Cell(0, 0, 10, 10));
    }

    [Fact]
    public void A_partition_of_thousands_of_cells_collapses_to_one_box()
    {
        var cells = Enumerable.Range(0, 48)
            .SelectMany(row => Enumerable.Range(0, 48).Select(column => Cell(column, row, column + 1, row + 1)))
            .ToList();

        cells.Count.ShouldBeGreaterThan(Boxes.IndexedMergeThreshold);

        Boxes.MergeAdjacent(cells).ShouldHaveSingleItem().ShouldBe(Cell(0, 0, 48, 48));
    }

    [Fact]
    public void Metadata_survives_the_indexed_merge()
    {
        var tagged = Cell(0, 0, 1, 1).WithMeta([.. """{"side":"left"}"""u8]);

        var merged = BoxMerger.Merge([tagged, Cell(0, 0, 1, 1)]);

        merged.ShouldHaveSingleItem().Meta.ShouldBe(tagged.Meta);
    }

    [Fact]
    public void An_indexed_merge_of_nothing_is_nothing()
    {
        BoxMerger.Merge([]).ShouldBeEmpty();
    }

    private static Box Cell(double minX, double minY, double maxX, double maxY) =>
        new([minX, minY], [maxX, maxY]);

    /// <summary>The metadata of the whole cover, joined: no path may lose any of it.</summary>
    private static ImmutableArray<byte> TotalMeta(IEnumerable<Box> boxes) =>
        boxes.Aggregate(ImmutableArray<byte>.Empty, (joined, box) => Meta.Union(joined, box.Meta));

    private static string Render(IEnumerable<Box> boxes) => string.Join(", ", boxes);
}
