using Shouldly;

namespace EventfulRanges.Tests;

public sealed class SweepTests
{
    [Fact]
    public void Sweep_labels_each_piece_with_the_highest_priority_cover()
    {
        var intervals = new[]
        {
            new Interval(1, 9),
            new Interval(3, 5),
        };

        var covers = Intervals.Sweep(intervals, (left, right) => left > right);

        var expectedPieces = new[] { "[1,3)", "[3,5]", "(5,9]" };
        var expectedWinners = new[] { 0, 1, 0 };
        covers.Select(cover => cover.Interval.ToString()).ShouldBe(expectedPieces);
        covers.Select(cover => cover.Index).ShouldBe(expectedWinners);
    }

    [Fact]
    public void Sweep_omits_the_gaps_no_interval_covers()
    {
        var intervals = new[] { new Interval(1, 2), new Interval(4, 5) };

        var covers = Intervals.Sweep(intervals, (left, right) => left < right);

        var expectedPieces = new[] { "[1,2]", "[4,5]" };
        covers.Select(cover => cover.Interval.ToString()).ShouldBe(expectedPieces);
    }

    [Fact]
    public void Sweep_joins_adjacent_pieces_of_the_same_winner_into_one_run()
    {
        var intervals = new[] { new Interval(1, 9), new Interval(3, 5) };

        var covers = Intervals.Sweep(intervals, (left, right) => left < right);

        covers.ShouldHaveSingleItem().Interval.ShouldBe(new Interval(1, 9));
    }

    [Fact]
    public void Sweep_of_nothing_is_nothing()
    {
        Intervals.Sweep(Array.Empty<Interval>(), (left, right) => left < right).ShouldBeEmpty();
    }
}
