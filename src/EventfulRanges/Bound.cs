namespace EventfulRanges;

/// <summary>
/// Marks whether an interval endpoint belongs to the interval.
/// </summary>
public enum Bound : byte
{
    /// <summary>The endpoint belongs to the interval.</summary>
    Closed = 0,

    /// <summary>The endpoint does not belong to the interval.</summary>
    Open = 1,
}

/// <summary>
/// The wire names of <see cref="Bound"/>: <c>"closed"</c> and <c>"open"</c>.
/// They are the names the original Go implementation writes to a log, so a
/// serialized interval reads the same in both languages.
/// </summary>
public static class BoundNames
{
    /// <summary>Returns the wire name of a bound, e.g. <c>"closed"</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a bound.</exception>
    public static string ToWireName(this Bound bound) => bound switch
    {
        Bound.Closed => "closed",
        Bound.Open => "open",
        _ => throw new ArgumentOutOfRangeException(nameof(bound), bound, "Unknown bound."),
    };

    /// <summary>Parses a wire name back to its bound.</summary>
    public static bool TryParse(string? name, out Bound bound)
    {
        switch (name)
        {
            case "closed":
                bound = Bound.Closed;
                return true;
            case "open":
                bound = Bound.Open;
                return true;
            default:
                bound = default;
                return false;
        }
    }
}
