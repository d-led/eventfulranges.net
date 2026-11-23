using System.Text.Json;

namespace EventfulRanges;

/// <summary>
/// Persists the log as a single JSON Lines stream: one operation per line, and
/// snapshots embedded as records in the same file, so the stream can be
/// compacted in place.
/// </summary>
/// <remarks>
/// A log instance is safe for concurrent callers in one process. It is not safe
/// for several processes appending at once; the version check catches a lost
/// append within one process, and the atomic rewrite keeps compaction from
/// leaving a half-written file.
/// </remarks>
public sealed class JsonlLog : IEventStore, ICompactableLog
{
    private const string StreamFileName = "ranges.stream.jsonl";

    private readonly Lock gate = new();

    private JsonlLog(string filePath) => FilePath = filePath;

    /// <summary>The stream file this log reads and appends to.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Opens a log rooted at <paramref name="directory"/>, creating the
    /// directory when it does not exist. The stream lives in
    /// <c>ranges.stream.jsonl</c>.
    /// </summary>
    public static JsonlLog Open(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);
        return new JsonlLog(Path.Combine(directory, StreamFileName));
    }

    /// <inheritdoc />
    public ValueTask AppendAsync(
        long expectedVersion,
        IReadOnlyList<Op> ops,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ops);

        lock (gate)
        {
            var current = ReadUnlocked(0).Version;
            if (current != expectedVersion)
            {
                throw new StoreConflictException(expectedVersion, current);
            }

            using var stream = OpenAppend();
            foreach (var op in ops)
            {
                WriteLine(stream, JsonSerializer.SerializeToUtf8Bytes(op, JsonContext.Default.Op));
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<LogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return ValueTask.FromResult(ReadUnlocked(fromVersion));
        }
    }

    /// <inheritdoc />
    public ValueTask SaveSnapshotAsync(byte[] data, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (gate)
        {
            using var stream = OpenAppend();
            WriteLine(stream, SnapshotLineBytes(version, data));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<SnapshotReadResult> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            SnapshotReadResult? latest = null;
            foreach (var line in Lines())
            {
                var parsed = ParseLine(line);
                if (parsed.Snapshot is { } marker && (latest is null || marker.Version >= latest.Value.Version))
                {
                    latest = new SnapshotReadResult(JsonSerializer.SerializeToUtf8Bytes(marker.Data, JsonContext.Default.JsonElement), marker.Version);
                }
            }

            if (latest is null)
            {
                throw new SnapshotNotFoundException();
            }

            return ValueTask.FromResult(latest.Value);
        }
    }

    /// <inheritdoc />
    public ValueTask CompactAsync(byte[] snapshot, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (gate)
        {
            var tail = ReadUnlocked(version).Ops;
            var temporary = FilePath + ".tmp";

            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteLine(stream, SnapshotLineBytes(version, snapshot));
                foreach (var op in tail)
                {
                    WriteLine(stream, JsonSerializer.SerializeToUtf8Bytes(op, JsonContext.Default.Op));
                }
            }

            File.Move(temporary, FilePath, overwrite: true);
        }

        return ValueTask.CompletedTask;
    }

    private LogReadResult ReadUnlocked(long fromVersion)
    {
        var tail = new List<Op>();
        long version = 0;

        foreach (var line in Lines())
        {
            var parsed = ParseLine(line);
            if (parsed.Op is not { } op)
            {
                continue;
            }

            if (version >= fromVersion)
            {
                tail.Add(op);
            }

            version++;
        }

        return new LogReadResult(tail, version);
    }

    private FileStream OpenAppend() =>
        new(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);

    private IEnumerable<string> Lines() =>
        File.Exists(FilePath) ? File.ReadLines(FilePath) : Array.Empty<string>();

    private static void WriteLine(FileStream stream, byte[] payload)
    {
        stream.Write(payload);
        stream.WriteByte((byte)'\n');
    }

    private static byte[] SnapshotLineBytes(long version, byte[] data)
    {
        using var document = JsonDocument.Parse(data);
        var line = new SnapshotLine(new SnapshotMarker(version, document.RootElement.Clone()));
        return JsonSerializer.SerializeToUtf8Bytes(line, JsonContext.Default.SnapshotLine);
    }

    private static StreamLine ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return default;
        }

        using var document = JsonDocument.Parse(line);
        if (document.RootElement.TryGetProperty("snapshot", out var marker))
        {
            return new StreamLine(null, marker.Deserialize(JsonContext.Default.SnapshotMarker));
        }

        return new StreamLine(JsonSerializer.Deserialize(line, JsonContext.Default.Op), null);
    }

    private readonly record struct StreamLine(Op? Op, SnapshotMarker? Snapshot);
}
