using CsCheck;
using Shouldly;

namespace EventfulRanges.Tests.Properties;

/// <summary>
/// The defining property of the library: whatever order the operations arrive
/// in, materialization gives the answer an independent oracle gives. The oracle
/// is plain LINQ over every operation; the implementation sweeps.
/// </summary>
public sealed class StrategyProperties
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

    private static readonly string[] Ids = { "a", "b", "c", "d" };

    private static readonly Gen<Op> GenOp =
        from interval in GenInterval
        from timestamp in Gen.Long[0, 3]
        from id in Gen.Int[0, 3]
        from isAdd in Gen.Bool
        select new Op(Ids[id], isAdd ? OpKind.Add : OpKind.Remove, interval, timestamp);

    private static readonly Gen<IReadOnlyList<Op>> GenOps =
        GenOp.List[0, 6]
            .Where(list => list.Select(op => op.Id).Distinct().Count() == list.Count)
            .Select(list => (IReadOnlyList<Op>)list);

    private static readonly double[] ProbePoints =
        Enumerable.Range(-1, 13).SelectMany(k => new[] { k + 0.5, k + 1.0 }).ToArray();

    private static readonly StrategyKind[] AllStrategies =
    {
        StrategyKind.Lww,
        StrategyKind.Fww,
        StrategyKind.AdditiveWins,
        StrategyKind.GrowOnly,
    };

    private static readonly StrategyKind[] RankingStrategies =
    {
        StrategyKind.Lww,
        StrategyKind.Fww,
    };

    [Fact]
    public void Materialize_covers_a_point_exactly_when_the_oracle_says_so()
    {
        GenOps.Sample(ops =>
        {
            foreach (var strategy in AllStrategies)
            {
                var set = Strategies.Materialize(strategy, ops);

                foreach (var point in ProbePoints)
                {
                    Intervals.Contains(set, point).ShouldBe(Covered(strategy, ops, point));
                }
            }
        });
    }

    [Fact]
    public void Materialize_does_not_depend_on_the_arrival_order()
    {
        Gen.Select(GenOps, Gen.Int[0, 5]).Sample(pair =>
        {
            var (ops, rotation) = pair;
            var rotated = ops.Skip(rotation).Concat(ops.Take(rotation)).ToList();

            foreach (var strategy in AllStrategies)
            {
                Intervals.Equal(
                        Strategies.Materialize(strategy, ops),
                        Strategies.Materialize(strategy, rotated))
                    .ShouldBeTrue();
            }
        });
    }

    [Fact]
    public void Segments_name_the_operation_the_oracle_ranks_first()
    {
        GenOps.Sample(ops =>
        {
            foreach (var strategy in RankingStrategies)
            {
                foreach (var segment in Strategies.Segments(strategy, ops))
                {
                    foreach (var point in ProbePoints.Where(segment.Interval.Contains))
                    {
                        var covering = ops.Where(op => op.Interval.Contains(point)).ToList();

                        segment.Winner.Id.ShouldBe(RankedFirst(strategy, covering)!.Id);
                    }
                }
            }
        });
    }

    /// <summary>The independent answer: plain LINQ over every operation.</summary>
    private static bool Covered(StrategyKind strategy, IReadOnlyList<Op> ops, double point)
    {
        var covering = ops.Where(op => op.Interval.Contains(point)).ToList();

        return strategy switch
        {
            StrategyKind.AdditiveWins => covering.Any(op => op.Kind == OpKind.Add)
                && covering.All(op => op.Kind != OpKind.Remove),
            StrategyKind.GrowOnly => covering.Any(op => op.Kind == OpKind.Add),
            _ => RankedFirst(strategy, covering)?.Kind == OpKind.Add,
        };
    }

    /// <summary>The operation the strategy prefers among the ones covering a point.</summary>
    private static Op? RankedFirst(StrategyKind strategy, IReadOnlyList<Op> covering) =>
        strategy == StrategyKind.Fww
            ? covering.OrderBy(op => op.Timestamp).ThenBy(op => op.Id, StringComparer.Ordinal).FirstOrDefault()
            : covering.OrderByDescending(op => op.Timestamp)
                .ThenByDescending(op => op.Id, StringComparer.Ordinal)
                .FirstOrDefault();
}
