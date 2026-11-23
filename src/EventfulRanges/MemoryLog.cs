namespace EventfulRanges;

/// <summary>
/// An in-memory event log: for tests and single-process use. It implements the
/// whole store surface, snapshots and compaction included.
/// </summary>
public sealed class MemoryLog : IEventStore, ICompactableLog
{
    private readonly Lock gate = new();
    private readonly List<Op> entries = new();
    private byte[]? snapshot;
    private long snapshotVersion;

    /// <summary>Creates an empty log, optionally seeded with operations.</summary>
    public MemoryLog(IEnumerable<Op>? initial = null)
    {
        if (initial is not null)
        {
            entries.AddRange(initial);
        }
    }

    /// <summary>The number of operations the log holds.</summary>
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

    /// <inheritdoc />
    public ValueTask AppendAsync(
        long expectedVersion,
        IReadOnlyList<Op> ops,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ops);

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

    /// <inheritdoc />
    public ValueTask<LogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var count = (int)Math.Min(Math.Max(entries.Count - fromVersion, 0), entries.Count);
            var tail = new List<Op>(count);
            for (var index = (int)Math.Max(fromVersion, 0); index < entries.Count; index++)
            {
                tail.Add(entries[index]);
            }

            return ValueTask.FromResult(new LogReadResult(tail, entries.Count));
        }
    }

    /// <inheritdoc />
    public ValueTask SaveSnapshotAsync(byte[] data, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (gate)
        {
            snapshot = data.ToArray();
            snapshotVersion = version;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<SnapshotReadResult> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (snapshot is null)
            {
                throw new SnapshotNotFoundException();
            }

            return ValueTask.FromResult(new SnapshotReadResult(snapshot, snapshotVersion));
        }
    }

    /// <inheritdoc />
    public ValueTask CompactAsync(byte[] snapshot, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (gate)
        {
            var tail = entries.Skip((int)Math.Min(Math.Max(version, 0), entries.Count)).ToList();
            entries.Clear();
            entries.AddRange(tail);
            this.snapshot = snapshot.ToArray();
            snapshotVersion = version;
        }

        return ValueTask.CompletedTask;
    }
}
