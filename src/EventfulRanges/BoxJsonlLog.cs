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
/// There is no compaction here yet. The range stream embeds snapshots in the same
/// file and rewrites it atomically; a box log only needs to be shorter than that
/// once the n-dimensional engine snapshots, which it does not do yet.
/// </para>
/// </remarks>
public sealed class BoxJsonlLog : IBoxLog
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
                JsonSerializer.Serialize(stream, op, JsonContext.Default.BoxOp);
                stream.WriteByte((byte)'\n');
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

    /// <summary>Every operation in the stream, oldest first.</summary>
    private List<BoxOp> ReadAll()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var ops = new List<BoxOp>();
        foreach (var line in File.ReadLines(FilePath))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var op = JsonSerializer.Deserialize(line, JsonContext.Default.BoxOp)
                ?? throw new InvalidDataException(FormattableString.Invariant($"Unreadable operation line: {line}"));
            ops.Add(op);
        }

        return ops;
    }
}
