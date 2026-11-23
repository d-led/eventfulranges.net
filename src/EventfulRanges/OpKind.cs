using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// The kind of a range operation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OpKind>))]
public enum OpKind : byte
{
    /// <summary>The interval joins the materialized set.</summary>
    [JsonStringEnumMemberName("add")]
    Add = 0,

    /// <summary>The interval leaves the materialized set.</summary>
    [JsonStringEnumMemberName("remove")]
    Remove = 1,
}

/// <summary>
/// The wire names of <see cref="OpKind"/>: <c>"add"</c> and <c>"remove"</c>,
/// the names the original implementation writes to a log.
/// </summary>
public static class OpKindNames
{
    /// <summary>Returns the wire name of an operation kind.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not an operation kind.</exception>
    public static string ToWireName(this OpKind kind) => kind switch
    {
        OpKind.Add => "add",
        OpKind.Remove => "remove",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown operation kind."),
    };

    /// <summary>Parses a wire name back to its operation kind.</summary>
    public static bool TryParse(string? name, out OpKind kind)
    {
        switch (name)
        {
            case "add":
                kind = OpKind.Add;
                return true;
            case "remove":
                kind = OpKind.Remove;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
