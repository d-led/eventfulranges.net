# Model a calendar of busy days

Goal: answer "is this day busy?" for a set of bookings and cancellations that
overlap each other. A date is a day number, so a date range is a plain interval.

## 1. Turn dates into day numbers

```csharp
using System.Globalization;
using EventfulRanges;

static double Day(string iso) =>
    DateOnly.Parse(iso, CultureInfo.InvariantCulture).DayNumber;
```

Day numbers are integers, and integers are exact in a `double` up to 2^53, so
nothing rounds.

## 2. Book with a half-open range

A stay from July 1 to July 10 occupies July 1–9 and frees July 10 for the next
guest:

```csharp
var alice = new Interval(Day("2026-07-01"), Day("2026-07-10"), Bound.Closed, Bound.Open);
```

## 3. Keep the busy set as the union of the bookings

```csharp
var bob = new Interval(Day("2026-07-06"), Day("2026-07-15"), Bound.Closed, Bound.Open);
var bookings = Intervals.Union(new[] { alice }, new[] { bob });
```

`Union` normalizes: one overlapping range comes back, not two.

## 4. Subtract the cancellations

```csharp
var cancelled = new Interval(Day("2026-07-08"), Day("2026-07-10"), Bound.Closed, Bound.Open);
var busy = Intervals.Difference(bookings, new[] { cancelled });
```

## 5. Answer what a guest asks

```csharp
Intervals.Contains(busy, Day("2026-07-08"));   // false: cancelled
Intervals.Contains(busy, Day("2026-07-13"));   // true: Bob is still away
Intervals.Overlaps(
    busy,
    new Interval(Day("2026-07-14"), Day("2026-07-20"), Bound.Closed, Bound.Open)); // true
```

## Rules worth knowing before you debug

- **Endpoints are exact.** `[a,b]` contains `b`; `[a,b)` does not. A day is busy
  exactly when `Contains` says so.
- **`Difference` is exact set difference.** `A \ B` together with `A ∩ B`
  reconstructs `A`, with no lost or duplicated points.
- **`Overlaps` needs a shared point.** Two ranges that meet where both bounds are
  open do not overlap. When you mean "the union of the two is contiguous", ask
  `Meets`.
- **Compare sets with `Intervals.Equal` only after normalizing.** It compares
  interval by interval, in order.
- **A booking is a value.** Store `Interval`s anywhere; they are immutable
  structs with structural equality.

## Where this is going

The same two operations — add a range, remove a range — are the operation log of
the CRDT. With `RangeSet` the same calendar becomes replicas that exchange
operations instead of state: `RangeSet.AddAsync` and `RangeSet.RemoveAsync`
append to a log, and the strategy materializes the set. The
[API reference](../reference/api.md) covers `RangeSet` and the box half that
builds on the same model.
