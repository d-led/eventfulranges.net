namespace EventfulRanges;

/// <summary>
/// Thrown when a line is used where a well-formed one is required.
/// </summary>
public sealed class InvalidLineException : ArgumentException
{
    /// <summary>Creates the exception for a line that failed validation.</summary>
    public InvalidLineException(Line line, LineError error)
        : base(FormattableString.Invariant($"Line {line} is not well-formed: {error}."), nameof(line))
    {
        Line = line;
        Error = error;
    }

    /// <summary>The offending line.</summary>
    public Line Line { get; }

    /// <summary>Why the line was rejected.</summary>
    public LineError Error { get; }
}
