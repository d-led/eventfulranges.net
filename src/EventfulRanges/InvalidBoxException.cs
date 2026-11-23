namespace EventfulRanges;

/// <summary>
/// Thrown when a box is used where a well-formed one is required.
/// </summary>
public sealed class InvalidBoxException : ArgumentException
{
    /// <summary>Creates the exception for a box that failed validation.</summary>
    public InvalidBoxException(Box box, BoxError error)
        : base(FormattableString.Invariant($"Box {box} is not well-formed: {error}."), nameof(box))
    {
        Box = box;
        Error = error;
    }

    /// <summary>The offending box.</summary>
    public Box Box { get; }

    /// <summary>Why the box was rejected.</summary>
    public BoxError Error { get; }
}
