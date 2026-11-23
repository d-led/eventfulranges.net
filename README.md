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

## Start here

| You want to | Read |
| --- | --- |
| get a program running in five minutes | [Quickstart](docs/users/tutorials/quickstart.md) |
| solve a concrete task | [Model a calendar of busy days](docs/users/how-to/model-a-calendar.md) |
| look up a signature, default or exception | [API reference](docs/users/reference/api.md) |
| understand why it works this way | [Range algebra](docs/users/explanation/range-algebra.md) |
| work on the library | [Local development](docs/developers/tutorials/local-development.md) · [AGENTS.md](AGENTS.md) |
| cut a release | [Cut a release](docs/developers/how-to/cut-a-release.md) |
| know what the gate measures | [Why the gate looks like this](docs/developers/explanation/quality-model.md) |

[`llms.txt`](llms.txt) is the same map in the form an agent routes from.

## At a glance

```csharp
var busy = new Interval(1, 10).Subtract(new Interval(3, 5));                  // [1,3) and (5,10]
var shared = Interval.Intersection(new Interval(1, 10), new Interval(8, 20)); // [8,10]
```

Endpoints are `double`, compared and never combined, so no rounding error
accumulates. Inclusivity is per side: `[a,b]`, `[a,b)`, `(a,b]`, `(a,b)`. A
malformed interval is a value, not an exception: `Validate()` names the problem,
`EnsureValid()` throws where a well-formed one is required.

Sweeping is what conflict resolution will stand on: `Intervals.Sweep` partitions
the union of a set into maximal pieces, each labelled with the index of the
winner under a caller-supplied priority.

## Roadmap

| Version | Contents |
| --- | --- |
| `0.0.1` | `Interval`, `Intervals`, `Sweep` — the interval algebra |
| `0.0.x` | operations, clocks, strategies, the log engine, JSON Lines and memory stores, `RangeSet` |
| `0.0.x` | n-dimensional boxes, canonicalization, region queries, R-tree, `BoxSet`, retractions |
| `0.1.0` | **n-dimensional parity** — the whole library surface, and the first version worth pinning |

## Quality

One command, `./scripts/quality-gate.sh`: formatting, a warnings-as-errors build,
every test with coverage, and a CRAP budget of 10 per method. The laws are
checked against an independent brute-force oracle, not against the
implementation. [Why the gate looks like this](docs/developers/explanation/quality-model.md)
explains the numbers and what is deliberately not measured yet.

Install and release mechanics:

- `dotnet add package EventfulRanges` from nuget.org. Every tagged version is also
  mirrored to GitHub Packages (`https://nuget.pkg.github.com/d-led/index.json`),
  which requires credentials even for a public package.
- Publishing happens only from a version tag, through NuGet Trusted Publishing —
  no API key is stored or rotated.

## License

Mozilla Public License 2.0. A derived work of
[eventfulranges](https://github.com/d-led/eventfulranges); see [NOTICE](NOTICE).
