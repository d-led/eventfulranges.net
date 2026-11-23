namespace EventfulRanges;

/// <summary>
/// FNV-1a over the bits of the values that decide identity, used by the merge
/// indexes to bucket boxes.
/// </summary>
internal static class Hashing
{
    /// <summary>The FNV-1a starting value.</summary>
    public const ulong OffsetBasis = 14695981039346656037;

    private const ulong Prime = 1099511628211;

    /// <summary>Mixes one value into the running hash.</summary>
    public static ulong Mix(ulong hash, ulong value) => (hash ^ value) * Prime;

    /// <summary>
    /// Mixes in a separator, so that neighbouring values cannot run together:
    /// without it the pairs [1, 2] and [12] would hash alike.
    /// </summary>
    public static ulong Separate(ulong hash) => Mix(hash, Prime);

    /// <summary>
    /// The bit pattern of a number with every zero normalized to positive zero,
    /// because -0 and 0 compare equal but do not share a bit pattern.
    /// </summary>
    public static ulong CanonicalBits(double value) =>
        value == 0 ? 0 : BitConverter.DoubleToUInt64Bits(value);

    /// <inheritdoc cref="CanonicalBits(double)"/>
    public static ulong CanonicalBits(int value) => CanonicalBits((double)value);
}
