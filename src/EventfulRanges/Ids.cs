using System.Security.Cryptography;

namespace EventfulRanges;

/// <summary>
/// The identifiers an operation carries.
/// </summary>
internal static class Ids
{
    /// <summary>
    /// Process-unique, roughly time-ordered: nanoseconds since the Unix epoch in
    /// base 36, then six random bytes. The shape matches the original
    /// implementation's identifiers.
    /// </summary>
    public static string New()
    {
        Span<byte> random = stackalloc byte[6];
        RandomNumberGenerator.Fill(random);
        var nanoseconds = (DateTimeOffset.UtcNow.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
        return FormattableString.Invariant($"{ToBase36(nanoseconds)}-{Convert.ToHexStringLower(random)}");
    }

    /// <summary>Formats a non-negative number in base 36, lower case.</summary>
    private static string ToBase36(long value)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0)
        {
            return "0";
        }

        Span<char> buffer = stackalloc char[13];
        var position = buffer.Length;
        while (value > 0)
        {
            buffer[--position] = digits[(int)(value % 36)];
            value /= 36;
        }

        return new string(buffer[position..]);
    }
}
