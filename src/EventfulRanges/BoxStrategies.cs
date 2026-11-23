namespace EventfulRanges;

/// <summary>
/// The n-dimensional counterpart of <see cref="Strategies"/>: the same policies
/// over boxes, plus retraction.
/// </summary>
public static class BoxStrategies
{
    /// <summary>
    /// Resolves retractions: drops every retraction and the operation each one
    /// names, leaving the rest in their original order.
    /// </summary>
    /// <remarks>
    /// Retraction is single-level. Undoing an undone edit re-issues the original
    /// under a fresh identifier rather than retracting the retraction, so one pass
    /// over the log suffices and a retraction naming an unknown operation changes
    /// nothing.
    /// </remarks>
    public static IReadOnlyList<BoxOp> Effective(IReadOnlyList<BoxOp> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);

        var retracted = ops
            .Where(op => op.Kind == BoxOpKind.Retract)
            .Select(op => op.Ref)
            .ToHashSet(StringComparer.Ordinal);
        if (retracted.Count == 0)
        {
            return ops;
        }

        return ops
            .Where(op => op.Kind != BoxOpKind.Retract && !retracted.Contains(op.Id))
            .ToList();
    }
}
