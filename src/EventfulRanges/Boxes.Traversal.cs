namespace EventfulRanges;

/// <summary>
/// The questions a line asks of a cover: what it crosses, and what covers which
/// stretch of it.
/// </summary>
public static partial class Boxes
{
    /// <summary>
    /// The boxes the line crosses with positive length, in canonical cover order.
    /// The cover is normalized first, so the answer reflects the cover rather than
    /// the raw list, and a box the line only grazes is not crossed.
    /// </summary>
    public static IReadOnlyList<Box> Crossed(IReadOnlyList<Box> boxes, Line line)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(line);

        return Normalize(boxes).Where(box => line.Crossing(box) is not null).ToList();
    }

    /// <summary>
    /// Splits the line's parameter interval <c>[0, 1]</c> into maximal segments of
    /// constant coverage, in the order the line passes through them, so covered
    /// stretches and gaps alternate as the line enters and leaves boxes.
    /// </summary>
    /// <remarks>
    /// The cover is normalized first. The result tiles <c>[0, 1]</c> exactly: the
    /// first segment starts at 0, the last ends at 1, and each segment's end is the
    /// next segment's start.
    /// </remarks>
    public static IReadOnlyList<LineSegment> Traverse(IReadOnlyList<Box> boxes, Line line)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(line);

        var crossings = (from box in Normalize(boxes)
                         let crossing = line.Crossing(box)
                         where crossing is not null
                         select (Box: box, Span: crossing.Value)).ToList();
        if (crossings.Count == 0)
        {
            return [new LineSegment(0, 1, covered: false)];
        }

        var breakpoints = Breakpoints(crossings);
        var segments = new List<LineSegment>(breakpoints.Count - 1);
        for (var index = 0; index < breakpoints.Count - 1; index++)
        {
            segments.Add(SegmentBetween(crossings, breakpoints[index], breakpoints[index + 1]));
        }

        return segments;
    }

    /// <summary>
    /// The segment between two consecutive breakpoints: coverage is constant
    /// there, so it belongs to exactly the boxes whose span holds the whole piece.
    /// </summary>
    private static LineSegment SegmentBetween(
        List<(Box Box, LineSpan Span)> crossings,
        double start,
        double end)
    {
        var covering = crossings
            .Where(crossing => crossing.Span.Start <= start && crossing.Span.End >= end)
            .Select(crossing => crossing.Box)
            .Order()
            .ToList();

        return new LineSegment(start, end, covered: covering.Count > 0, covering);
    }

    /// <summary>
    /// Every span boundary, sorted and deduplicated, bookended by the line's own
    /// parameter range.
    /// </summary>
    private static List<double> Breakpoints(List<(Box Box, LineSpan Span)> crossings)
    {
        var boundaries = new List<double>(crossings.Count + 1) { 0, 1 };
        boundaries.AddRange(crossings.SelectMany(crossing => new[] { crossing.Span.Start, crossing.Span.End }));

        boundaries.Sort();
        return [.. boundaries.Distinct()];
    }
}
