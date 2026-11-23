using System.Collections.Immutable;

namespace EventfulRanges;

/// <summary>
/// A bounded straight segment through n dimensions, parameterized by <c>t</c> in
/// <c>[0, 1]</c>: <c>P(t) = From + t·(To − From)</c>. It is the query shape for
/// region questions — which boxes the segment crosses, in the order it meets them,
/// including the empty stretches between them.
/// </summary>
/// <remarks>
/// The Go original calls this <c>Path</c>; <c>Line</c> is used here so the type
/// cannot be confused with <see cref="System.IO.Path"/> in a file that also does
/// file I/O.
/// <para>
/// A crossing uses the same half-open convention as <see cref="Box"/>: a point on a
/// box's upper face belongs to the neighbouring box, not this one. A line that
/// grazes a face or runs along an edge therefore resolves deterministically to the
/// neighbouring box. The slab arithmetic divides and multiplies; every endpoint
/// comparison stays exact, and a zero-length line is a point query.
/// </para>
/// </remarks>
public sealed class Line
{
    /// <summary>Creates a line between two points.</summary>
    public Line(IReadOnlyList<double> from, IReadOnlyList<double> to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        From = [.. from];
        To = [.. to];
    }

    /// <summary>Where the line starts (<c>t = 0</c>).</summary>
    public ImmutableArray<double> From { get; }

    /// <summary>Where the line ends (<c>t = 1</c>).</summary>
    public ImmutableArray<double> To { get; }

    /// <summary>The number of dimensions.</summary>
    public int Dimensions => From.Length;

    /// <summary>Whether the line is well-formed.</summary>
    public bool IsValid => Validate() == LineError.None;

    /// <summary>Reports the first reason the line is not well-formed, if any.</summary>
    public LineError Validate()
    {
        if (From.Length != To.Length)
        {
            return LineError.Dimensions;
        }

        if (From.Length == 0)
        {
            return LineError.NoDimensions;
        }

        for (var axis = 0; axis < Dimensions; axis++)
        {
            var error = ValidateAxis(axis);
            if (error != LineError.None)
            {
                return error;
            }
        }

        return LineError.None;
    }

    /// <summary>Throws when the line is not well-formed.</summary>
    /// <exception cref="InvalidLineException">The line failed validation.</exception>
    public void EnsureValid()
    {
        var error = Validate();
        if (error != LineError.None)
        {
            throw new InvalidLineException(this, error);
        }
    }

    /// <summary>The position along the line at <paramref name="t"/>.</summary>
    public ImmutableArray<double> Point(double t)
    {
        var point = ImmutableArray.CreateBuilder<double>(Dimensions);
        for (var axis = 0; axis < Dimensions; axis++)
        {
            point.Add(From[axis] + (t * (To[axis] - From[axis])));
        }

        return point.MoveToImmutable();
    }

    /// <summary>
    /// The interval of <c>t</c> for which the line lies inside
    /// <paramref name="box"/>, clipped to <c>[0, 1]</c>; <c>null</c> when it never
    /// lies inside with positive length.
    /// </summary>
    /// <remarks>
    /// A line that merely grazes a face, runs along an edge, or has a different
    /// number of dimensions does not cross. Each axis contributes a half-open
    /// interval of <c>t</c> and the answer is their intersection, by the slab
    /// method.
    /// </remarks>
    public LineSpan? Crossing(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);
        if (box.Dimensions != Dimensions)
        {
            return null;
        }

        var start = 0.0;
        var end = 1.0;
        for (var axis = 0; axis < Dimensions; axis++)
        {
            if (EntryExit(axis, box) is not { } slab)
            {
                return null;
            }

            start = Math.Max(start, slab.Entry);
            end = Math.Min(end, slab.Exit);
        }

        return start < end ? new LineSpan(start, end) : null;
    }

    /// <summary>Renders the line as <c>(from) → (to)</c>.</summary>
    public override string ToString() =>
        FormattableString.Invariant($"({string.Join(", ", From)}) → ({string.Join(", ", To)})");

    private LineError ValidateAxis(int axis)
    {
        if (double.IsNaN(From[axis]) || double.IsNaN(To[axis]))
        {
            return LineError.NotANumber;
        }

        return double.IsInfinity(From[axis]) || double.IsInfinity(To[axis])
            ? LineError.NotFinite
            : LineError.None;
    }

    /// <summary>
    /// The interval of <c>t</c> on which one axis is inside the box, or
    /// <c>null</c> when a stationary coordinate lies outside it. A stationary
    /// coordinate inside the span contributes the whole run, imposing nothing.
    /// </summary>
    private (double Entry, double Exit)? EntryExit(int axis, Box box)
    {
        var from = From[axis];
        var to = To[axis];
        if (from == to)
        {
            var span = new Interval(box.Min[axis], box.Max[axis], box.LowestBound(axis), box.HighestBound(axis));
            return span.Contains(from) ? (0.0, 1.0) : null;
        }

        var delta = to - from;
        var atLow = (box.Min[axis] - from) / delta;
        var atHigh = (box.Max[axis] - from) / delta;
        return delta > 0 ? (atLow, atHigh) : (atHigh, atLow);
    }
}
