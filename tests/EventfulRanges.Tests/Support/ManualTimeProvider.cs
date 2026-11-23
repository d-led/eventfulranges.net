namespace EventfulRanges.Tests;

/// <summary>
/// A time source that moves only when a test moves it, so hybrid-clock tests do
/// not race the wall clock.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset now;

    public ManualTimeProvider(DateTimeOffset start) => now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
