# API reference

Namespace `EventfulRanges` · assembly `EventfulRanges` · target framework
`net10.0` · package `EventfulRanges` · license MPL-2.0.

**Published at 0.0.2:** interval algebra, operations, clocks and strategies.
**On `main`, shipping in the next release:** the log engine, the memory and JSON
Lines stores, and the `RangeSet` facade. `0.1.0` completes n-dimensional parity.

Everything below is immutable and thread-safe. `Interval` and `Cover` are
`readonly record struct`s with structural equality; every static method is pure.

## Types

| Type | Kind | Purpose |
| --- | --- | --- |
| `Interval` | `readonly record struct` | One contiguous range with per-side inclusivity |
| `Bound` | `enum : byte` | Includes or excludes an endpoint (`Closed`, `Open`) |
| `BoundNames` | `static class` | Wire names for `Bound` |
| `IntervalError` | `enum` | Why an `Interval` is not well-formed |
| `InvalidIntervalException` | `sealed class : ArgumentException` | Thrown by `EnsureValid` |
| `Intervals` | `static class` | Set operations over collections of `Interval` |
| `Cover` | `readonly record struct` | One piece of a swept union, with its winner |
| `Op` | `sealed record` | One addition or removal on the line |
| `OpKind` | `enum : byte` | `Add` or `Remove` |
| `OpKindNames` | `static class` | Wire names for `OpKind` |
| `OpError` | `enum` | Why an `Op` is not well-formed |
| `InvalidOpException` | `sealed class : ArgumentException` | Thrown by `Op.EnsureValid` |
| `IClock` | `interface` | Stamps new operations and observes remote ones |
| `HybridClock`, `LamportClock` | `sealed class` | The two clocks |
| `StrategyKind` | `enum : byte` | The four conflict-resolution rules |
| `StrategyNames` | `static class` | Wire names for `StrategyKind` |
| `Winner`, `Segment` | `readonly record struct` | Who decided which region |
| `Strategies` | `static class` | Materializes a history into a set |
| `IEventLog` | `interface` | An append-only, versioned operation stream |
| `ISnapshotStore`, `ICompactableLog`, `IEventStore` | `interface` | The optional store capabilities |
| `LogReadResult`, `SnapshotReadResult` | `readonly record struct` | What a read returns |
| `StoreConflictException`, `SnapshotNotFoundException` | `sealed class : Exception` | Store failures a caller must handle |
| `MemoryLog`, `JsonlLog` | `sealed class` | The two stores |
| `EngineOptions` | `sealed record` | Clock and snapshot interval |
| `Engine` | `sealed class : IDisposable` | Applies operations, owns the cached view |
| `RangeSet` | `sealed class : IDisposable` | The facade a caller uses |
| `ArgumentNullException` | — | Thrown by every `Intervals` method for a null argument |

## `Interval`

### Constructors

| Signature | Result |
| --- | --- |
| `Interval(double start, double end)` | Closed `[start, end]` |
| `Interval(double start, double end, Bound startBound, Bound endBound)` | Explicit inclusivity per side |

Constructors do not validate. Any `double` is stored as given.

`default(Interval)` is `[0, 0]` — both bounds `Closed`, and valid.

### Properties

| Member | Type | Meaning |
| --- | --- | --- |
| `Start` | `double` | Lower endpoint |
| `End` | `double` | Upper endpoint |
| `StartBound` | `Bound` | Whether `Start` belongs to the interval |
| `EndBound` | `Bound` | Whether `End` belongs to the interval |
| `IsValid` | `bool` | `Validate() == IntervalError.None` |
| `IsEmpty` | `bool` | No point belongs to it: `Start > End`, or `Start == End` with an `Open` bound |

### Members

