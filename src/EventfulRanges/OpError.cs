namespace EventfulRanges;

/// <summary>
/// Reports why an <see cref="Op"/> is not well-formed.
/// </summary>
public enum OpError
{
    /// <summary>The operation is well-formed.</summary>
    None = 0,

    /// <summary><see cref="Op.Id"/> is null or empty.</summary>
    MissingId,

    /// <summary><see cref="Op.Kind"/> is not a defined operation kind.</summary>
    UnknownKind,

    /// <summary><see cref="Op.Interval"/> is not well-formed; see <see cref="Interval.Validate"/>.</summary>
    InvalidInterval,
}

/// <summary>
/// Thrown when an operation is used where a well-formed one is required.
/// </summary>
public sealed class InvalidOpException : ArgumentException
{
    /// <summary>Creates the exception for an operation that failed validation.</summary>
    public InvalidOpException(Op op, OpError error)
        : base(FormattableString.Invariant($"Operation {op} is not well-formed: {error}."), nameof(op))
    {
        Op = op;
        Error = error;
    }

    /// <summary>The offending operation.</summary>
    public Op Op { get; }

    /// <summary>Why the operation was rejected.</summary>
    public OpError Error { get; }
}
