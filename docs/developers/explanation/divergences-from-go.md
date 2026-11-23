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

## 2. Names that would collide in C#

The n-dimensional query shape is `Line` here and `Path` in the original. A type
named `Path` in this namespace shadows `System.IO.Path`, and the library's own
`JsonlLog` failed to compile on it the first time the type existed — so the name is
not a preference but a defect avoided. The span of a line is `LineSpan` for the same
reason (`Span<T>`), and the documentation of both names the original's word.

`RTree` has one build rather than the original's two: the original's second build
exists to avoid copying the caller's boxes, and `Box` is immutable, so the copy
never has to happen. `Search` sorts by `Box.CompareTo` — the library's canonical
cover order — where the original sorts by a different total order that groups
corners; the original's own documentation calls its order "canonical", which it is
not.

## 3. Where the original drops what it has copied

`RTree.Build` drops empty boxes **before** validating them, as the original does:
an inverted box holds no point, so it cannot be indexed, and validating what will
not be kept would only reject input the builder can handle. A malformed box — a
NaN coordinate — is still refused. Both halves are pinned by tests, because the
ordering reads like an oversight until it is written down.

## 4. Where the merge switches to indexes

`MergeAdjacent` has two implementations in both languages and one contract: the
same cover, whichever runs. The threshold between them is the only number that
differs — 2048 in Go, 512 here — because it is a cost measurement against each
language's scan, not a semantic choice. Measured on a partitioned 2-D grid and on
overlapping 3-D boxes, the C# index overtakes its scan earlier: the Go scan is
cheap to start and the C# one carries the cost of `InsertNormalized` per merge.

The property test that holds the two paths to the same cover is ported, and it is
what makes the number safe to differ: a wrong threshold can only cost time.

## 5. Where the original computed an answer nobody read

`newCoverGrid` in the Go original walked every box's candidates to decide whether
the cover was disjoint, and stored the answer in a field no code read: the merger
asks the same question itself, through the same candidates, and caches it where
it is used. Removed upstream (commit `88c29b7` in the Go repository) after the
port showed it as an unexplained cost in the fast path. No behaviour changed, and
the `space` package stayed at 100% statement coverage.

## 6. A read past the end of a log — fixed in both

**The original answered the wrong version.** `kurrent.Store.Read` took the
version from each event's number and otherwise returned the version the caller
asked for, so reading version 9 of a two-op stream answered 9. An append at that
expectation then conflicted, and a catch-up loop would have asked again. Both
implementations now read the length from the last event when a read comes back
empty (`2cbb865` in the Go repository).

## 7. Stream names are parameters, not package variables

`KurrentLog.Open` takes the operation and snapshot stream names, where the
original keeps them as package-level variables that its tests reassign. Passing
them is what lets two logs on one server stay apart without shared mutable state,
and it keeps the tests parallel-safe.

## Deliberate non-divergences so far

- Textual formatting is .NET's shortest round-trip (`1.5`, not `1.500000`), and
  is culture-independent. Wire format is JSON, not `ToString`.
- The KurrentDB backend exists for the range half only, as in the original: the
  box half's stores are `BoxMemoryLog` and `BoxJsonlLog`.
- `Interval` is a permissive value: construction does not throw. The original's
  zero value is a valid point interval, and property tests need to be able to
  build malformed values to test `Validate`.