| Signature | Returns | Throws | Behaviour |
| --- | --- | --- | --- |
| `Validate()` | `IntervalError` | — | First failing rule, in this order: `NotANumber`, `NotFinite`, `Inverted`, `Empty` |
| `EnsureValid()` | `void` | `InvalidIntervalException` | Rejects an interval that is not well-formed |
| `Contains(double value)` | `bool` | — | Within the span and not excluded by an open bound |
| `Overlaps(Interval other)` | `bool` | — | The intervals share at least one point. A shared endpoint counts only when **both** intervals include it |
| `Touches(Interval other)` | `bool` | — | The intervals meet at exactly one endpoint that at least one of them includes, and share nothing else |
| `Meets(Interval other)` | `bool` | — | `Overlaps(other) \|\| Touches(other)`: the union of the two is contiguous |
| `Subtract(Interval cut)` | `IReadOnlyList<Interval>` | — | The parts of this interval not covered by `cut`: zero, one or two intervals, in order. Returns `[this]` when `!Meets(cut)` |
| `ToString()` | `string` | — | Standard notation, e.g. `[1,5)`; culture-independent, shortest round-trip numbers |

### Static methods

| Signature | Returns | Throws | Behaviour |
| --- | --- | --- | --- |
| `Merge(Interval first, Interval second)` | `Interval` | — | Smallest interval containing both. A shared endpoint is `Closed` when either side includes it. Does **not** check that the union is contiguous — callers gate on `Meets` |
| `Intersection(Interval first, Interval second)` | `Interval` | `InvalidOperationException` | The shared interval; requires the intervals to overlap |
| `TryIntersection(Interval first, Interval second, out Interval intersection)` | `bool` | — | `false` when they do not overlap, and `intersection` is `default` |

## `Intervals`

Static set operations. A "set" is any `IReadOnlyList<Interval>`.

| Signature | Returns | Behaviour |
| --- | --- | --- |
| `Less(Interval first, Interval second)` | `bool` | Canonical order: by `Start`, then `StartBound` (`Closed` first), then `End`, then `EndBound` (`Closed` first) |
| `Normalize(IReadOnlyList<Interval> intervals)` | `IReadOnlyList<Interval>` | Sorts, drops empty intervals, and merges every pair where `Meets` holds. Empty input returns an empty list |
| `Union(IReadOnlyList<Interval> first, IReadOnlyList<Interval> second)` | `IReadOnlyList<Interval>` | `Normalize(first ∪ second)` |
| `Difference(IReadOnlyList<Interval> first, IReadOnlyList<Interval> second)` | `IReadOnlyList<Interval>` | Exact set difference: every point in `first` and not in `second`. Returns `Normalize(first)` when `second` is empty |
| `Contains(IReadOnlyList<Interval> set, double value)` | `bool` | Membership in any interval of the set |
| `Overlaps(IReadOnlyList<Interval> set, Interval interval)` | `bool` | Any interval of the set overlaps `interval` |
| `Equal(IReadOnlyList<Interval> first, IReadOnlyList<Interval> second)` | `bool` | Same intervals in the same order. Compare normalized sets |
| `Sweep(IReadOnlyList<Interval> intervals, Func<int, int, bool> better)` | `IReadOnlyList<Cover>` | See below |

### `Sweep` contract

- `better(left, right)` answers "does the left index outrank the right one on the
  points both cover". Ties are broken by the lower index, so the order is total
  and the result is deterministic.
- The result partitions the union of the input: pieces are disjoint, ordered, and
  omitted where no interval covers. Adjacent pieces with the same winner are one
  piece.
- Each `Cover.Index` is the best covering index for every point of its interval.
- **Precondition:** every input interval is well-formed (`Validate()` returns
  `None`). `Sweep` does not check this.

```csharp
var covers = Intervals.Sweep(
    new[] { new Interval(1, 9), new Interval(3, 5) },
    better: (left, right) => left > right);
// [1,3) by 0, [3,5] by 1, (5,9] by 0
```

## `Bound`

