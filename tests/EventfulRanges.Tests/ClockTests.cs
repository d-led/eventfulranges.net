using Shouldly;

namespace EventfulRanges.Tests;

public sealed class ClockTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch.AddSeconds(1);

    [Fact]
    public void The_hybrid_clock_starts_at_the_wall_clock()
    {
        var clock = new HybridClock(new ManualTimeProvider(Start));

        clock.Tick().ShouldBe(1_000_000_000);
    }

    [Fact]
    public void The_hybrid_clock_stays_strictly_increasing_while_time_stands_still()
    {
        var clock = new HybridClock(new ManualTimeProvider(Start));

        var first = clock.Tick();
        var second = clock.Tick();

        second.ShouldBe(first + 1);
    }

    [Fact]
    public void The_hybrid_clock_moves_past_a_timestamp_it_has_observed()
    {
        var clock = new HybridClock(new ManualTimeProvider(Start));

        clock.Observe(5_000_000_000);

        clock.Tick().ShouldBeGreaterThan(5_000_000_000);
    }

    [Fact]
    public void The_hybrid_clock_follows_time_forward()
    {
        var time = new ManualTimeProvider(Start);
        var clock = new HybridClock(time);

        var first = clock.Tick();
        time.Advance(TimeSpan.FromSeconds(1));
        var second = clock.Tick();

        second.ShouldBe(first + 1_000_000_000);
    }

    [Fact]
    public void The_lamport_clock_counts_from_one()
    {
        var clock = new LamportClock();

        clock.Tick().ShouldBe(1);
        clock.Tick().ShouldBe(2);
    }

    [Fact]
    public void The_lamport_clock_moves_past_a_timestamp_it_has_observed()
    {
        var clock = new LamportClock();

        clock.Observe(10);

        clock.Tick().ShouldBe(11);
    }

    [Fact]
    public void Observing_an_older_timestamp_does_not_move_the_clock_back()
    {
        var clock = new LamportClock();
        clock.Observe(10);

        clock.Observe(3);

        clock.Tick().ShouldBe(11);
    }
}
