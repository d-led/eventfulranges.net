# AGENTS.md

EventfulRanges is a C#/.NET 10 port of the Go library
[eventfulranges](https://github.com/d-led/eventfulranges): a CRDT over
real-valued ranges (`Interval`, `Strategies`, `Engine`, `RangeSet`) and over
n-dimensional boxes (`Box`, `BoxStrategies`, `BoxEngine`, `BoxSet`). `0.1.0` is
the parity release.

## Build, test, verify

```shell
dotnet build EventfulRanges.sln --configuration Release   # warnings are errors
dotnet test EventfulRanges.sln --configuration Release    # behaviour + property tests
./scripts/format.sh                                       # apply formatting
./scripts/quality-gate.sh                                 # the gate: must end "QUALITY GATE PASSED"
./scripts/mutation.sh                                     # Stryker.NET, tens of minutes
./scripts/bench.sh                                        # BenchmarkDotNet, minutes
./scripts/kurrent-up.sh && ./scripts/itest-kurrent.sh     # the server-backed tests
./scripts/bump-version.sh patch                           # version + changelog heading
./scripts/pack.sh                                         # build/packages/*.nupkg + *.snupkg
```

Requires the .NET 10 SDK (pinned in `global.json`) and `python3` for
`scripts/crap.py`. The KurrentDB tests also need Docker: without
`KURRENTDB_CONNECTION` they report as skipped, never as passed.

## Codebase map

| Path | Contents |
| --- | --- |
| `src/EventfulRanges/Interval.cs` | the range type: validation, membership, overlap, merge, subtract, intersect |
| `src/EventfulRanges/Intervals.cs`, `Intervals.Sweep.cs` | set operations, canonical order, the doubled-line sweep |
| `src/EventfulRanges/Bound.cs`, `IntervalError.cs`, `InvalidIntervalException.cs` | inclusivity and the validation vocabulary |
| `src/EventfulRanges/Cover.cs`, `Op.cs`, `OpKind.cs`, `OpError.cs` | swept pieces and the 1-D operation |
| `src/EventfulRanges/IClock.cs`, `HybridClock.cs`, `LamportClock.cs` | stamps |
| `src/EventfulRanges/StrategyKind.cs`, `Strategies.cs`, `Winner.cs`, `Segment.cs` | conflict resolution for ranges |
| `src/EventfulRanges/IEventLog.cs`, `MemoryLog.cs`, `JsonlLog.cs`, `Engine.cs`, `EngineOptions.cs` | the log contracts, two backends, the engine |
| `src/EventfulRanges/RangeSet.cs`, `JsonContext.cs` | the range facade and the source-generated JSON |
| `src/EventfulRanges/Box.cs`, `Boxes*.cs`, `Meta.cs`, `Canonicalizer.cs` | box geometry: normalize, exact difference, adjacent merge, components, path queries, metadata |
| `src/EventfulRanges/BoxOp.cs`, `BoxStrategies.cs`, `Layer.cs` | box operations, the five strategies, the painter's front |
| `src/EventfulRanges/IBoxLog.cs`, `BoxMemoryLog.cs`, `BoxJsonlLog.cs`, `BoxEngine.cs`, `BoxSet.cs` | the box stack and its facade |
| `src/EventfulRanges/BoxMerger.cs`, `CoverGrid.cs`, `RTree.cs` | the indexed adjacent merge and the static spatial index |
| `src/EventfulRanges.Kurrent/KurrentLog.cs` | the KurrentDB log |
| `tests/EventfulRanges.Tests/` | xUnit behaviour tests; `Properties/` holds CsCheck oracle tests; `Support/` holds helpers |
| `tests/EventfulRanges.Kurrent.Tests/` | the server-backed tests, skipped without `KURRENTDB_CONNECTION` |
| `benchmarks/EventfulRanges.Benchmarks/` | BenchmarkDotNet, the source of the merge threshold |
| `scripts/` | gate, CRAP, version bump, pack |
| `.github/workflows/ci.yml`, `release.yml` | build + tests; tag-driven publish |

## Documentation map (Diátaxis, two audiences)

| Quadrant | Users | Developers |
| --- | --- | --- |
| Tutorials | `docs/users/tutorials/quickstart.md` | `docs/developers/tutorials/local-development.md` |
| How-to | `docs/users/how-to/model-a-calendar.md` | `docs/developers/how-to/cut-a-release.md` |
| Reference | `docs/users/reference/api.md` | `docs/developers/reference/release-pipeline.md` |
| Explanation | `docs/users/explanation/range-algebra.md` | `docs/developers/explanation/` (quality model, port plan, divergences) |

`llms.txt` routes consumer agents; this file routes contributors.

## Invariants and conventions

- **One decision in one place.** The version lives only in
  `src/EventfulRanges/EventfulRanges.csproj` `<Version>`; the interval's
  inclusivity rule lives only in `Stricter` / `MoreClosed`; the canonical order
  lives only in `Intervals.Less`.
- **`Intervals.Less` is the only ordering.** Any new ordering must agree with it,
  or the sweep and normalization stop being deterministic.
- **`Meets` is the 1-D merge predicate.** `Normalize` merges exactly when
  `Overlaps || Touches`; do not add a second notion of adjacency.
- **Box emptiness is a value.** An inverted box, or one whose corners meet on an
  open face, is empty, and `Boxes.Normalize` drops it rather than rejecting it.
- **A replica is one space.** `BoxEngine` fixes the dimensionality on the first
  operation and rejects any other with `DimensionMismatchException`.
- **`AdditiveWinsLww` belongs to the n-D half.** `Strategies.Materialize`
  rejects it with `NotSupportedException`; `BoxStrategies.Layers` is a render
  recipe, not the cover.
- **Intervals are permissive values.** Constructors do not throw; `Validate`
  reports the problem, `EnsureValid` throws. Property tests need to build
  malformed values.
- **Endpoints are compared, never combined.** No arithmetic on coordinates, no
  `Math.FusedMultiplyAdd`, no tolerance windows.
- **Public members need XML documentation.** The build generates the doc file and
  fails without it.
- **Warnings are errors.** Suppress nothing without a comment; `CA1707` is
  suppressed in the test project only, because test names are sentences.
- **Methods stay under CRAP 10.** Split, test, or both.
- **Test names are sentences; assertions read as prose** (Shouldly). Behaviour,
  not implementation: a test that would pass if every method returned nothing is
  not a test.
- **Laws go in `Properties/`** and are checked against a brute-force oracle, not
  against the implementation.
- **Docs change with the public surface.** When a public type or member changes,
  update `docs/users/reference/api.md` in the same change, and keep `llms.txt`
  and this file's map accurate.
- **`README.md` uses absolute links only.** It is packed as the NuGet README and
  rendered on nuget.org, where a relative link resolves to nothing;
  `scripts/check-links.py` runs in the gate and enforces it.
- **The engine writes before it reads.** An operation reaches the view only after
  the log accepted it, and appends are serialized by one async gate; readers take
  only the short state lock.
- **A snapshot is a cache.** Reload seeds the known operations from it and
  rebuilds the view; never trust a persisted view instead of rebuilding it.
- **Wire names are part of the API.** They live on the types as JSON attributes
  and beside them in the `*Names` helpers; change both together, and add new wire
  types to `JsonContext`.
- **Publishing only from a tag.** Never `dotnet nuget push` by hand; the release
  workflow does it with Trusted Publishing (no stored key).
- **License stays MPL-2.0** — a derived work; keep `NOTICE` accurate as more of
  the original is ported.