| Member | Value | Wire name (`ToWireName`) |
| --- | --- | --- |
| `Bound.Closed` | `0` | `"closed"` |
| `Bound.Open` | `1` | `"open"` |

Numeric values are part of the contract; they match the original Go
implementation and the on-the-wire names.

## `BoundNames`

| Signature | Returns | Throws |
| --- | --- | --- |
| `ToWireName(this Bound bound)` | `string` | `ArgumentOutOfRangeException` for an undefined value |
| `TryParse(string? name, out Bound bound)` | `bool` | — |

`TryParse` is case-sensitive, matches `"closed"` and `"open"` only, and returns
`false` for `null` or any other string.

## `IntervalError`

| Value | Meaning |
| --- | --- |
| `None` | Well-formed |
| `NotANumber` | `Start` or `End` is `NaN` |
| `NotFinite` | `Start` or `End` is infinite |
| `Inverted` | `Start > End` |
| `Empty` | `Start == End` and at least one bound is `Open` |

## `InvalidIntervalException`

| Member | Type | Value |
| --- | --- | --- |
| `Interval` | `Interval` | The rejected value |
| `Error` | `IntervalError` | Why it was rejected |
| `ParamName` | `string` | `"interval"` |

## `Cover`

| Member | Type | Meaning |
| --- | --- | --- |
| `Interval` | `Interval` | The piece |
| `Index` | `int` | Index into the swept input of the interval that decided the piece |

## `OpKind`

| Member | Value | Wire name |
| --- | --- | --- |
| `OpKind.Add` | `0` | `"add"` |
| `OpKind.Remove` | `1` | `"remove"` |

`OpKindNames.ToWireName(OpKind)` throws `ArgumentOutOfRangeException` for an
undefined value. `OpKindNames.TryParse(string?, out OpKind)` is case-sensitive.

## `Op`

`sealed record Op(string Id, OpKind Kind, Interval Interval, long Timestamp = 0, string? Origin = null)`

| Member | Type | Meaning |
| --- | --- | --- |
| `Id` | `string` | Process-unique, roughly time-ordered |
| `Kind` | `OpKind` | Add or remove |
| `Interval` | `Interval` | The range the operation applies to |
| `Timestamp` | `long` | Ordering stamp; `0` means not stamped yet |
| `Origin` | `string?` | Optional producer |

| Signature | Returns | Behaviour |
| --- | --- | --- |
| `Validate()` | `OpError` | First failing rule: `MissingId`, `UnknownKind`, `InvalidInterval` |
| `EnsureValid()` | `void` | Throws `InvalidOpException` |
| `Create(OpKind, Interval)` | `Op` | Fresh identifier |
| `Add(double, double)` | `Op` | `[start, end]`, timestamp `0` |
| `Add(double, double, Bound, Bound)` | `Op` | Explicit inclusivity |
| `Remove(double, double)` | `Op` | `[start, end]`, timestamp `0` |
| `Remove(double, double, Bound, Bound)` | `Op` | Explicit inclusivity |
| `ToString()` | `string` | `add [1,5]` |

`Op` is a record: `op with { Timestamp = 42 }` is how an engine stamps an
operation, and the identifier is preserved.

## `OpError` and `InvalidOpException`

| `OpError` | Meaning |
| --- | --- |
| `None` | Well-formed |
| `MissingId` | `Id` is null or empty |
| `UnknownKind` | `Kind` is not a defined `OpKind` |
| `InvalidInterval` | `Interval.Validate()` is not `None` |

`InvalidOpException : ArgumentException` exposes `Op`, `Error` and
`ParamName == "op"`.

## Clocks

| Signature | Behaviour |
| --- | --- |
| `IClock.Tick()` | Strictly increasing timestamp for a new local operation |
| `IClock.Observe(long)` | Advances the clock past a timestamp seen elsewhere; never goes backwards |
| `HybridClock(TimeProvider? = null)` | Wall clock in nanoseconds since the Unix epoch; two ticks in the same reading differ by 1; `TimeProvider.System` by default |
| `LamportClock()` | Counts 1, 2, 3… from zero |

