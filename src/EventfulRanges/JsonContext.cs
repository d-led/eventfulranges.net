using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventfulRanges;

/// <summary>
/// The source-generated serializer for everything that reaches a store. Source
/// generation is what keeps the package trimming- and AOT-safe: no serializer
/// is built by reflection at run time.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Op))]
[JsonSerializable(typeof(Box))]
[JsonSerializable(typeof(BoxOp))]
[JsonSerializable(typeof(SnapshotDocument))]
[JsonSerializable(typeof(SnapshotMarker))]
[JsonSerializable(typeof(SnapshotLine))]
internal sealed partial class JsonContext : JsonSerializerContext;

/// <summary>
/// The state an engine persists: enough to rebuild the view without replaying
/// the operations a snapshot covers.
/// </summary>
/// <param name="Strategy">The strategy the snapshot was built with.</param>
/// <param name="Version">The log version the snapshot covers.</param>
/// <param name="Intervals">The materialized view at that version.</param>
/// <param name="Ops">Every known operation, so anti-entropy stays complete.</param>
public sealed record SnapshotDocument(
    [property: JsonPropertyName("strategy")] StrategyKind Strategy,
    [property: JsonPropertyName("version")] long Version,
    [property: JsonPropertyName("intervals")] IReadOnlyList<Interval> Intervals,
    [property: JsonPropertyName("ops")] IReadOnlyList<Op> Ops);

/// <summary>One snapshot record in the stream, wrapped so a reader can recognise it.</summary>
internal sealed record SnapshotLine(
    [property: JsonPropertyName("snapshot")] SnapshotMarker Snapshot);

/// <summary>The log version a snapshot covers and its opaque payload.</summary>
internal sealed record SnapshotMarker(
    [property: JsonPropertyName("version")] long Version,
    [property: JsonPropertyName("data")] JsonElement Data);
