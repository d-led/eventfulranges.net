# API reference

Namespace `EventfulRanges` · assembly `EventfulRanges` · target framework
`net10.0` · package `EventfulRanges` · license MPL-2.0.

**Surface at 0.0.2:** interval algebra, operations, clocks and strategies. The
log engine, the stores and `RangeSet` arrive in `0.0.x`; `0.1.0` completes
n-dimensional parity.

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

## Invariants you can rely on

- `Normalize` output is sorted by `Less`, contains no empty interval, and no two
  neighbours overlap or touch. It is idempotent, and independent of input order.
- `Difference(a, b)` covers exactly the points of `a` that `b` does not.
- `Sweep` output is a partition of the union of its input.
- Every returned value is a fresh, immutable value; nothing aliases internal
  state.

## See also

- [Range algebra](../explanation/range-algebra.md) — why endpoints are compared,
  never combined.
- [Divergences from the Go original](../../developers/explanation/divergences-from-go.md) —
  the one place this library deliberately answers differently from its source.
