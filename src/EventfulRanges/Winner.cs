namespace EventfulRanges;

/// <summary>
/// The operation that decided a region of the line, identified by the total
/// order strategies compare: timestamp first, then identifier.
/// </summary>
/// <param name="Timestamp">The stamp the operation was applied with.</param>
/// <param name="Id">The operation identifier, which breaks timestamp ties.</param>
/// <param name="Kind">Whether the winner adds or removes its region.</param>
public readonly record struct Winner(long Timestamp, string Id, OpKind Kind);
