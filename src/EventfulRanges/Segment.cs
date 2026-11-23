namespace EventfulRanges;

/// <summary>
/// A maximal region decided by a single winner.
/// </summary>
/// <param name="Interval">The region.</param>
/// <param name="Winner">The operation that decided it.</param>
public readonly record struct Segment(Interval Interval, Winner Winner);
