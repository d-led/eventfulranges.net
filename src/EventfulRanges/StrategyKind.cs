using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// How concurrent operations resolve into one materialized set.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StrategyKind>))]
public enum StrategyKind : byte
{
    /// <summary>The operation with the highest <c>(timestamp, id)</c> wins at each point.</summary>
    [JsonStringEnumMemberName("lww")]
    Lww = 0,

    /// <summary>The operation with the lowest <c>(timestamp, id)</c> wins at each point.</summary>
    [JsonStringEnumMemberName("fww")]
    Fww = 1,

    /// <summary>The union of every addition minus the union of every removal.</summary>
    [JsonStringEnumMemberName("additive-wins")]
    AdditiveWins = 2,

    /// <summary>The union of every addition; removals are ignored.</summary>
    [JsonStringEnumMemberName("grow-only")]
    GrowOnly = 3,

    /// <summary>
    /// The union of every addition minus every removal, with each point's metadata
    /// decided by the latest addition covering it. It is the n-dimensional
    /// strategy: a range carries no metadata for a point to resolve.
    /// </summary>
    [JsonStringEnumMemberName("additive-wins-lww")]
    AdditiveWinsLww = 4,
}

/// <summary>
/// The wire names of <see cref="StrategyKind"/>: the names the original
/// implementation writes to a log or a configuration file.
/// </summary>
public static class StrategyNames
{
    /// <summary>Returns the wire name of a strategy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a strategy.</exception>
    public static string ToWireName(this StrategyKind strategy) => strategy switch
    {
        StrategyKind.Lww => "lww",
        StrategyKind.Fww => "fww",
        StrategyKind.AdditiveWins => "additive-wins",
        StrategyKind.GrowOnly => "grow-only",
        StrategyKind.AdditiveWinsLww => "additive-wins-lww",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown strategy."),
    };

    /// <summary>Parses a wire name back to its strategy.</summary>
    public static bool TryParse(string? name, out StrategyKind strategy)
    {
        switch (name)
        {
            case "lww":
                strategy = StrategyKind.Lww;
                return true;
            case "fww":
                strategy = StrategyKind.Fww;
                return true;
            case "additive-wins":
                strategy = StrategyKind.AdditiveWins;
                return true;
            case "grow-only":
                strategy = StrategyKind.GrowOnly;
                return true;
            case "additive-wins-lww":
                strategy = StrategyKind.AdditiveWinsLww;
                return true;
            default:
                strategy = default;
                return false;
        }
    }
}
