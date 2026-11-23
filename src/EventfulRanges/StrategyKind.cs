namespace EventfulRanges;

/// <summary>
/// How concurrent operations resolve into one materialized set.
/// </summary>
public enum StrategyKind : byte
{
    /// <summary>The operation with the highest <c>(timestamp, id)</c> wins at each point.</summary>
    Lww = 0,

    /// <summary>The operation with the lowest <c>(timestamp, id)</c> wins at each point.</summary>
    Fww = 1,

    /// <summary>The union of every addition minus the union of every removal.</summary>
    AdditiveWins = 2,

    /// <summary>The union of every addition; removals are ignored.</summary>
    GrowOnly = 3,
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
            default:
                strategy = default;
                return false;
        }
    }
}
