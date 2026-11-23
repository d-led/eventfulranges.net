using Shouldly;

namespace EventfulRanges.Tests;

public sealed class OpTests
{
    [Fact]
    public void An_addition_carries_its_kind_interval_and_a_fresh_identifier()
    {
        var op = Op.Add(1, 10);

        op.Kind.ShouldBe(OpKind.Add);
        op.Interval.ShouldBe(new Interval(1, 10));
        op.Id.ShouldNotBeNullOrEmpty();
        op.Timestamp.ShouldBe(0);
        op.Origin.ShouldBeNull();
        op.Validate().ShouldBe(OpError.None);
    }

    [Fact]
    public void A_removal_carries_its_kind()
    {
        Op.Remove(3, 5).Kind.ShouldBe(OpKind.Remove);
    }

    [Fact]
    public void Explicit_bounds_survive_into_the_operation()
    {
        var op = Op.Add(1, 5, Bound.Closed, Bound.Open);

        op.Interval.ShouldBe(new Interval(1, 5, Bound.Closed, Bound.Open));
    }

    [Fact]
    public void Identifiers_do_not_repeat()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => Op.Add(0, 1).Id).ToList();

        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Theory]
    [InlineData("", IntervalError.None, OpError.MissingId)]
    [InlineData("op", IntervalError.Inverted, OpError.InvalidInterval)]
    public void Validate_names_the_first_problem(string id, IntervalError intervalError, OpError expected)
    {
        var interval = intervalError == IntervalError.None ? new Interval(1, 2) : new Interval(5, 1);

        new Op(id, OpKind.Add, interval).Validate().ShouldBe(expected);
    }

    [Fact]
    public void Validate_rejects_an_undefined_kind()
    {
        new Op("op", (OpKind)7, new Interval(1, 2)).Validate().ShouldBe(OpError.UnknownKind);
    }

    [Fact]
    public void A_malformed_operation_is_rejected_with_its_reason()
    {
        var invalid = new Op("", OpKind.Add, new Interval(1, 2));

        var exception = Should.Throw<InvalidOpException>(invalid.EnsureValid);

        exception.Error.ShouldBe(OpError.MissingId);
        exception.Op.ShouldBe(invalid);
        exception.ParamName.ShouldBe("op");
    }

    [Fact]
    public void Operations_render_as_kind_then_interval()
    {
        Op.Add(1, 5).ToString().ShouldBe("add [1,5]");
        Op.Remove(1, 5, Bound.Open, Bound.Closed).ToString().ShouldBe("remove (1,5]");
    }

    [Fact]
    public void An_operation_is_a_value_that_can_be_restamped()
    {
        var original = Op.Add(1, 5);

        var stamped = original with { Timestamp = 42 };

        stamped.Timestamp.ShouldBe(42);
        original.Timestamp.ShouldBe(0);
        stamped.Id.ShouldBe(original.Id);
    }

    [Fact]
    public void Operation_kinds_round_trip_through_their_wire_names()
    {
        OpKind.Add.ToWireName().ShouldBe("add");
        OpKind.Remove.ToWireName().ShouldBe("remove");
        OpKindNames.TryParse("remove", out var parsed).ShouldBeTrue();
        parsed.ShouldBe(OpKind.Remove);
        OpKindNames.TryParse("retract", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(StrategyKind.Lww, "lww")]
    [InlineData(StrategyKind.Fww, "fww")]
    [InlineData(StrategyKind.AdditiveWins, "additive-wins")]
    [InlineData(StrategyKind.GrowOnly, "grow-only")]
    public void Strategies_round_trip_through_their_wire_names(StrategyKind strategy, string name)
    {
        strategy.ToWireName().ShouldBe(name);
        StrategyNames.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(strategy);
    }

    [Fact]
    public void An_unknown_strategy_name_does_not_parse()
    {
        StrategyNames.TryParse("last-wins", out _).ShouldBeFalse();
    }
}
