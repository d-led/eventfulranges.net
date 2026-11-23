namespace EventfulRanges;

/// <summary>
/// The options an <see cref="Engine"/> is opened with.
/// </summary>
public sealed record EngineOptions
{
    /// <summary>
    /// The clock used to stamp new operations. Defaults to a
    /// <see cref="HybridClock"/> over the system time; inject a
    /// <see cref="LamportClock"/> or a clock over a fake time source in tests.
    /// </summary>
    public IClock Clock { get; init; } = new HybridClock();

    /// <summary>
    /// Snapshots the view every this many new operations; 0 disables automatic
    /// snapshots. Defaults to 100, as in the original implementation.
    /// </summary>
    public int SnapshotEvery { get; init; } = 100;
}
