namespace EventfulRanges;

/// <summary>
/// An append-only, versioned stream of box operations.
/// </summary>
/// <remarks>
/// A store is the truth: an engine reads it back rather than keeping state it
/// cannot rebuild. <see cref="AppendAsync"/> takes the version the caller believes
/// it is appending to, so two writers cannot both win — the loser gets a
/// <see cref="StoreConflictException"/> and can read what it missed.
/// </remarks>
public interface IBoxLog
{
    /// <summary>The number of operations the log holds.</summary>
    long Version { get; }

    /// <summary>Appends operations at the expected version, or throws.</summary>
    /// <exception cref="StoreConflictException">The log has moved on.</exception>
    ValueTask AppendAsync(
        long expectedVersion,
        IReadOnlyList<BoxOp> ops,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the operations after <paramref name="fromVersion"/>.</summary>
    ValueTask<BoxLogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a read of a box log returns: the operations after the version asked for,
/// and the log's version after them.
/// </summary>
/// <param name="Ops">The operations that follow the requested version.</param>
/// <param name="Version">The log's total number of operations.</param>
public readonly record struct BoxLogReadResult(IReadOnlyList<BoxOp> Ops, long Version);
