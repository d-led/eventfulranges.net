# Changelog

`0.1.0` is the n-dimensional parity release: the port covers the whole library
rather than half of it, and is the first version worth pinning.

## 0.1.0 — n-dimensional parity

The same CRDT over boxes: regions in any number of dimensions, each face included
or excluded, with retraction and layered views. The `0.0.3` surface is unchanged
and still supported as it was.

- `Box` with per-face inclusivity, validation, geometry-only equality and canonical
  order; `Boxes` with normalize, union, exact difference, insertion, adjacent merge
  and connected components.
- `Meta`/`MetaMerge`: the metadata join — keys from either side, nested objects
  joined, scalars resolved by raw bytes so every replica agrees.
- `Canonicalizer`/`Canonicalizers.Chain`: the cover-preserving seam the engine
  shapes its view through.
- `Line`, `LineSpan`, `LineSegment` with `Boxes.Crossed` and `Boxes.Traverse`:
  region queries by a straight segment, with the box's half-open convention.
- `BoxOp` with `Retract`, five box strategies including `AdditiveWinsLww`, and
  `Layers` for the painter's front.
- `BoxEngine`, `BoxMemoryLog`, `BoxJsonlLog` and the `BoxSet` facade: an
  n-dimensional replica that persists and survives a restart.
- `Boxes.MergeAdjacent` merges through cross-section, pair-order and spatial
  indexes above 512 boxes — measured on this port — instead of pairwise passes,
  with the same cover either way.
- Snapshots and compaction for a box log: `IBoxSnapshotStore`,
  `ICompactableBoxLog`, `SnapshotAsync()`, `CompactAsync()`. A restart reloads
  from a compatible snapshot and replays only the tail; compaction rewrites the
  stream as one snapshot plus what it does not cover.
- `RTree`: a static, bulk-loaded spatial index over a cover.
- `EventfulRanges.Kurrent`: the first store backend beyond the local file —
  operations as one KurrentDB stream and snapshots as another, with the server's
  own optimistic concurrency as the version check. Its tests skip without a
  server; `scripts/itest-kurrent.sh` and a CI job run them for real.
- `scripts/bench.sh`: BenchmarkDotNet measured the two adjacent merges, which is
  where the 512-box threshold comes from — the index is 6× the scan at 512 cells
  and 49× at 4096, and level with it at 128.

Tested to the same bar as the range half: oracle property tests over the algebra,
replication properties, and a CRAP budget of 10 that every method meets. 380 tests
in the gate, 17 more against a live KurrentDB server, and mutation testing at 77%
with every survivor read.

Two things this release does not do, both written down rather than left to be
discovered: it does not compact a KurrentDB stream (a stream cannot lose its
prefix), and its mutation score is 77 against a target of 80.

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
