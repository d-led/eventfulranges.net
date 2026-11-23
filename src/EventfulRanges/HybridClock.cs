namespace EventfulRanges;

/// <summary>
/// A hybrid logical clock: the wall clock, forced to be strictly increasing, and
/// never behind a timestamp it has observed. Two ticks in the same clock reading
/// differ by one nanosecond.
/// </summary>
/// <remarks>
/// Timestamps are nanoseconds since the Unix epoch, the unit the original
/// implementation uses. <see cref="TimeProvider"/> is injectable, so tests can
/// stop time.
/// </remarks>
public sealed class HybridClock : IClock
{
    private readonly TimeProvider timeProvider;
    private long last;

    /// <summary>Creates a clock reading the given time source.</summary>
    /// <param name="timeProvider">The time source; <see cref="TimeProvider.System"/> when omitted.</param>
    public HybridClock(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public long Tick()
    {
        while (true)
        {
            var previous = Interlocked.Read(ref last);
            var now = Now();
            var next = now <= previous ? previous + 1 : now;
            if (Interlocked.CompareExchange(ref last, next, previous) == previous)
            {
                return next;
            }
        }
    }

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

    private long Now() =>
        (timeProvider.GetUtcNow().UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
}
