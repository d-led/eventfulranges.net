using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// The kind of a box operation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BoxOpKind>))]
public enum BoxOpKind : byte
{
    /// <summary>The box joins the materialized set.</summary>
    [JsonStringEnumMemberName("add")]
    Add = 0,

    /// <summary>The box leaves the materialized set.</summary>
    [JsonStringEnumMemberName("remove")]
    Remove = 1,

    /// <summary>The operation named by the reference is cancelled.</summary>
    [JsonStringEnumMemberName("retract")]
    Retract = 2,
}

/// <summary>
/// The wire names of <see cref="BoxOpKind"/>: <c>"add"</c>, <c>"remove"</c> and
/// <c>"retract"</c>, the names the original implementation writes to a log.
/// </summary>
public static class BoxOpKindNames
{
    /// <summary>Returns the wire name of a box operation kind.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a box operation kind.</exception>
    public static string ToWireName(this BoxOpKind kind) => kind switch
    {
        BoxOpKind.Add => "add",
        BoxOpKind.Remove => "remove",
        BoxOpKind.Retract => "retract",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown box operation kind."),
    };

    /// <summary>Parses a wire name back to its box operation kind.</summary>
    public static bool TryParse(string? name, out BoxOpKind kind)
    {
        switch (name)
        {
            case "add":
                kind = BoxOpKind.Add;
                return true;
            case "remove":
                kind = BoxOpKind.Remove;
                return true;
            case "retract":
                kind = BoxOpKind.Retract;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
