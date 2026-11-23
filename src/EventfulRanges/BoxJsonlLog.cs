using System.Text;
using System.Text.Json;

namespace EventfulRanges;

/// <summary>
/// Persists a box log as a single JSON Lines stream: one operation per line, so a
/// restart replays exactly what was accepted.
/// </summary>
/// <remarks>
/// Safe for concurrent callers in one process, not for several processes appending
/// at once — the same contract as the range stream, and for the same reason: the
/// version check catches a lost append inside the process, and nothing can catch
/// two processes racing for the same file.
/// <para>
/// Snapshots live in the same stream, as records a reader recognises and skips, so
/// compaction can rewrite the file in one pass: the newest snapshot, then the
/// operations it does not cover.
/// </para>
/// </remarks>
public sealed class BoxJsonlLog : IBoxEventStore, ICompactableBoxLog
{
    private const string StreamFileName = "boxes.stream.jsonl";

    private readonly Lock gate = new();

    private BoxJsonlLog(string filePath) => FilePath = filePath;

    /// <summary>The stream file this log reads and appends to.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Opens a log rooted at <paramref name="directory"/>, creating the directory
    /// when it does not exist. The stream lives in <c>boxes.stream.jsonl</c>.
    /// </summary>
    public static BoxJsonlLog Open(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);
        return new BoxJsonlLog(Path.Combine(directory, StreamFileName));
    }

    /// <inheritdoc/>
    public long Version
    {
        get
        {
            lock (gate)
            {
                return ReadAll().Count;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask AppendAsync(
        long expectedVersion,
        IReadOnlyList<BoxOp> ops,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ops);

        lock (gate)
        {
            var current = ReadAll().Count;
            if (current != expectedVersion)
            {
                throw new StoreConflictException(expectedVersion, current);
            }

            using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            foreach (var op in ops)
            {
                WriteOperation(stream, op);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<BoxLogReadResult> ReadAsync(
        long fromVersion,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var all = ReadAll();
            var start = (int)Math.Clamp(fromVersion, 0, all.Count);
            return ValueTask.FromResult(new BoxLogReadResult(all.GetRange(start, all.Count - start), all.Count));
        }
    }

    /// <inheritdoc/>
    public ValueTask SaveSnapshotAsync(byte[] data, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (gate)
        {
            using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            WriteSnapshotLine(stream, version, data);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<SnapshotReadResult> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            SnapshotReadResult? newest = null;
            foreach (var line in Lines())
            {
                if (SnapshotOf(line) is not { } marker || (newest is { } known && marker.Version < known.Version))
                {
                    continue;
                }

                newest = marker;
            }

            return newest is { } result ? ValueTask.FromResult(result) : throw new SnapshotNotFoundException();
        }
    }

    /// <inheritdoc/>
    public ValueTask CompactAsync(byte[] data, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (gate)
        {
            var rewriting = FilePath + ".rewrite";
            var tail = ReadAll().Skip((int)Math.Clamp(version, 0, Version));
            using (var stream = new FileStream(rewriting, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteSnapshotLine(stream, version, data);
                foreach (var op in tail)
                {
                    WriteOperation(stream, op);
                }
            }

            File.Move(rewriting, FilePath, overwrite: true);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Every operation in the stream, oldest first, ignoring snapshot records.</summary>
    private List<BoxOp> ReadAll()
    {
        var ops = new List<BoxOp>();
        foreach (var line in Lines())
        {
            if (SnapshotOf(line) is null)
            {
                ops.Add(JsonSerializer.Deserialize(line, JsonContext.Default.BoxOp)
                    ?? throw new InvalidDataException(FormattableString.Invariant($"Unreadable operation line: {line}")));
            }
        }

        return ops;
    }

    private IEnumerable<string> Lines() =>
        File.Exists(FilePath) ? File.ReadLines(FilePath).Where(line => line.Length > 0) : [];

    /// <summary>The snapshot record a line holds, or <c>null</c> when it is an operation.</summary>
    private static SnapshotReadResult? SnapshotOf(string line)
    {
        if (!line.Contains("\"snapshot\"", StringComparison.Ordinal))
        {
            return null;
        }

        using var document = JsonDocument.Parse(line);
        if (!document.RootElement.TryGetProperty("snapshot", out var marker)
            || !marker.TryGetProperty("version", out var version)
            || !marker.TryGetProperty("data", out var data))
        {
            throw new InvalidDataException(FormattableString.Invariant($"Unreadable snapshot line: {line}"));
        }

        return new SnapshotReadResult(JsonSerializer.SerializeToUtf8Bytes(data, JsonContext.Default.JsonElement), version.GetInt64());
    }

    private static void WriteOperation(Stream stream, BoxOp op)
    {
        JsonSerializer.Serialize(stream, op, JsonContext.Default.BoxOp);
        stream.WriteByte((byte)'\n');
    }

    private static void WriteSnapshotLine(Stream stream, long version, byte[] data)
    {
        stream.Write(Encoding.UTF8.GetBytes("{\"snapshot\":{\"version\":"));
        stream.Write(Encoding.UTF8.GetBytes(version.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        stream.Write(Encoding.UTF8.GetBytes(",\"data\":"));
        stream.Write(data);
        stream.Write(Encoding.UTF8.GetBytes("}}\n"));
    }
}
