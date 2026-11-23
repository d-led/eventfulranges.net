namespace EventfulRanges;

/// <summary>
/// Assigns timestamps to new operations and observes the ones that arrive from
/// other replicas, so an operation stamped locally sorts after everything the
/// replica has already seen.
/// </summary>
public interface IClock
{
    /// <summary>Returns a strictly increasing timestamp for a new local operation.</summary>
    long Tick();

    /// <summary>Advances the clock past a timestamp observed on another replica.</summary>
    void Observe(long timestamp);
}
