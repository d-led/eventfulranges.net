using System.Buffers;
using System.Collections.Immutable;
using System.Text.Json;

namespace EventfulRanges;

/// <summary>
/// Joins two metadata objects into one. An implementation must be commutative,
/// associative and idempotent — the contract of a CRDT join — so that the
/// metadata carried by merged ranges converges whatever order the merges
/// happen in.
/// </summary>
/// <param name="a">One metadata object, as raw JSON.</param>
/// <param name="b">The other metadata object, as raw JSON.</param>
/// <returns>The joined metadata object, as raw JSON.</returns>
public delegate ImmutableArray<byte> MetaMerge(ImmutableArray<byte> a, ImmutableArray<byte> b);

/// <summary>
/// The default metadata join, and the rules it follows.
/// </summary>
/// <remarks>
/// A key carried by only one side is kept, equal values collapse, and a
/// conflict resolves by joining two nested objects or, for scalars, by keeping
/// the value whose raw JSON sorts larger. Raw bytes decide, not the parsed
/// value, so <c>9</c> beats <c>10</c>. Both rules are order-independent, which
/// is what makes <see cref="Union"/> a CRDT join.
/// </remarks>
public static class Meta
{
    /// <summary>
    /// Joins two metadata objects key by key. Empty, invalid or non-object
    /// metadata counts as absent, so the other side survives untouched.
    /// </summary>
    public static ImmutableArray<byte> Union(ImmutableArray<byte> a, ImmutableArray<byte> b)
    {
        var left = AsObject(a);
        if (left is null)
        {
            return b;
        }

        var right = AsObject(b);
        if (right is null)
        {
            return a;
        }

        return Join(left, right);
    }

    /// <summary>Picks the value that survives for a key both sides carry.</summary>
    private static string Resolve(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return a;
        }

        var left = AsObject(a);
        var right = AsObject(b);
        if (left is not null && right is not null)
        {
            return Text(Join(left, right));
        }

        return string.CompareOrdinal(a, b) > 0 ? a : b;
    }

    private static ImmutableArray<byte> Join(
        Dictionary<string, string> a,
        Dictionary<string, string> b)
    {
        var members = new Dictionary<string, string>(a, StringComparer.Ordinal);
        foreach (var (key, value) in b)
        {
            members[key] = members.TryGetValue(key, out var existing)
                ? Resolve(existing, value)
                : value;
        }

        return Write(members);
    }

    /// <summary>Writes the members with their keys in ordinal order.</summary>
    private static ImmutableArray<byte> Write(Dictionary<string, string> members)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var key in members.Keys.Order(StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                writer.WriteRawValue(members[key], skipInputValidation: false);
            }

            writer.WriteEndObject();
        }

        return ImmutableArray.Create(buffer.WrittenSpan.ToArray());
    }

    /// <summary>
    /// The top-level members of <paramref name="raw"/>, or <c>null</c> when it
    /// is empty or is not a JSON object.
    /// </summary>
    private static Dictionary<string, string>? AsObject(ImmutableArray<byte> raw) =>
        raw.IsDefaultOrEmpty ? null : AsObject(Text(raw));

    private static Dictionary<string, string>? AsObject(string json)
    {
        if (json.Length == 0)
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var members = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var member in document.RootElement.EnumerateObject())
            {
                members[member.Name] = member.Value.GetRawText();
            }

            return members;
        }
    }

    private static string Text(ImmutableArray<byte> raw) => System.Text.Encoding.UTF8.GetString(raw.AsSpan());
}
