using CsCheck;
using Shouldly;

namespace EventfulRanges.Tests.Properties;

/// <summary>
/// The property the library exists for: replicas that see the same operations
/// end up with the same set, whatever each replica saw first.
/// </summary>
public sealed class ReplicationProperties
{
    private static readonly Gen<Interval> GenInterval =
        from a in Gen.Int[0, 10]
        from b in Gen.Int[0, 10]
        from startClosed in Gen.Bool
        from endClosed in Gen.Bool
        let start = Math.Min(a, b)
        let end = Math.Max(a, b)
        select start == end
            ? new Interval(start, end)
            : new Interval(
                start,
                end,
                startClosed ? Bound.Closed : Bound.Open,
                endClosed ? Bound.Closed : Bound.Open);

    private static readonly string[] Ids = { "a", "b", "c", "d", "e", "f" };

    private static readonly Gen<Op> GenOp =
        from interval in GenInterval
        from timestamp in Gen.Long[0, 3]
        from id in Gen.Int[0, 5]
        from isAdd in Gen.Bool
        select new Op(Ids[id], isAdd ? OpKind.Add : OpKind.Remove, interval, timestamp);

    private static readonly Gen<IReadOnlyList<Op>> GenOps =
        GenOp.List[0, 6]
            .Where(list => list.Select(op => op.Id).Distinct().Count() == list.Count)
            .Select(list => (IReadOnlyList<Op>)list);

    private static readonly StrategyKind[] AllStrategies =
    {
        StrategyKind.Lww,
        StrategyKind.Fww,
        StrategyKind.AdditiveWins,
        StrategyKind.GrowOnly,
    };

    private static readonly EngineOptions Deterministic = new()
    {
        Clock = new LamportClock(),
        SnapshotEvery = 0,
    };

    private static T Run<T>(ValueTask<T> task) => task.AsTask().GetAwaiter().GetResult();

    private static void Run(ValueTask task) => task.AsTask().GetAwaiter().GetResult();

    private static Engine Open(StrategyKind strategy) =>
        Run(Engine.OpenAsync(new MemoryLog(), strategy, Deterministic));

    [Fact]
    public void Replicas_that_exchange_their_operations_converge()
    {
        GenOps.Sample(ops =>
        {
            foreach (var strategy in AllStrategies)
            {
                using var left = Open(strategy);
                using var right = Open(strategy);

                Run(left.ApplyAllAsync(ops.Where((_, index) => index % 2 == 0).ToList()));
                Run(right.ApplyAllAsync(ops.Where((_, index) => index % 2 == 1).ToList()));

                Run(left.ApplyAllAsync(right.Ops));
                Run(right.ApplyAllAsync(left.Ops));

                Intervals.Equal(left.Materialize(), right.Materialize()).ShouldBeTrue();
            }
        });
    }

    [Fact]
    public void Gossip_reaches_the_answer_of_one_replica_that_saw_everything()
    {
        GenOps.Sample(ops =>
        {
            foreach (var strategy in AllStrategies)
            {
                using var left = Open(strategy);
                using var right = Open(strategy);
                using var single = Open(strategy);

                Run(left.ApplyAllAsync(ops.Take(ops.Count / 2).ToList()));
                Run(right.ApplyAllAsync(ops.Skip(ops.Count / 2).ToList()));
                Run(single.ApplyAllAsync(ops));

                Run(left.ApplyAllAsync(right.Ops));
                Run(right.ApplyAllAsync(left.Ops));

                Intervals.Equal(left.Materialize(), single.Materialize()).ShouldBeTrue();
                Intervals.Equal(right.Materialize(), single.Materialize()).ShouldBeTrue();
            }
        });
    }

    [Fact]
    public void A_reopened_replica_answers_the_same()
    {
        GenOps.Sample(ops =>
        {
            var log = new MemoryLog();
            using var first = Run(Engine.OpenAsync(log, StrategyKind.Lww, Deterministic));
            Run(first.ApplyAllAsync(ops));

            using var second = Run(Engine.OpenAsync(log, StrategyKind.Lww, Deterministic));

            Intervals.Equal(first.Materialize(), second.Materialize()).ShouldBeTrue();
            second.Ops.Count.ShouldBe(first.Ops.Count);
        });
    }
}
