namespace EventfulRanges;

/// <summary>
/// Reports why an <see cref="Interval"/> is not well-formed.
/// </summary>
public enum IntervalError
{
    /// <summary>The interval is well-formed.</summary>
    None = 0,

    /// <summary>An endpoint is not a number.</summary>
    NotANumber,

    /// <summary>An endpoint is infinite.</summary>
    NotFinite,

    /// <summary><see cref="Interval.Start"/> is after <see cref="Interval.End"/>.</summary>
    Inverted,

    /// <summary>The interval contains no point.</summary>
    Empty,
}
