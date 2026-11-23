namespace EventfulRanges;

/// <summary>
/// Rewrites a cover of boxes into another cover of the same points. It is the
/// seam the n-dimensional engine compacts through: the default keeps partially
/// overlapping boxes as they are, while a custom canonicalizer may subdivide
/// them, break boundary ties deliberately, or index them spatially.
/// </summary>
/// <remarks>
/// Two properties are required, because replicas depend on them: a canonicalizer
/// must be cover-preserving — a point is covered by the output exactly when the
/// input covered it — and deterministic, so the same input always yields the
/// same output.
/// </remarks>
/// <param name="boxes">The cover to rewrite.</param>
/// <returns>Another cover of the same points.</returns>
public delegate IReadOnlyList<Box> Canonicalizer(IReadOnlyList<Box> boxes);

/// <summary>
/// Composes canonicalizers.
/// </summary>
public static class Canonicalizers
{
    /// <summary>
    /// Runs <paramref name="canonicalizers"/> left to right. A missing one is
    /// skipped, so a pipeline can be assembled without branching.
    /// </summary>
    /// <param name="canonicalizers">The steps, in the order they apply.</param>
    public static Canonicalizer Chain(params Canonicalizer?[] canonicalizers)
    {
        ArgumentNullException.ThrowIfNull(canonicalizers);
        var steps = canonicalizers.OfType<Canonicalizer>().ToArray();

        return boxes =>
        {
            ArgumentNullException.ThrowIfNull(boxes);
            return steps.Aggregate(boxes, (cover, step) => step(cover));
        };
    }
}
