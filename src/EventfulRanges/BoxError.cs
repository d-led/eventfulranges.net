namespace EventfulRanges;

/// <summary>
/// Reports why a <see cref="Box"/> is not well-formed.
/// </summary>
public enum BoxError
{
    /// <summary>The box is well-formed.</summary>
    None = 0,

    /// <summary>The lower and upper corners have different lengths.</summary>
    Dimensions,

    /// <summary>The box has no coordinates.</summary>
    NoDimensions,

    /// <summary>A bound slice does not match the number of dimensions.</summary>
    Bounds,

    /// <summary>A coordinate is not a number.</summary>
    NotANumber,

    /// <summary>A coordinate is infinite.</summary>
    NotFinite,

    /// <summary>A lower corner lies above the upper one on some axis.</summary>
    Inverted,
}
