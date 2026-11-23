# Range algebra: compared, never combined

`EventfulRanges` answers every question about real-valued ranges without doing
arithmetic on endpoints. This page is the mental model behind that, and the
reason the answers are exact rather than approximately right.

## The one rule

An endpoint is a value you **compare**, never a quantity you combine. Nothing
adds, subtracts, averages or scales a coordinate. Two consequences follow, and
everything else on this page is a detail of them:

- Floating-point coordinates cannot accumulate error, because no operation has
  the chance to introduce any. `0.1 + 0.2` never happens here; `0.3 == 0.3` does.
- Every answer is reproducible: the same inputs give the same outputs on every
  platform, in every culture.

## Inclusivity is per side, and it is data

An interval is four values: two endpoints and two bounds.

| Notation | `Start` | `StartBound` | `End` | `EndBound` |
| --- | --- | --- | --- | --- |
| `[a,b]` | `a` | `Closed` | `b` | `Closed` |
| `[a,b)` | `a` | `Closed` | `b` | `Open` |
| `(a,b]` | `a` | `Open` | `b` | `Closed` |
| `(a,b)` | `a` | `Open` | `b` | `Open` |

The bounds are not an afterthought: they decide membership, they survive every
set operation, and they appear in equality. `[0,5]` and `[0,5)` are different
values — they are not equal, and `Intersection` keeps the stricter bound,
`[0,5)` — yet their union is contiguous, so `Normalize` merges them into
`[0,5]`. Comparing ranges is not comparing their text.

The 1-D default is **closed on both sides**, which matches how people talk about
booked days and booked hours. The n-dimensional `Box` defaults to **half-open**
instead, because that makes set difference exact in more than one dimension. The
asymmetry is deliberate; do not assume one from the other.

## A shared endpoint belongs to the intersection only when both sides include it

This is the rule that is easy to get wrong, and the one place this library
answers differently from its Go original (see
[divergences](../../developers/explanation/divergences-from-go.md)):

```csharp
new Interval(0, 0)                                            // [0,0]
    .Overlaps(new Interval(0, 7, Bound.Open, Bound.Closed));  // false, no shared point
new Interval(0, 0)
    .Touches(new Interval(0, 7, Bound.Open, Bound.Closed));   // true, they meet at 0
Interval.Intersection(new Interval(0, 5), new Interval(0, 5, Bound.Closed, Bound.Open));
                                                              // [0,5), the stricter bound
```

Reading it as point sets removes the doubt: the intersection of `[0,0]` and
`(0,7]` is empty, because the only candidate point, 0, is in the first and not
the second.

## `Meets` is the predicate `Normalize` actually needs

`Overlaps` ("shares a point") and `Touches` ("meets at an endpoint, shares
nothing") are two different questions, and neither alone answers "can these be
written as one interval?". Their disjunction does, and it has a name:

```csharp
public bool Meets(Interval other) => Overlaps(other) || Touches(other);
```

`Normalize` merges exactly when `Meets` holds. That is why the fix described
above cannot change a materialized set: moving a degenerate case from one side of
the disjunction to the other leaves the disjunction unchanged.

## Why a sweep

`Difference` and the conflict-resolution strategies all reduce to one question:
*along the line, which interval is in charge here?* Answering it by subtraction
would be arithmetic on endpoints; answering it by repeated merges would be
quadratic. The implementation answers it with a sweep over the **doubled line**:
every real coordinate appears twice, once as the point itself and once as the
point just after it. On that line, a closed endpoint includes its point and an
open one does not, and there are no special cases left to get wrong.

`Intervals.Sweep` exposes the machinery, with the priority left to the caller —
"higher index wins" is last-write-wins in miniature. The strategies pass
`(timestamp, id)` order in instead.

## What is *not* decided here

Convergence. This page is about one replica's algebra: given a set of intervals,
which points are covered. Turning a history of operations into that set is the
job of the strategies, which rank concurrent operations by `(timestamp, id)`.
Keeping that history durable and ordered is the job of the log engine; the
[API reference](../reference/api.md) has the signatures.
