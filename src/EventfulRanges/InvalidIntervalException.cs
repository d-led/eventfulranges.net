namespace EventfulRanges;

/// <summary>
/// Thrown when an interval is used where a well-formed one is required.
/// </summary>
public sealed class InvalidIntervalException : ArgumentException
{
    /// <summary>Creates the exception for an interval that failed validation.</summary>
    public InvalidIntervalException(Interval interval, IntervalError error)
        : base(FormattableString.Invariant($"Interval {interval} is not well-formed: {error}."), nameof(interval))
    {
        Interval = interval;
        Error = error;
    }

    /// <summary>The offending interval.</summary>
    public Interval Interval { get; }

    /// <summary>Why the interval was rejected.</summary>
    public IntervalError Error { get; }
}
