using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace EventfulRanges.Benchmarks;

/// <summary>
/// Measures the two adjacent merges against each other, which is what
/// <see cref="Boxes.IndexedMergeThreshold"/> claims to be the crossing of.
/// </summary>
/// <remarks>
/// The cover is a partition of a square grid: thousands of disjoint cells that
/// touch edge to edge, which is the shape a visualizer hands the merge and the
/// one the indexes were written for. Run it with <c>scripts/bench.sh</c>.
/// </remarks>
[MemoryDiagnoser]
public class MergeBenchmarks
{
    /// <summary>The number of cells in the partition, either side of the threshold.</summary>
    [Params(128, 512, 2048, 4096)]
    public int Cells { get; set; }

    private List<Box> cover = [];

    [GlobalSetup]
    public void Partition()
    {
        var side = (int)Math.Round(Math.Sqrt(Cells));
        cover = [.. Enumerable.Range(0, side).SelectMany(row =>
            Enumerable.Range(0, side).Select(column =>
                new Box([column, row], [column + 1, row + 1])))];
    }

    /// <summary>The pairwise scan: the reference implementation.</summary>
    [Benchmark(Baseline = true)]
    public IReadOnlyList<Box> Scan() => Boxes.MergeAdjacentByScan(cover);

    /// <summary>The indexed merge: what runs above the threshold.</summary>
    [Benchmark]
    public IReadOnlyList<Box> Indexed() => BoxMerger.Merge(cover);
}

/// <summary>The entry point BenchmarkDotNet drives.</summary>
public static class Program
{
    /// <summary>Runs every benchmark in this assembly.</summary>
    public static void Main(string[] args) =>
        BenchmarkRunner.Run<MergeBenchmarks>(args: args);
}
