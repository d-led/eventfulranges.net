namespace EventfulRanges;

/// <summary>
/// The operation that decided a region of space, identified by the total order the
/// ranking strategies compare: timestamp first, then identifier.
/// </summary>
/// <param name="Timestamp">The stamp the operation was applied with.</param>
/// <param name="Id">The operation identifier, which breaks timestamp ties.</param>
/// <param name="Kind">Whether the winner adds, removes, or retracts.</param>
public readonly record struct BoxWinner(long Timestamp, string Id, BoxOpKind Kind);

/// <summary>
/// A maximal region decided by a single winner.
/// </summary>
/// <param name="Box">The region.</param>
/// <param name="Winner">The operation that decided it.</param>
public readonly record struct BoxSegment(Box Box, BoxWinner Winner);
