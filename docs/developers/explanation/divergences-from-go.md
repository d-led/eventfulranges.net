# Divergences from the Go original

The port follows the original's model and its documented semantics. Where the
two disagree, the port follows the documentation, and the divergence is
recorded here with the evidence.

## 1. A shared endpoint of two intervals — fixed in both

**The original had a bug.** `interval.Interval.Overlaps` is documented as "reports
whether the intervals share at least one point", and `Touches` as "meet at
exactly one endpoint that belongs to at least one of them, without sharing it".
Its `maxStart`/`minEnd` picked the bound of a shared endpoint with "either side
closed", which contradicted both:

```text
$ go run ./check
point.Overlaps(beyond) = true      # [0,0] vs (0,7]
point.Touches(beyond)  = false     # [0,0] vs (0,7]
[0,5).Intersection([0,5]) = [0,5]  # should be [0,5)
```

**Both implementations now answer the same.** The original was fixed (commit
`1c18c4d` in the Go repository): the bound of a shared endpoint is `Closed` only
when both intervals include it, so one open side excludes the point.

```csharp
new Interval(0, 0).Overlaps(new Interval(0, 7, Bound.Open, Bound.Closed))   // false
new Interval(0, 0).Touches(new Interval(0, 7, Bound.Open, Bound.Closed))    // true
Interval.Intersection(new Interval(0, 5), new Interval(0, 5, Bound.Closed, Bound.Open))
    // [0,5)
```

**Why the CRDT was unaffected.** `Normalize` merges when `Overlaps || Touches`,
and replacing "either side closed" with "both sides closed" moves the degenerate
case from `Overlaps` to `Touches` — the disjunction is the same predicate. So
the materialized view, `Contains`, `Difference`, and `Sweep` are unchanged. Only
the two query helpers become correct, in both languages.

Pinned on this side by `IntervalTests.A_point_interval_does_not_overlap_the_range_that_leaves_that_point_out`,
`IntervalTests.Intervals_sharing_a_start_meet_at_the_stricter_bound`, and
`IntervalsTests.Normalize_merges_a_point_interval_into_the_range_that_starts_after_it`;
in Go by the degenerate cases in `TestOverlaps`, `TestTouches` and
`TestIntersection`.

## Deliberate non-divergences so far

- Textual formatting is .NET's shortest round-trip (`1.5`, not `1.500000`), and
  is culture-independent. Wire format is JSON, not `ToString`.
- `Interval` is a permissive value: construction does not throw. The original's
  zero value is a valid point interval, and property tests need to be able to
  build malformed values to test `Validate`.
