# EventfulRanges

[![CI](https://github.com/d-led/eventfulranges.net/actions/workflows/ci.yml/badge.svg)](https://github.com/d-led/eventfulranges.net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EventfulRanges.svg)](https://www.nuget.org/packages/EventfulRanges)
[![License: MPL-2.0](https://img.shields.io/badge/license-MPL--2.0-blue.svg)](LICENSE)

**A CRDT for real-valued ranges**, in C# on .NET 10. Any number of replicas add
and remove ranges on their own; every replica that has seen the same operations
ends up with the same set — no coordinator, no consensus, any arrival order.

A port of [eventfulranges](https://github.com/d-led/eventfulranges): the same
model, the same quality bar, an API that reads like C# rather than like Go.

> **Status: 0.0.1 — interval foundation.** Today the package ships the interval
> algebra the whole library stands on: per-side inclusivity, canonical sets,
> union, difference, and the sweep that decides conflict resolution. The
> operation log, the replication engine, and the n-dimensional half follow in
> `0.0.x`. **Nothing reaches 0.1 until n-dimensional parity.**

## Install

```shell
dotnet add package EventfulRanges --version 0.0.1
```

Every tagged version is also mirrored to GitHub Packages
(`https://nuget.pkg.github.com/d-led/index.json`). That feed requires credentials
even for a public package, so `dotnet add package` works anonymously only from
nuget.org; the mirror exists for CI and for keeping a copy of each tag.

Build from source:

```shell
dotnet build EventfulRanges.sln
```

## What you can do today

```csharp
using EventfulRanges;

var booked = new Interval(1, 10);                                   // [1,10]
var cancelled = new Interval(3, 5);                                 // [3,5]

var busy = booked.Subtract(cancelled);                              // [1,3) and (5,10]
var shared = Interval.Intersection(booked, new Interval(8, 20));    // [8,10]
var union = Intervals.Union(busy, new[] { new Interval(7, 12) });   // [1,3) and (5,12]
```

Endpoints are `double`, compared and never combined, so no rounding error
accumulates. Every interval carries inclusivity per side: `[a,b]`, `[a,b)`,
`(a,b]`, `(a,b)`. A malformed interval is a value, not an exception:
`Validate()` names the problem, and `EnsureValid()` throws where a well-formed
one is required.

Sweeping is what conflict resolution will stand on: `Intervals.Sweep` partitions
the union of a set into maximal pieces, each labelled with the index of the
winner under a caller-supplied priority.

```csharp
var covers = Intervals.Sweep(
    new[] { new Interval(1, 9), new Interval(3, 5) },
    better: (left, right) => left > right);   // [1,3) by 0, [3,5] by 1, (5,9] by 0
```

## Roadmap

| Version | Contents |
| --- | --- |
| `0.0.1` | `Interval`, `Intervals`, `Sweep` — the interval algebra |
| `0.0.x` | operations, clocks, strategies, the log engine, JSON Lines and memory stores, `RangeSet` |
| `0.0.x` | n-dimensional boxes, canonicalization, region queries, R-tree, `BoxSet`, retractions |
| `0.1.0` | **n-dimensional parity** — the whole library surface, and the first version worth pinning |

## Quality

One command: `./scripts/quality-gate.sh`. It runs formatting, a warnings-as-errors
build, every test with coverage, and the CRAP budget.

- **Tests specify behaviour.** Names read as sentences; assertions use Shouldly.
- **Property tests against an independent oracle.** CsCheck generates interval
  sets from a small integer grid — so shared endpoints, point intervals, and
  open/closed disagreements are common — and compares every answer with
  brute-force membership over an exhaustive probe grid.
- **CRAP ≤ 10 per method** (`scripts/crap.py`): complexity squared, punished
  cubically by missing coverage. A method must be simple, or tested, or both.
- **The port is not a transliteration.** Where the original and its documentation
  disagree, the port follows the documentation and records the divergence in
  [docs/DIVERGENCES.md](docs/DIVERGENCES.md).

## License

Mozilla Public License 2.0. A derived work of
[eventfulranges](https://github.com/d-led/eventfulranges); see [NOTICE](NOTICE).

## Releasing

Publishing happens only from a version tag, through NuGet Trusted Publishing —
no API key is stored or rotated. See [docs/RELEASING.md](docs/RELEASING.md).
