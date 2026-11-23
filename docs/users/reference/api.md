# API reference

Namespace `EventfulRanges` · assembly `EventfulRanges` · target framework
`net10.0` · package `EventfulRanges` · license MPL-2.0.

**Surface at 0.0.1:** interval algebra. `RangeSet`, `BoxSet`, the operation log
and the stores arrive in `0.0.x`; `0.1.0` completes n-dimensional parity.

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