## `StrategyKind`

| Member | Wire name | Rule |
| --- | --- | --- |
| `Lww` | `"lww"` | Highest `(timestamp, id)` wins at each point |
| `Fww` | `"fww"` | Lowest `(timestamp, id)` wins at each point |
| `AdditiveWins` | `"additive-wins"` | Union of additions minus union of removals |
| `GrowOnly` | `"grow-only"` | Union of additions; removals ignored |

## `Winner` and `Segment`

| Type | Members |
| --- | --- |
| `Winner` | `Timestamp`, `Id`, `Kind` |
| `Segment` | `Interval`, `Winner` |

## `Strategies`

| Signature | Returns | Behaviour |
| --- | --- | --- |
| `Materialize(StrategyKind, IReadOnlyList<Op>)` | `IReadOnlyList<Interval>` | The strategy's canonical set; independent of the order of `ops`; throws `ArgumentOutOfRangeException` for an undefined strategy |
| `Segments(StrategyKind, IReadOnlyList<Op>)` | `IReadOnlyList<Segment>` | Winner-annotated regions for `Lww` and `Fww`; empty for the others |
| `CombineSegments(StrategyKind, IReadOnlyList<Segment>, IReadOnlyList<Segment>)` | `IReadOnlyList<Segment>` | Merges two canonical segment sets |
| `ToIntervals(IReadOnlyList<Segment>)` | `IReadOnlyList<Interval>` | The regions decided by additions |
| `IntervalsOf(IReadOnlyList<Op>, OpKind)` | `IReadOnlyList<Interval>` | Normalized intervals of one kind |

Ranking order, in full: timestamp, then `string.CompareOrdinal` on the
identifier, then the canonical interval order. The last step only matters when a
log repeats an identifier; the engine deduplicates by id, so it never does.

## Stores

| Signature | Behaviour |
| --- | --- |
| `IEventLog.AppendAsync(long expectedVersion, IReadOnlyList<Op> ops, CancellationToken)` | Appends atomically at the expected version |
| `IEventLog.ReadAsync(long fromVersion, CancellationToken)` | `LogReadResult(Ops, Version)`: the operations after `fromVersion` and the log's total count |
| `ISnapshotStore.SaveSnapshotAsync(byte[] data, long version, CancellationToken)` | Stores a snapshot |
| `ISnapshotStore.LoadSnapshotAsync(CancellationToken)` | `SnapshotReadResult(Data, Version)`; throws `SnapshotNotFoundException` |
| `ICompactableLog.CompactAsync(byte[] snapshot, long version, CancellationToken)` | Rewrites the log as the snapshot plus what it does not cover |
| `IEventStore` | `IEventLog` + `ISnapshotStore` |

`MemoryLog(IEnumerable<Op>? initial = null)` implements the whole surface;
`JsonlLog.Open(string directory)` writes `ranges.stream.jsonl` in that directory
and implements the whole surface too. A stale `expectedVersion` throws
`StoreConflictException` carrying both versions.

`JsonlLog` is safe for concurrent callers in one process, not for several
processes appending at once. Compaction rewrites through a temporary file and a
rename, so a crash cannot leave a half-written stream.

### The KurrentDB store

`EventfulRanges.Kurrent` puts the log in KurrentDB. It is a separate package so
the client and its gRPC stack stay out of a library that is AOT-compatible
without them.

```csharp
using var log = KurrentLog.Open("esdb://localhost:2113?tls=false");
using var replica = await RangeSet.OpenStoreAsync(log, StrategyKind.Lww);
```

