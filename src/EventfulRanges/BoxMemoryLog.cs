namespace EventfulRanges;

/// <summary>
/// A box log that lives in memory. Nothing is persisted, so a restart loses it.
/// </summary>
public sealed class BoxMemoryLog : IBoxEventStore, ICompactableBoxLog
{
    private readonly Lock gate = new();
    private readonly List<BoxOp> entries = [];
    private byte[]? snapshot;
    private long snapshotVersion;

    /// <summary>Creates a log, optionally holding operations already known.</summary>
    public BoxMemoryLog(IEnumerable<BoxOp>? initial = null)
    {
        if (initial is not null)
        {
            entries.AddRange(initial);
        }
    }

    /// <inheritdoc/>
    public long Version
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
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
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (expectedVersion != entries.Count)
            {
                throw new StoreConflictException(expectedVersion, entries.Count);
            }

            entries.AddRange(ops);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<BoxLogReadResult> ReadAsync(
        long fromVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var start = (int)Math.Clamp(fromVersion, 0, entries.Count);
            return ValueTask.FromResult(new BoxLogReadResult(entries.GetRange(start, entries.Count - start), entries.Count));
        }
    }

    /// <inheritdoc/>
    public ValueTask SaveSnapshotAsync(byte[] data, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (gate)
        {
            snapshot = data;
            snapshotVersion = version;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<SnapshotReadResult> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return snapshot is null
                ? throw new SnapshotNotFoundException()
                : ValueTask.FromResult(new SnapshotReadResult(snapshot, snapshotVersion));
        }
    }

    /// <inheritdoc/>
    public ValueTask CompactAsync(byte[] data, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (gate)
        {
            snapshot = data;
            snapshotVersion = version;
            entries.RemoveRange(0, (int)Math.Clamp(version, 0, entries.Count));
        }

        return ValueTask.CompletedTask;
    }
}
