namespace EventfulRanges;

/// <summary>
/// An append-only, ordered stream of operations — the smallest persistence
/// contract. A stream-only backend (a log service, a queue) needs only this.
/// </summary>
/// <remarks>
/// Implementations are expected to be safe for concurrent callers within one
/// process and to reject a stale expected version with
/// <see cref="StoreConflictException"/> rather than losing an append.
/// </remarks>
public interface IEventLog
{
    /// <summary>
    /// Appends the operations atomically, provided the log still holds exactly
    /// <paramref name="expectedVersion"/> operations.
    /// </summary>
    /// <exception cref="StoreConflictException">
    /// Another writer appended first; read the missing suffix and retry.
    /// </exception>
    ValueTask AppendAsync(
        long expectedVersion,
        IReadOnlyList<Op> ops,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the operations after <paramref name="fromVersion"/>, together with
    /// the total number of operations in the log.
    /// </summary>
    ValueTask<LogReadResult> ReadAsync(long fromVersion, CancellationToken cancellationToken = default);
}

/// <summary>
/// The operations read from a log and the log's total operation count.
/// </summary>
/// <param name="Ops">The operations after the requested version, in log order.</param>
/// <param name="Version">The number of operations the log holds.</param>
public readonly record struct LogReadResult(IReadOnlyList<Op> Ops, long Version);

/// <summary>
/// Stores and loads a materialized snapshot. A log may optionally implement it;
/// backends that do not simply skip snapshotting.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>Stores a snapshot at the given log version.</summary>
    ValueTask SaveSnapshotAsync(
        byte[] data,
        long version,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the latest snapshot.</summary>
    /// <exception cref="SnapshotNotFoundException">No snapshot has been stored.</exception>
    ValueTask<SnapshotReadResult> LoadSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A snapshot's payload and the log version it covers.
/// </summary>
/// <param name="Data">The serialized snapshot.</param>
/// <param name="Version">The log version the snapshot covers.</param>
public readonly record struct SnapshotReadResult(byte[] Data, long Version);

/// <summary>
/// Rewrites the log so a snapshot replaces every operation it covers, shrinking
/// the stream. A log may optionally implement it.
/// </summary>
public interface ICompactableLog
{
    /// <summary>Rewrites the log as the snapshot followed by the operations after its version.</summary>
    ValueTask CompactAsync(
        byte[] snapshot,
        long version,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The combined contract: an append-only log that can also snapshot.
/// </summary>
public interface IEventStore : IEventLog, ISnapshotStore;

/// <summary>
/// Thrown when an append observed a stale expected version: another writer
/// appended first.
/// </summary>
public sealed class StoreConflictException : Exception
{
    /// <summary>Creates the exception for an append that lost the race.</summary>
    public StoreConflictException(long expectedVersion, long actualVersion)
        : base(FormattableString.Invariant(
            $"Store version conflict: expected {expectedVersion}, found {actualVersion}."))
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>The version the caller required.</summary>
    public long ExpectedVersion { get; }

    /// <summary>The version the log actually held.</summary>
    public long ActualVersion { get; }
}

/// <summary>
/// Thrown when a store holds no snapshot.
/// </summary>
public sealed class SnapshotNotFoundException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SnapshotNotFoundException()
        : base("Store holds no snapshot.")
    {
    }
}
