using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// Reads and writes a <see cref="Box"/> in the shape the original implementation
/// uses: the two corners, the faces only where they differ from the half-open
/// default, and the metadata as raw JSON.
/// </summary>
/// <remarks>
/// A half-open box therefore stays in its compact form — no <c>minBound</c>, no
/// <c>maxBound</c>, no <c>meta</c> — so the common case writes nothing it does not
/// need to. The converter is hand-written rather than reflected because the
/// metadata is embedded verbatim: it must reach the log exactly as the writer
/// produced it.
/// </remarks>
internal sealed class BoxJsonConverter : JsonConverter<Box>
{
    public override Box Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new Box(
                Coordinates(root, "min"),
                Coordinates(root, "max"),
                Bounds(root, "minBound"),
                Bounds(root, "maxBound"))
            .WithMeta(Metadata(root));
    }

    public override void Write(Utf8JsonWriter writer, Box value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        WriteCoordinates(writer, "min", value.Min);
        WriteCoordinates(writer, "max", value.Max);
        WriteBounds(writer, "minBound", value.MinBound);
        WriteBounds(writer, "maxBound", value.MaxBound);
        WriteMetadata(writer, value.Meta);
        writer.WriteEndObject();
    }

    private static List<double> Coordinates(JsonElement root, string name) =>
        root.TryGetProperty(name, out var corners)
            ? corners.EnumerateArray().Select(coordinate => coordinate.GetDouble()).ToList()
            : [];

    private static ImmutableArray<Bound> Bounds(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var faces))
        {
            return [];
        }

        return [.. faces.EnumerateArray().Select(face => ParseBound(face.GetString()))];
    }

    private static Bound ParseBound(string? name) =>
        BoundNames.TryParse(name, out var bound)
            ? bound
            : throw new JsonException(FormattableString.Invariant($"Unknown face inclusivity {name}."));

    private static ImmutableArray<byte> Metadata(JsonElement root) =>
        root.TryGetProperty("meta", out var meta)
            ? ImmutableArray.Create(Encoding.UTF8.GetBytes(meta.GetRawText()))
            : [];

    private static void WriteCoordinates(Utf8JsonWriter writer, string name, ImmutableArray<double> corners)
    {
        writer.WriteStartArray(name);
        foreach (var coordinate in corners)
        {
            writer.WriteNumberValue(coordinate);
        }

        writer.WriteEndArray();
    }

    private static void WriteBounds(Utf8JsonWriter writer, string name, ImmutableArray<Bound> faces)
    {
        if (faces.IsDefaultOrEmpty)
        {
            return;
        }

        writer.WriteStartArray(name);
        foreach (var face in faces)
        {
            writer.WriteStringValue(face.ToWireName());
        }

        writer.WriteEndArray();
    }

    private static void WriteMetadata(Utf8JsonWriter writer, ImmutableArray<byte> meta)
    {
        if (meta.IsDefaultOrEmpty)
        {
            return;
        }

        writer.WritePropertyName("meta");
        writer.WriteRawValue(meta.AsSpan(), skipInputValidation: false);
    }
}