| Member | Behaviour |
| --- | --- |
| `KurrentLog.Open(string connectionString, string operationStream = "eventfulranges-ops", string snapshotStream = "eventfulranges-snapshot")` | Connects; the stream names are parameters so two logs on one server never share a log by accident |
| `OperationStream`, `SnapshotStream` | The streams it reads and writes |
| `AppendAsync`, `ReadAsync` | The `IEventLog` contract; KurrentDB's own expected revision is the version check |
| `SaveSnapshotAsync`, `LoadSnapshotAsync` | The `ISnapshotStore` contract; the newest snapshot event wins |
| `Dispose()` | Releases the connection |

Two replicas may write the same stream: the loser of the race is told
`StoreConflictException`, and the engine catches up and retries rather than
failing. A read past the end of the stream reports the log's real length, not the
version that was asked for.

The store does **not** compact — a KurrentDB stream cannot lose its prefix — so
`Engine.CompactAsync` reports that the log cannot compact. Snapshots still
keep restarts cheap.

## `EngineOptions` and `Engine`

```csharp
var engine = await Engine.OpenAsync(log, StrategyKind.Lww, new EngineOptions
{
    Clock = new LamportClock(),      // default: HybridClock over system time
    SnapshotEvery = 0,               // default: 100; 0 disables auto-snapshot
});
```

| Member | Behaviour |
| --- | --- |
| `Engine.OpenAsync(IEventLog, StrategyKind, EngineOptions?, CancellationToken)` | Loads a compatible snapshot, replays the rest of the log, materializes |
| `ApplyAsync(Op, CancellationToken)` | `ValueTask<Op>`: the operation as stored, with the timestamp the engine stamped. A duplicate id is a no-op |
| `ApplyAllAsync(IReadOnlyList<Op>, CancellationToken)` | The same for a batch |
| `Materialize()` | The canonical view, a copy |
| `Contains(double)`, `Overlaps(Interval)` | Membership questions against the view |
| `Ops` | Every known operation, sorted by identifier |
| `SnapshotAsync(CancellationToken)` | Persists a snapshot now; a log without `ISnapshotStore` skips it |
| `CompactAsync(CancellationToken)` | Throws `NotSupportedException` unless the log implements `ICompactableLog` |
| `Dispose()` | Releases the async gate; do not use the engine afterwards |

Appends are serialized by one async gate, so concurrent callers of one engine
never race each other. A `StoreConflictException` from another writer triggers a
catch-up and up to three retries before the exception surfaces.

## `RangeSet`

| Member | Behaviour |
| --- | --- |
| `OpenAsync(string directory, StrategyKind, EngineOptions?, CancellationToken)` | A replica over a JSON Lines stream |
| `OpenStoreAsync(IEventLog, StrategyKind, EngineOptions?, CancellationToken)` | A replica over any log |
| `Strategy` | The strategy it materializes with |
| `Ranges` | The canonical view |
| `Contains(double)`, `Overlaps(Interval)` | Membership questions |
| `Ops` | The known operations, sorted by identifier |
| `ApplyAsync(Op, CancellationToken)`, `ApplyAllAsync(...)` | Apply prepared operations |
| `AddAsync(double, double, ...)`, `AddWithBoundsAsync(...)` | `ValueTask<Op>`: the applied operation |
| `RemoveAsync(double, double, ...)`, `RemoveWithBoundsAsync(...)` | The same for removals |
| `SnapshotAsync(...)`, `CompactAsync(...)` | Forwarded to the engine |
| `Dispose()` | Releases the engine |

## The n-dimensional half

The same model over boxes: an axis-aligned region whose every face is included or
excluded. Everything below mirrors the range types one dimension up.

