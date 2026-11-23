using Shouldly;

namespace EventfulRanges.Tests;

public sealed class StrategiesTests
{
    private static Op Add(string id, long timestamp, double start, double end) =>
        Op.Add(start, end) with { Id = id, Timestamp = timestamp };

    private static Op Remove(string id, long timestamp, double start, double end) =>
        Op.Remove(start, end) with { Id = id, Timestamp = timestamp };

    private static readonly StrategyKind[] AllStrategies =
    {
        StrategyKind.Lww,
        StrategyKind.Fww,
        StrategyKind.AdditiveWins,
        StrategyKind.GrowOnly,
    };

    [Fact]
    public void Last_write_wins_lets_the_later_removal_cut_the_later_addition()
    {
        var ops = new[] { Add("a", 1, 1, 10), Remove("b", 2, 3, 5) };

        var expected = new[] { "[1,3)", "(5,10]" };
        Strategies.Materialize(StrategyKind.Lww, ops)
            .Select(interval => interval.ToString())
            .ShouldBe(expected);
    }

    [Fact]
    public void First_write_wins_lets_the_earlier_addition_keep_its_range()
    {
        var ops = new[] { Add("a", 1, 1, 10), Remove("b", 2, 3, 5) };

        Strategies.Materialize(StrategyKind.Fww, ops).ShouldBe(new[] { new Interval(1, 10) });
    }

    [Fact]
    public void The_identifier_breaks_a_timestamp_tie_in_favour_of_the_later_one()
    {
        var ops = new[] { Add("a", 1, 1, 5), Remove("b", 1, 3, 5) };

        var expected = new[] { "[1,3)" };
        Strategies.Materialize(StrategyKind.Lww, ops)
            .Select(interval => interval.ToString())
            .ShouldBe(expected);
    }

    [Fact]
    public void The_identifier_breaks_a_timestamp_tie_in_favour_of_the_earlier_one()
    {
        var ops = new[] { Add("a", 1, 1, 5), Remove("b", 1, 3, 5) };

        Strategies.Materialize(StrategyKind.Fww, ops).ShouldBe(new[] { new Interval(1, 5) });
    }

    [Fact]
    public void A_later_addition_re_adds_what_a_removal_took_under_last_write_wins()
    {
        var ops = new[] { Add("a", 1, 1, 10), Remove("b", 2, 3, 5), Add("c", 3, 3, 5) };

        Strategies.Materialize(StrategyKind.Lww, ops).ShouldBe(new[] { new Interval(1, 10) });
    }

    [Fact]
    public void Additive_wins_subtracts_every_removal_from_every_addition()
    {
        var ops = new[] { Add("a", 1, 1, 10), Remove("b", 2, 3, 5), Add("c", 3, 3, 5) };

        var expected = new[] { "[1,3)", "(5,10]" };
        Strategies.Materialize(StrategyKind.AdditiveWins, ops)
            .Select(interval => interval.ToString())
            .ShouldBe(expected);
    }

    [Fact]
    public void Additive_wins_ignores_the_arrival_order()
    {
        var ops = new[] { Remove("b", 9, 3, 5), Add("a", 1, 1, 10) };

        var expected = new[] { "[1,3)", "(5,10]" };
        Strategies.Materialize(StrategyKind.AdditiveWins, ops)
            .Select(interval => interval.ToString())
            .ShouldBe(expected);
    }

    [Fact]
    public void Grow_only_ignores_removals()
    {
        var ops = new[] { Add("a", 1, 1, 10), Remove("b", 2, 3, 5) };

        Strategies.Materialize(StrategyKind.GrowOnly, ops).ShouldBe(new[] { new Interval(1, 10) });
    }

    [Fact]
    public void Segments_label_each_region_with_the_operation_that_decided_it()
    {
        var ops = new[] { Add("a", 1, 1, 9), Add("b", 2, 3, 5) };

        var segments = Strategies.Segments(StrategyKind.Lww, ops);

        var expectedRegions = new[] { "[1,3)", "[3,5]", "(5,9]" };
        var expectedWinners = new[] { "a", "b", "a" };
        segments.Select(segment => segment.Interval.ToString()).ShouldBe(expectedRegions);
        segments.Select(segment => segment.Winner.Id).ShouldBe(expectedWinners);
    }

    [Fact]
    public void Segments_exist_only_for_strategies_that_rank_operations()
    {
        var ops = new[] { Add("a", 1, 1, 10) };

        Strategies.Segments(StrategyKind.AdditiveWins, ops).ShouldBeEmpty();
        Strategies.Segments(StrategyKind.GrowOnly, ops).ShouldBeEmpty();
    }

    [Fact]
    public void Combining_segments_agrees_with_ranking_both_lists_together()
    {
        var first = new[] { Add("a", 1, 1, 10) };
        var second = new[] { Remove("b", 2, 3, 5) };

        var combined = Strategies.CombineSegments(
            StrategyKind.Lww,
            Strategies.Segments(StrategyKind.Lww, first),
            Strategies.Segments(StrategyKind.Lww, second));

        combined.ShouldBe(Strategies.Segments(StrategyKind.Lww, first.Concat(second).ToList()));
    }

    [Fact]
    public void Intervals_of_kind_filters_and_normalizes()
    {
        var ops = new[] { Add("a", 1, 3, 5), Add("b", 2, 1, 10), Remove("c", 3, 2, 3) };

        Strategies.IntervalsOf(ops, OpKind.Add).ShouldBe(new[] { new Interval(1, 10) });
        Strategies.IntervalsOf(ops, OpKind.Remove).ShouldBe(new[] { new Interval(2, 3) });
    }

    [Fact]
    public void Materializing_nothing_yields_nothing()
    {
        foreach (var strategy in AllStrategies)
        {
            Strategies.Materialize(strategy, Array.Empty<Op>()).ShouldBeEmpty();
        }
    }

    [Fact]
    public void An_undefined_strategy_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => Strategies.Materialize((StrategyKind)9, Array.Empty<Op>()));
    }

    [Fact]
    public void Duplicate_identifiers_do_not_make_the_answer_depend_on_arrival_order()
    {
        var addition = Op.Add(1, 6) with { Id = "dup", Timestamp = 0 };
        var removal = Op.Remove(3, 8) with { Id = "dup", Timestamp = 0 };

        var first = Strategies.Materialize(StrategyKind.Lww, new[] { addition, removal });
        var second = Strategies.Materialize(StrategyKind.Lww, new[] { removal, addition });

        Intervals.Equal(first, second).ShouldBeTrue();
    }
}
