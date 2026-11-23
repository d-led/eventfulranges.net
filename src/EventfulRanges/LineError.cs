namespace EventfulRanges;

/// <summary>
/// Reports why a <see cref="Line"/> is not well-formed.
/// </summary>
public enum LineError
{
    /// <summary>The line is well-formed.</summary>
    None = 0,

    /// <summary>The two endpoints have different lengths.</summary>
    Dimensions,

    /// <summary>The line has no coordinates.</summary>
    NoDimensions,

    /// <summary>A coordinate is not a number.</summary>
    NotANumber,

    /// <summary>A coordinate is infinite.</summary>
    NotFinite,
}
