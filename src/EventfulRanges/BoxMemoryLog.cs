namespace EventfulRanges;

/// <summary>
/// A box log that lives in memory. Nothing is persisted, so a restart loses it.
/// </summary>
public sealed class BoxMemoryLog : IBoxLog
{
    private readonly Lock gate = new();
    private readonly List<BoxOp> entries = [];

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
}
