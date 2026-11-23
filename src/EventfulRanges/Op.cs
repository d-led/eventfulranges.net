using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// An immutable range operation, the atomic unit of the event log: either the
/// interval joins the set or it leaves it.
/// </summary>
/// <param name="Id">Process-unique, roughly time-ordered identifier.</param>
/// <param name="Kind">Whether the operation adds or removes.</param>
/// <param name="Interval">The range the operation applies to.</param>
/// <param name="Timestamp">
/// The stamp used to order concurrent operations. Zero means "not stamped yet";
/// an engine stamps it when the operation is applied.
/// </param>
/// <param name="Origin">Who produced the operation, when the producer says so.</param>
public sealed record Op(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] OpKind Kind,
    [property: JsonPropertyName("interval")] Interval Interval,
    [property: JsonPropertyName("ts")] long Timestamp = 0,
    [property: JsonPropertyName("origin")] string? Origin = null)
{
    /// <summary>Reports the first reason the operation is not well-formed, if any.</summary>
    public OpError Validate()
    {
        if (string.IsNullOrEmpty(Id))
        {
            return OpError.MissingId;
        }

        if (Kind is not (OpKind.Add or OpKind.Remove))
        {
            return OpError.UnknownKind;
        }

        return Interval.IsValid ? OpError.None : OpError.InvalidInterval;
    }

    /// <summary>Throws when the operation is not well-formed.</summary>
    /// <exception cref="InvalidOpException">The operation failed validation.</exception>
    public void EnsureValid()
    {
        var error = Validate();
        if (error != OpError.None)
        {
            throw new InvalidOpException(this, error);
        }
    }

    /// <summary>Creates an operation of the given kind with a freshly generated identifier.</summary>
    public static Op Create(OpKind kind, Interval interval) => new(NewId(), kind, interval);

    /// <summary>Creates an addition over <c>[start, end]</c>.</summary>
    public static Op Add(double start, double end) => Create(OpKind.Add, new Interval(start, end));

    /// <summary>Creates an addition with explicit inclusivity for each endpoint.</summary>
    public static Op Add(double start, double end, Bound startBound, Bound endBound) =>
        Create(OpKind.Add, new Interval(start, end, startBound, endBound));

    /// <summary>Creates a removal over <c>[start, end]</c>.</summary>
    public static Op Remove(double start, double end) => Create(OpKind.Remove, new Interval(start, end));

    /// <summary>Creates a removal with explicit inclusivity for each endpoint.</summary>
    public static Op Remove(double start, double end, Bound startBound, Bound endBound) =>
        Create(OpKind.Remove, new Interval(start, end, startBound, endBound));

    /// <summary>Renders the operation as <c>add [1,5]</c>.</summary>
    public override string ToString() => FormattableString.Invariant($"{Kind.ToWireName()} {Interval}");

    /// <summary>
    /// Process-unique, roughly time-ordered: nanoseconds since the Unix epoch in
    /// base 36, then six random bytes. The shape matches the original
    /// implementation's identifiers.
    /// </summary>
    private static string NewId()
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