| Type | Kind | Purpose |
| --- | --- | --- |
| `Box` | `sealed class` | An n-dimensional region with per-face inclusivity |
| `BoxError` | `enum` | Why a box is not well-formed |
| `InvalidBoxException` | `sealed class : ArgumentException` | A box was required to be well-formed |
| `Boxes` | `static class` | The cover algebra: normalize, union, exact difference, insertion, merge, components |
| `Meta` / `MetaMerge` | `static class` / `delegate` | The metadata join: keys from either side, scalars by raw bytes |
| `Canonicalizer` / `Canonicalizers` | `delegate` / `static class` | Cover-preserving rewrites, and `Chain` |
| `Line`, `LineSpan`, `LineSegment` | classes and a `record struct` | Region queries: what a straight segment crosses |
| `BoxOp`, `BoxOpKind`, `BoxOpError`, `InvalidBoxOpException` | `sealed record`, enums, exception | Add, remove, or retract a box |
| `BoxStrategies`, `BoxSegment`, `BoxWinner`, `Layer` | `static class`, records | Materialization, and the painter's front |
| `IBoxLog`, `IBoxSnapshotStore`, `ICompactableBoxLog`, `IBoxEventStore` | interfaces | The box log, and its optional snapshot and compaction capabilities |
| `BoxMemoryLog`, `BoxJsonlLog`, `BoxLogReadResult` | classes and a `record struct` | The two box logs |
| `BoxEngine`, `BoxEngineOptions`, `DimensionMismatchException` | `sealed class`, `record`, exception | Applies box operations, owns the cover |
| `BoxSet` | `sealed class : IDisposable` | The n-dimensional facade |
| `RTree` | `sealed class` | A static spatial index over a cover |

### `Box`

`new Box(min, max)` is half-open — every lower face closed, every upper face open.
`new Box(min, max, minBound, maxBound)` sets the faces; an empty bound list means
the default. `Validate()` names one problem (`BoxError.Dimensions`, `NoDimensions`,
`Bounds`, `NotANumber`, `NotFinite`, `Inverted`) and `EnsureValid()` throws
`InvalidBoxException`.

Emptiness is a **value**, not a fault: `IsEmpty` is true for an inverted box or for
one whose corners meet on an open face, and `Boxes.Normalize` drops those rather
than rejecting them. `Contains(point)`, `Contains(box)`, `Overlaps(box)`,
`LowestBound(axis)`, `HighestBound(axis)`, `WithMeta(...)` answer the rest.
`Equals` and `CompareTo` describe geometry only — metadata is never part of a box's
identity, and `CompareTo` is canonical cover order (lower corner, upper corner,
faces, axis by axis).

### `Boxes`

| Signature | Behaviour |
| --- | --- |
| `Normalize(boxes)`, `Normalize(boxes, merge)` | Drops empty and absorbed boxes, joins the metadata of the absorbed, sorts |
| `Union(first, second)` | The canonical cover of both |
| `Difference(first, second)` | Exactly the points in the first and not the second; carved pieces keep their box's metadata |
| `InsertNormalized(cover, box)` | Adds one box to a normalized cover without re-normalizing it |
| `MergeAdjacent(boxes)` | Merges every edge-adjacent pair, joining their metadata |
| `Components(boxes)` | Maximal regions: overlapping, or sharing a face of positive measure |
| `Contains(cover, point)`, `Overlaps(cover, box)`, `Equal(first, second)` | Questions about a cover |
| `Crossed(cover, line)`, `Traverse(cover, line)` | Region queries: what a segment meets, and how coverage changes along it |

`MergeAdjacent` is a deterministic greedy, not a minimal rectangle cover: it takes
the first mergeable pair in canonical order and repeats. A pair whose shared face
both sides exclude leaves a pinhole no single box can represent, so it stays apart.
Above a few hundred boxes it merges the same cover through indexes instead of
pairwise passes, which is what keeps a partition of thousands of cells quick; the
two paths answer the same cover, so the choice is only ever about cost.

### `BoxOp` and `BoxStrategies`

`BoxOp.Add(min, max)`, `AddWithBounds(...)`, `Remove(...)`, `RemoveWithBounds(...)`
build edits; `BoxOp.Retract(operationId, box)` cancels one named edit, carrying the
box of the operation it undoes. Wire names are `add`, `remove`, `retract`, and the
JSON fields are `id`, `kind`, `box`, `ts`, `ref`, `origin`.

