# Changelog

Versioning: `0.0.x` while the library is being ported. `0.1.0` is the
n-dimensional parity release — the first version with the whole surface.

## 0.0.3

The 1-D CRDT end to end: a replica that persists, converges and restarts.

- `IEventLog` with the optional `ISnapshotStore`, `ICompactableLog` and the
  combined `IEventStore`; `LogReadResult`, `SnapshotReadResult`,
  `StoreConflictException`, `SnapshotNotFoundException`.
- `MemoryLog` and `JsonlLog`: the whole store surface. The stream is
  `ranges.stream.jsonl`, one operation per line, snapshots embedded as
  `{"snapshot":{"version":N,"data":{…}}}`, compaction through a temporary file
  and a rename.
- `Engine` and `EngineOptions`: versioned append with catch-up and retries,
  stamped operations, a cached view, snapshots and compaction. Appends are
  serialized by one async gate, so concurrent callers of one engine cannot race
  each other; a store conflict from another writer is caught up and retried.
- `RangeSet`: `AddAsync`, `RemoveAsync`, `AddWithBoundsAsync`,
  `RemoveWithBoundsAsync`, `ApplyAsync`, `ApplyAllAsync`, `Ranges`, `Contains`,
  `Overlaps`, `Ops`, `SnapshotAsync`, `CompactAsync`.
- JSON is source-generated, so the package stays trimming- and AOT-safe, and the
  wire names are the original's: `id`, `kind`, `interval`, `ts`, `origin`, with
  `"closed"`/`"open"` for bounds.

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
