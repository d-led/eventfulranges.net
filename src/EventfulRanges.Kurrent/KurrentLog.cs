using System.Text.Json;
using KurrentDB.Client;

namespace EventfulRanges.Kurrent;

/// <summary>
/// A KurrentDB-backed event log: a replica's operations as one stream, and its
/// snapshots as another.
/// </summary>
/// <remarks>
/// The stream is the log. Its length is the version a caller expects, and
/// KurrentDB's own optimistic concurrency — an append at a stale revision — is
/// what becomes <see cref="StoreConflictException"/>, so two replicas writing
/// the same stream cannot both win.
/// <para>
/// Snapshots are a second stream appended to without a revision requirement,
/// read back from the end: the newest one wins and the older ones stay as
/// history. Nothing here compacts, because a stream cannot lose its prefix;
/// <see cref="Engine.CompactAsync"/> therefore reports that this log cannot
/// compact, exactly as the interface documents.
/// </para>
/// </remarks>
public sealed class KurrentLog : IEventStore, IDisposable
{
    /// <summary>The event type every operation is written with.</summary>
    public const string OperationEventType = "range-op";

    /// <summary>The event type every snapshot is written with.</summary>
    public const string SnapshotEventType = "snapshot";

    private const string JsonContentType = "application/json";

    private readonly KurrentDBClient client;
    private readonly string snapshotStream;

    private KurrentLog(KurrentDBClient client, string operationStream, string snapshotStream)
    {
        this.client = client;
        this.snapshotStream = snapshotStream;
        OperationStream = operationStream;
    }

    /// <summary>The stream the operations are written to.</summary>
    public string OperationStream { get; }

    /// <summary>The stream the snapshots are written to.</summary>
    public string SnapshotStream => snapshotStream;

    /// <summary>
    /// Connects to KurrentDB, for example at
    /// <c>esdb://localhost:2113?tls=false</c>.
    /// </summary>
    /// <param name="connectionString">The KurrentDB connection string.</param>
    /// <param name="operationStream">The stream to keep operations in.</param>
    /// <param name="snapshotStream">The stream to keep snapshots in.</param>
    public static KurrentLog Open(
        string connectionString,
        string operationStream = "eventfulranges-ops",
        string snapshotStream = "eventfulranges-snapshot")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotStream);

        var settings = KurrentDBClientSettings.Create(connectionString);
        return new KurrentLog(new KurrentDBClient(settings), operationStream, snapshotStream);
    }

    /// <summary>Appends the operations at the expected log length.</summary>
    /// <exception cref="StoreConflictException">Another writer appended first.</exception>
    public async ValueTask AppendAsync(
        long expectedVersion,
        IReadOnlyList<Op> ops,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ops);
        if (ops.Count == 0)
        {
            return;
        }

        var events = ops.Select(OperationEvent).ToList();
        try
        {
            await client.AppendToStreamAsync(
                OperationStream,
                ExpectedStreamState(expectedVersion),
                events,
                cancellationToken: cancellationToken);
        }
        catch (WrongExpectedVersionException)
        {
            throw new StoreConflictException(expectedVersion, await CurrentVersionAsync(cancellationToken));
        }
    }

    /// <summary>
    /// Reads the operations after the given version. A stream that does not
    /// exist yet is an empty log, not a failure.
    /// </summary>
    public async ValueTask<LogReadResult> ReadAsync(
        long fromVersion,
        CancellationToken cancellationToken = default)
    {
        var ops = new List<Op>();
        var version = fromVersion;
        try
        {
            await using var read = client.ReadStreamAsync(
                Direction.Forwards,
                OperationStream,
                StreamPosition.FromInt64(Math.Max(fromVersion, 0)),
                long.MaxValue,
                cancellationToken: cancellationToken);

            await foreach (var resolved in read)
            {
                ops.Add(Decode(resolved.OriginalEvent));
                version = (long)resolved.OriginalEvent.EventNumber.ToUInt64() + 1;
            }
        }
        catch (StreamNotFoundException)
        {
            return new LogReadResult([], 0);
        }

        // Nothing after the requested version may mean the stream ends before
        // it, and the caller still needs the log's real length.
        return ops.Count == 0 && fromVersion > 0
            ? new LogReadResult([], await CurrentVersionAsync(cancellationToken))
            : new LogReadResult(ops, version);
    }

    /// <summary>Stores a snapshot at the given log version.</summary>
    public async ValueTask SaveSnapshotAsync(
        byte[] data,
        long version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var metadata = JsonSerializer.SerializeToUtf8Bytes(new SnapshotMetadata(version));
        await client.AppendToStreamAsync(
            snapshotStream,
            StreamState.Any,
            [new EventData(Uuid.NewUuid(), SnapshotEventType, data, metadata, JsonContentType)],
            cancellationToken: cancellationToken);
    }

    /// <summary>Returns the latest snapshot.</summary>
    /// <exception cref="SnapshotNotFoundException">No snapshot has been stored.</exception>
    public async ValueTask<SnapshotReadResult> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var read = client.ReadStreamAsync(
                Direction.Backwards,
                snapshotStream,
                StreamPosition.End,
                1,
                cancellationToken: cancellationToken);

            await foreach (var resolved in read)
            {
                return SnapshotOf(resolved.OriginalEvent);
            }
        }
        catch (StreamNotFoundException)
        {
            // Fall through: a snapshot stream that was never written holds none.
        }

        throw new SnapshotNotFoundException();
    }

    /// <summary>Releases the connection.</summary>
    public void Dispose() => client.Dispose();

    private static EventData OperationEvent(Op op) =>
        new(Uuid.NewUuid(), OperationEventType, JsonSerializer.SerializeToUtf8Bytes(op, JsonContext.Default.Op), null, JsonContentType);

    private static Op Decode(EventRecord record) =>
        JsonSerializer.Deserialize(record.Data.Span, JsonContext.Default.Op)
        ?? throw new JsonException("The stream holds an operation that decoded to null.");

    private static SnapshotReadResult SnapshotOf(EventRecord record)
    {
        var metadata = JsonSerializer.Deserialize<SnapshotMetadata>(record.Metadata.Span)
            ?? throw new JsonException("The snapshot carries no version.");

        return new SnapshotReadResult(record.Data.ToArray(), metadata.Version);
    }

    /// <summary>The length of the log, read from its last event.</summary>
    private async ValueTask<long> CurrentVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var read = client.ReadStreamAsync(
                Direction.Backwards,
                OperationStream,
                StreamPosition.End,
                1,
                cancellationToken: cancellationToken);

            await foreach (var resolved in read)
            {
                return (long)resolved.OriginalEvent.EventNumber.ToUInt64() + 1;
            }
        }
        catch (StreamNotFoundException)
        {
            // No stream is an empty log.
        }

        return 0;
    }

    /// <summary>
    /// KurrentDB revisions are zero-based, so a log length of n means the append
    /// expects the stream to end at n - 1; a length of zero expects no stream.
    /// </summary>
    private static StreamState ExpectedStreamState(long version) =>
        version <= 0 ? StreamState.NoStream : StreamState.StreamRevision((ulong)(version - 1));

    /// <summary>The metadata stored with a snapshot event.</summary>
    private sealed record SnapshotMetadata(long Version);
}
