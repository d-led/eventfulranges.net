# Changelog

Versioning: `0.0.x` while the library is being ported. `0.1.0` is the
n-dimensional parity release — the first version with the whole surface.

## 0.0.2

Operations, clocks and strategies: the step from algebra to CRDT events.

- `Op`: kind, interval, timestamp and origin, with a process-unique,
  roughly time-ordered identifier. `Op.Create`, `Op.Add`, `Op.Remove` and the
  `…WithBounds` variants; `Validate`/`EnsureValid` with `OpError` and
  `InvalidOpException`; records, so an engine stamps with `op with { Timestamp }`.
- `OpKind` and `OpKindNames`: the `"add"`/`"remove"` wire names.
- `IClock` with `HybridClock` (injectable `TimeProvider`, strictly increasing,
  never behind an observed stamp) and `LamportClock`.
- `StrategyKind`, `Winner`, `Segment` and `Strategies`: `Materialize`, `Segments`,
  `CombineSegments`, `ToIntervals`, `IntervalsOf`, built on the existing sweep.
- The strategy ranking is total — timestamp, then ordinal identifier, then
  canonical interval order — so a log that repeats an identifier cannot make the
  materialized set depend on arrival order.
- The package README is self-contained: every link is absolute, so it works when
  nuget.org renders it. `scripts/check-links.py` now fails the gate if the
  packaged README ever carries a relative link.

## 0.0.1

The interval foundation: everything the replication engine will stand on.

- `Interval`: `double` endpoints with per-side inclusivity, `Validate`,
  `EnsureValid`, `Contains`, `Overlaps`, `Touches`, `Meets` (the union is
  contiguous), `Merge`, `Subtract`, `Intersection`/`TryIntersection`,
  invariant-culture `ToString`.
- `IntervalError` and `InvalidIntervalException`: a malformed interval is a
  value that reports why, and an exception only where one is required.
- `Intervals`: canonical order (`Less`), `Normalize`, `Union`, `Difference`,
  `Contains`, `Overlaps`, `Equal`.
- `Intervals.Sweep` and `Cover`: the doubled-line sweep that partitions a union
  into winner-labelled pieces, with lazy-deletion heap ordering.
- `Bound` and `BoundNames`: `"closed"`/`"open"` wire names shared with the
  original Go implementation.

Fixed against the original:

- `Overlaps` and `Intersection` now treat a shared endpoint as included only
  when both intervals include it. The Go implementation used "either side
  closed", which made `[0,0]` overlap `(0,7]` and `[0,5) ∩ [0,5]` come out as
  `[0,5]`. The CRDT-visible behaviour is unchanged: `Normalize` merges on
  `Overlaps || Touches`, and that union is identical either way. See
  `docs/developers/explanation/divergences-from-go.md`.
