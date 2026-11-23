namespace EventfulRanges;

/// <summary>
/// A pure logical clock starting at zero: each tick is one more than the last,
/// and observing a timestamp moves the clock past it. Use it when tests need
/// timestamps that do not depend on the wall clock.
/// </summary>
public sealed class LamportClock : IClock
{
    private long last;

    /// <inheritdoc />
    public long Tick() => Interlocked.Increment(ref last);

    /// <inheritdoc />
    public void Observe(long timestamp)
    {
        while (true)
        {
            var previous = Interlocked.Read(ref last);
            if (timestamp <= previous)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref last, timestamp, previous) == previous)
            {
                return;
            }
        }
    }
}