`BoxStrategies.Effective(ops)` drops each retraction and its target; retraction is
single-level, so retracting a retraction changes nothing. `Materialize(strategy, ops)`
projects the log under any of the five strategies — `Lww`, `Fww`, `AdditiveWins`,
`GrowOnly`, and `AdditiveWinsLww`, which resolves each point's metadata by the latest
addition covering it. `Layers(ops)` returns the painter's front instead: whole
regions bottom to top, with only fully hidden ones culled.

`AdditiveWinsLww` is the n-dimensional strategy. `Strategies.Materialize` (the range
half) rejects it with a `NotSupportedException` naming the reason.

### `BoxEngine` and `BoxSet`

```csharp
using var wall = await BoxSet.OpenAsync("./wall", StrategyKind.AdditiveWinsLww);
await wall.AddAsync([0, 0], [10, 10]);
await wall.RemoveAsync([3, 3], [5, 5]);
var crossed = wall.Crossed(new Line([0, 5], [10, 5]));
```

`BoxSet.OpenAsync(directory, strategy)` keeps the history in
`boxes.stream.jsonl`; `OpenLogAsync(IBoxLog, strategy)` takes any box log.
`BoxEngineOptions` carries the `Clock`, the `Canonicalizer` that shapes the cover
before it is returned, and the `MetaMerge` used when boxes combine.

The engine rejects an operation of another dimensionality
(`DimensionMismatchException`) — a replica is one space, and the first operation
fixes it — and surfaces a log that moved on as `StoreConflictException` with the
cover untouched.

`SnapshotAsync()` persists the cover, the version and every known operation, and
`CompactAsync()` rewrites the log as that snapshot plus the operations it does not
cover. A snapshot is a cache of history, never a replacement: a restart seeds
every operation from it and replays only the tail, so a replica that compacts
still answers the same cover and still gossips complete history. A snapshot built
by another strategy is ignored rather than trusted, and `SnapshotAsync()` on a log
without `IBoxSnapshotStore` does nothing, while `CompactAsync()` on a log without
`ICompactableBoxLog` says so with a `NotSupportedException`.

### `RTree`

`RTree.Build(boxes)` bulk-loads a static index by median split on the widest axis;
`Search(query)` returns the boxes sharing a point with it in canonical order, while
`SearchInTraversalOrder(query)` skips the sort. Boxes are immutable, so a tree
stores them directly and needs no copying variant.

## Invariants you can rely on

- `Normalize` output is sorted by `Less`, contains no empty interval, and no two
  neighbours overlap or touch. It is idempotent, and independent of input order.
- `Difference(a, b)` covers exactly the points of `a` that `b` does not.
- A snapshot and its tail materialize to the same cover as the whole log.
- `MergeAdjacent` returns the same cover whichever merge implementation runs.
- A read past the end of a store reports the log's length, never the requested
  version.
- `Sweep` output is a partition of the union of its input.
- `Materialize` is order-independent for every strategy; a replica that applied
the same operations in another order, or received them from another replica,
answers the same.
- An operation reaches the view only after the log accepted it; a rejected append
leaves the view unchanged.
- `Boxes.Normalize` is idempotent and order-independent, and `Boxes.Equal`
  compares the normalized covers point for point.
- `Boxes.Difference` is exact at the boundary: a box carved by an open-faced
  neighbour keeps the faces that neighbour excludes.
- `BoxStrategies.Materialize` answers the same for every arrival order, and
  `Layers` runs bottom to top, so a painter drawing the list in order gets the
  same front.
- Every returned value is a fresh, immutable value; nothing aliases internal
  state.

## See also

- [Range algebra](../explanation/range-algebra.md) — why endpoints are compared,
  never combined.
- [Divergences from the Go original](../../developers/explanation/divergences-from-go.md) —
  the one place this library deliberately answers differently from its source.
