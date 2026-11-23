namespace EventfulRanges;

/// <summary>
/// One maximal piece of a swept union: the interval and the index of the best
/// interval covering it.
/// </summary>
public readonly record struct Cover(Interval Interval, int Index);
