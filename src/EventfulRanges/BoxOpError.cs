namespace EventfulRanges;

/// <summary>
/// Reports why a <see cref="BoxOp"/> is not well-formed.
/// </summary>
public enum BoxOpError
{
    /// <summary>The operation is well-formed.</summary>
    None = 0,

    /// <summary>The operation has no identifier.</summary>
    MissingId,

    /// <summary>A retraction names no target.</summary>
    MissingRetractTarget,

    /// <summary>The operation's kind is not one the library knows.</summary>
    UnknownKind,

    /// <summary>The operation's box is malformed.</summary>
    InvalidBox,

    /// <summary>The operation's box holds no point.</summary>
    EmptyBox,
}

/// <summary>
/// Thrown when a box operation is used where a well-formed one is required.
/// </summary>
public sealed class InvalidBoxOpException : ArgumentException
{
    /// <summary>Creates the exception for an operation that failed validation.</summary>
    public InvalidBoxOpException(BoxOp op, BoxOpError error)
        : base(FormattableString.Invariant($"Operation {op} is not well-formed: {error}."), nameof(op))
    {
        Op = op;
        Error = error;
    }

    /// <summary>The offending operation.</summary>
    public BoxOp Op { get; }

    /// <summary>Why the operation was rejected.</summary>
    public BoxOpError Error { get; }
}
