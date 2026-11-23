using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// A contiguous, non-empty range of real values with per-side inclusivity:
/// <c>[a, b]</c>, <c>[a, b)</c>, <c>(a, b]</c> or <c>(a, b)</c>.
/// The default value is the point interval <c>[0, 0]</c>.
/// </summary>
/// <remarks>
/// Endpoints are compared, never combined, and every endpoint is used exactly
/// as given, so floating-point values never accumulate rounding error. A
/// value is not required to be well-formed; <see cref="Validate"/> reports
/// what is wrong with it, and <see cref="EnsureValid"/> rejects it where a
/// well-formed interval is required.
/// </remarks>
public readonly record struct Interval
{
    /// <summary>Creates the closed interval <c>[start, end]</c>.</summary>
    public Interval(double start, double end)
        : this(start, end, Bound.Closed, Bound.Closed)
    {
    }

    /// <summary>Creates an interval with explicit inclusivity for each endpoint.</summary>
    [JsonConstructor]
    public Interval(double start, double end, Bound startBound, Bound endBound)
    {
        Start = start;
        End = end;
        StartBound = startBound;
        EndBound = endBound;
    }

    /// <summary>The lower endpoint.</summary>
    [JsonPropertyName("start")]
    public double Start { get; }

    /// <summary>The upper endpoint.</summary>
    [JsonPropertyName("end")]
    public double End { get; }

    /// <summary>Whether <see cref="Start"/> belongs to the interval.</summary>
    [JsonPropertyName("startBound")]
    public Bound StartBound { get; }

    /// <summary>Whether <see cref="End"/> belongs to the interval.</summary>
    [JsonPropertyName("endBound")]
    public Bound EndBound { get; }

    /// <summary>Whether the interval is well-formed.</summary>
    [JsonIgnore]
    public bool IsValid => Validate() == IntervalError.None;

    /// <summary>Whether the interval contains no point at all.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        Start > End || (Start == End && (StartBound == Bound.Open || EndBound == Bound.Open));

    /// <summary>Reports the first reason the interval is not well-formed, if any.</summary>
    public IntervalError Validate()
    {
        if (HasNotANumber())
        {
            return IntervalError.NotANumber;
        }

        if (HasInfiniteEndpoint())
        {
            return IntervalError.NotFinite;
        }

        if (Start > End)
        {
            return IntervalError.Inverted;
        }

        return IsEmpty ? IntervalError.Empty : IntervalError.None;
    }

    private bool HasNotANumber() => double.IsNaN(Start) || double.IsNaN(End);

    private bool HasInfiniteEndpoint() => double.IsInfinity(Start) || double.IsInfinity(End);

    /// <summary>Throws when the interval is not well-formed.</summary>
    /// <exception cref="InvalidIntervalException">The interval failed validation.</exception>
    public void EnsureValid()
    {
        var error = Validate();
        if (error != IntervalError.None)
        {
            throw new InvalidIntervalException(this, error);
        }
    }

    /// <summary>Whether <paramref name="value"/> belongs to the interval.</summary>
    public bool Contains(double value) =>
        value >= Start && value <= End && !IsExcludedByAnOpenBound(value);

    /// <summary>
    /// Whether an endpoint excludes <paramref name="value"/> because the bound
    /// on that side is open.
    /// </summary>
    private bool IsExcludedByAnOpenBound(double value) =>
        (value == Start && StartBound == Bound.Open) || (value == End && EndBound == Bound.Open);

    /// <summary>Whether the intervals share at least one point.</summary>
    public bool Overlaps(Interval other)
    {
        var (low, lowBound) = MaxStart(this, other);
        var (high, highBound) = MinEnd(this, other);
        if (low > high)
        {
            return false;
        }

        if (low < high)
        {
            return true;
        }

        return lowBound == Bound.Closed && highBound == Bound.Closed;
    }

    /// <summary>
    /// Whether the intervals meet at exactly one endpoint that belongs to at
    /// least one of them, without sharing it.
    /// </summary>
    public bool Touches(Interval other)
    {
        if (End == other.Start)
        {
            return MeetsAt(EndBound, other.StartBound, other);
        }

        if (other.End == Start)
        {
            return MeetsAt(other.EndBound, StartBound, other);
        }

        return false;
    }

    /// <summary>
    /// Whether two touching endpoints count as touching: at least one side
    /// includes the point, and the intervals share nothing else.
    /// </summary>
    private bool MeetsAt(Bound left, Bound right, Interval other) =>
        (left == Bound.Closed || right == Bound.Closed) && !Overlaps(other);

    /// <summary>
    /// Combines two intervals whose union is contiguous into the smallest
    /// interval containing both.
    /// </summary>
    public static Interval Merge(Interval first, Interval second)
    {
        var (start, startBound) = LowerStart(first, second);
        var (end, endBound) = UpperEnd(first, second);
        return new Interval(start, end, startBound, endBound);
    }

    private static (double Start, Bound Bound) LowerStart(Interval first, Interval second) =>
        second.Start < first.Start ? (second.Start, second.StartBound)
        : second.Start > first.Start ? (first.Start, first.StartBound)
        : (first.Start, MoreClosed(first.StartBound, second.StartBound));

    private static (double End, Bound Bound) UpperEnd(Interval first, Interval second) =>
        second.End > first.End ? (second.End, second.EndBound)
        : second.End < first.End ? (first.End, first.EndBound)
        : (first.End, MoreClosed(first.EndBound, second.EndBound));

    /// <summary>
    /// The parts of this interval left after removing every point covered by
    /// <paramref name="cut"/>: zero, one or two intervals, in order.
    /// </summary>
    public IReadOnlyList<Interval> Subtract(Interval cut)
    {
        if (!Meets(cut))
        {
            return new[] { this };
        }

        var parts = new List<Interval>(2);
        AddPart(parts, LeftPart(this, cut));
        AddPart(parts, RightPart(this, cut));
        return parts;
    }

    /// <summary>
    /// Whether the union of the two intervals is contiguous: they share a
    /// point, or they meet at one endpoint that belongs to at least one of
    /// them.
    /// </summary>
    public bool Meets(Interval other) => Overlaps(other) || Touches(other);

    private static void AddPart(List<Interval> parts, Interval? part)
    {
        if (part is { } value)
        {
            parts.Add(value);
        }
    }

    /// <summary>The shared interval of two overlapping intervals.</summary>
    /// <exception cref="InvalidOperationException">The intervals do not overlap.</exception>
    public static Interval Intersection(Interval first, Interval second) =>
        TryIntersection(first, second, out var intersection)
            ? intersection
            : throw new InvalidOperationException(
                FormattableString.Invariant($"Intervals {first} and {second} do not overlap."));

    /// <summary>Attempts to compute the shared interval of two intervals.</summary>
    public static bool TryIntersection(Interval first, Interval second, out Interval intersection)
    {
        if (!first.Overlaps(second))
        {
            intersection = default;
            return false;
        }

        var (start, startBound) = MaxStart(first, second);
        var (end, endBound) = MinEnd(first, second);
        intersection = new Interval(start, end, startBound, endBound);
        return true;
    }

    /// <summary>Renders the interval in standard notation, e.g. <c>[1,5)</c>.</summary>
    public override string ToString()
    {
        var left = StartBound == Bound.Open ? '(' : '[';
        var right = EndBound == Bound.Open ? ')' : ']';
        return FormattableString.Invariant($"{left}{Start},{End}{right}");
    }

    private static Interval? LeftPart(Interval interval, Interval cut)
    {
        if (interval.Start < cut.Start)
        {
            return new Interval(interval.Start, cut.Start, interval.StartBound, Invert(cut.StartBound));
        }

        if (interval.Start == cut.Start &&
            interval.StartBound == Bound.Closed &&
            cut.StartBound == Bound.Open)
        {
            return new Interval(interval.Start, cut.Start);
        }

        return null;
    }

    private static Interval? RightPart(Interval interval, Interval cut)
    {
        if (cut.End < interval.End)
        {
            return new Interval(cut.End, interval.End, Invert(cut.EndBound), interval.EndBound);
        }

        if (cut.End == interval.End &&
            cut.EndBound == Bound.Open &&
            interval.EndBound == Bound.Closed)
        {
            return new Interval(cut.End, interval.End);
        }

        return null;
    }

    private static Bound Invert(Bound bound) => bound == Bound.Closed ? Bound.Open : Bound.Closed;

    // A shared endpoint belongs to the intersection only when both intervals
    // include it. The original Go implementation used "either side closed"
    // here, which made a point interval overlap a range that excludes it.
    private static (double Start, Bound Bound) MaxStart(Interval first, Interval second) =>
        first.Start > second.Start ? (first.Start, first.StartBound)
        : first.Start < second.Start ? (second.Start, second.StartBound)
        : (first.Start, Stricter(first.StartBound, second.StartBound));

    private static (double End, Bound Bound) MinEnd(Interval first, Interval second) =>
        first.End < second.End ? (first.End, first.EndBound)
        : first.End > second.End ? (second.End, second.EndBound)
        : (first.End, Stricter(first.EndBound, second.EndBound));

    /// <summary>Closed only when both are closed: the rule an intersection needs.</summary>
    private static Bound Stricter(Bound first, Bound second) =>
        first == Bound.Closed && second == Bound.Closed ? Bound.Closed : Bound.Open;

    /// <summary>Closed when either is closed: the rule a union needs.</summary>
    private static Bound MoreClosed(Bound first, Bound second) =>
        first == Bound.Closed || second == Bound.Closed ? Bound.Closed : Bound.Open;
}
