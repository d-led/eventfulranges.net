namespace EventfulRanges;

/// <summary>
/// One contiguous piece of a traversal: an interval of the line's parameter with
/// constant coverage. A covered piece names the boxes covering it; a gap is empty
/// space.
/// </summary>
public sealed class LineSegment : IEquatable<LineSegment>
{
    /// <summary>Creates a segment.</summary>
    /// <param name="start">Where the segment starts along the line.</param>
    /// <param name="end">Where it ends.</param>
    /// <param name="covered">Whether boxes cover it.</param>
    /// <param name="boxes">The boxes covering it, in canonical order; none for a gap.</param>
    public LineSegment(double start, double end, bool covered, IReadOnlyList<Box>? boxes = null)
    {
        Start = start;
        End = end;
        Covered = covered;
        Boxes = boxes ?? [];
    }

    /// <summary>Where the segment starts along the line.</summary>
    public double Start { get; }

    /// <summary>Where the segment ends.</summary>
    public double End { get; }

    /// <summary>Whether boxes cover the segment.</summary>
    public bool Covered { get; }

    /// <summary>The boxes covering it, in canonical order; empty for a gap.</summary>
    public IReadOnlyList<Box> Boxes { get; }

    /// <summary>Whether the segments describe the same piece of the same line.</summary>
    public bool Equals(LineSegment? other) =>
        other is not null
        && Start == other.Start
        && End == other.End
        && Covered == other.Covered
        && Boxes.SequenceEqual(other.Boxes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as LineSegment);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Start);
        hash.Add(End);
        hash.Add(Covered);
        foreach (var box in Boxes)
        {
            hash.Add(box);
        }

        return hash.ToHashCode();
    }

    /// <summary>Renders the segment as <c>[start, end)</c> with what covers it.</summary>
    public override string ToString() =>
        FormattableString.Invariant(
            $"[{Start}, {End}) {(Covered ? $"covered by {Boxes.Count} box(es)" : "empty")}");
}
