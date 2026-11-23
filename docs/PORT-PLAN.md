# C# port plan — eventfulranges.net

Status: planning only, nothing built. Drafted 2026-11-23 from the Go source in
this repository.

**Goal.** A C#/.NET 10 port of the generic eventfulranges CRDT library and its
API, built to the same quality bar as the Go library (100% coverage, oracle
property tests, mutation efficacy ≥ 0.8, real-server integration test), and
published as **eventfulranges.net**.

**Out of scope** (per decision): `demo/web`, `demo/paint`, `demo/network`,
`demo/pubsub`, `demo/automerge`, `demo/go-automerge`, the WebAssembly build,
the Playwright suites, `pages.yml`, `goreleaser.yml`.

---

## 1. What has to be translated

Measured on this tree (production code, tests and demos excluded):

| Package | Go LOC | Contents |
| --- | ---: | --- |
| root facade | 323 | `RangeSet`, `BoxSet` |
| `interval` | 544 | bounds, set algebra, doubled-line sweep |
| `op` | 104 | 1-D operation |
| `clock` | 75 | hybrid + Lamport |
| `strategy` | 178 | LWW/FWW/AdditiveWins/GrowOnly |
| `engine` | 414 | log + cached view + snapshots |
| `store` | 55 | `Log`, `Snapshotter`, `Compactor` |
| `store/jsonl` | 261 | JSON Lines stream with embedded snapshots |
| `store/memory` | 70 | in-memory log |
| `store/kurrent` | 258 | KurrentDB backend (build-tagged) |
| `meta` | 85 | CRDT join on JSON objects |
| `rtree` | 275 | bulk-loaded median-split R-tree |
| `space` | 1 842 | box algebra, difference, canonicalizer, `MergeAdjacent` (scan + indexed), components, path |
| `space/op` | 139 | box operations incl. retract |
| `space/strategy` | 289 | n-D strategies, retractions, layers |
| `space/engine` | 525 | n-D engine |
| `space/store` (+ memory, jsonl) | 389 | n-D log |
| **total** | **≈ 5 826** | 120 exported top-level names, 65 types, ≈250 exported funcs+methods |

Tests: **≈ 8 968 LOC, 469 `Test`/`Fuzz`/`Example` functions**, all package-local.
They are the real specification of the port; port the assertions, not the syntax.

---

## 2. Effort

### Headline

| Scenario | Effort |
| --- | --- |
| Conservative, human-only translation | **80–90 engineer-days** (≈16–18 weeks solo) |
| Agent does the mechanical port under review (realistic here) | **45–60 working days** (≈9–12 weeks elapsed) |
| First publishable 1-D slice (`RangeSet`, memory + JSONL, NuGet, CI) | **3–4 weeks** → `0.0.x` on NuGet |
| Full n-D parity (`BoxSet`, retract, layers, path, rtree) | **+5–7 weeks** → `0.1.0` |

### Where the days go

| Area | Days |
| --- | ---: |
| 1-D interval geometry + differential tests | 8 |
| `op`, `clock` (`TimeProvider`), `strategy`, `meta` | 6 |
| 1-D engine, snapshot/compact, memory + JSONL stores, `RangeSet`, fixture parity | 10 |
| n-D box algebra (`space.go`, `set.go`, `canonical.go`, `components.go`) | 14 |
| `space/merge_index` indexed merger — *deferrable, performance only* | 8 |
| `space/path` (slab method, `Crossed`, `Traverse`) | 4 |
| n-D `op`/`strategy`/`engine`/stores + `BoxSet` | 12 |
| `rtree` | 4 |
| `store/kurrent` + real-server integration test | 5 |
| Quality gate: coverage, Stryker ≥ 0.8, analyzers, benchmarks | 6 |
| Docs (README, DESIGN/CRDT/N-DIM/EXTENSIONS, XML docs, docfx), samples, NuGet + CI | 5 |
| Buffer for the unknown (≈20%) | 8 |

The distribution is not proportional to LOC: the 1 842 lines of `space` carry
more risk than the 1 123 lines of 1-D engine + stores, because the box algebra
is where the boundary-inclusivity corner cases live.

### Why agent assistance compresses it unevenly

Mechanical work — stores, facades, options plumbing, XML docs, translating 469
test bodies — compresses 2–3×. The two places it does not compress:
`interval/sweep.go` (doubled-line sweep with lazy heap deletion) and
`space/merge_index.go` (buckets + pair heap + grid + disjoint fast path), where
the time goes into *discovering* which invariant a failing shrink violates.

---

## 3. Phases

| Phase | Days | Deliverable |
| --- | ---: | --- |
| **P0** decisions + scaffold | 2 | solution, `Directory.Build.props` (nullable, warnings-as-errors, `AnalysisLevel=latest-all`, `IsAotCompatible`, SourceLink), project layout, CI skeleton, and a `fixtures.sh` that runs the Go tests to emit golden `.jsonl` + `Ops()` JSON |
| **P1** 1-D core | 8 | `Interval`, `Bound`, `Intervals` (Normalize/Union/Difference/Less/Equal), `Sweep`, `Op`, `IClock` + `HybridClock(TimeProvider)`, `StrategyKind`, `meta.Union`; unit + CsCheck properties vs a brute-force oracle |
| **P2** 1-D engine + stores + facade | 10 | async engine (versioned append, catch-up/retry), snapshot/compact, memory + JSONL, `RangeSet`; golden-fixture parity; concurrent gossip convergence test |
| **P3** n-D geometry | 14 | `Box`, `Boxes`, axis-sweep `subtractBox`, `Canonicalizer` + `Chain`, `MergeAdjacent` via `scanMerge` only, `ConnectedComponents`, `Path`/`Span`/`Crossed`/`Traverse`; scan-vs-naive differential tests |
| **P4** n-D stack + `BoxSet` | 12 | n-D `Op` (with `Retract`), `AdditiveWinsLWW`, `Effective`, `Layers`, engine (dims guard, meta merge, canonicalizer seam), stores, facade |
| **P5** `rtree` | 4 | `Build`/`BuildRef`, `Search`/`SearchRef` |
| **P6** indexed merge *(conditional)* | 8 | port `merge_index.go`, hold it to `scanMerge`'s output, re-derive the 2048 threshold with BenchmarkDotNet |
| **P7** KurrentDB package | 5 | `EventfulRanges.Kurrent` against `KurrentDB.Client` 1.6.1; reuse this repo's `docker-compose.yml` for the integration test |
| **P8** quality gate | 6 | 100% line coverage per library project, Stryker break-at-80, analyzers clean, `dotnet format --verify-no-changes`, stress test under `--blame-hang` |
| **P9** docs + publish | 5 | translated deep-dive docs, XML doc comments, docfx site, hello/local/pubsub samples (`System.Threading.Channels`), the tag-triggered release workflow (GitHub Packages mirror, then nuget.org) |

Checkpoints: **`0.0.1`** is the interval foundation (shipped); `0.0.2` up to
`0.0.9` carry the engine, the stores, the facade and the n-dimensional half;
**`0.1.0` is the n-dimensional parity release**, and the first version worth
pinning.

Deliberate ordering choice: the 1-D library is a complete, shippable product on
its own (it is the "several writers, no lock" case in the README). Shipping it
first turns a 3-month project into a 3-week one with a real release, and puts
the riskiest code (`space`) after the quality machinery already exists.

---

## 4. Target API shape

The point of the port is *usability in C#*, not transliteration. Concretely:

```csharp
using EventfulRanges;

await using var set = await RangeSet.OpenAsync("./example", StrategyKind.Lww);

await set.AddAsync(1, 10);
await set.RemoveAsync(3, 5);              // [1,3) ∪ (5,10]

foreach (var iv in set.Ranges)
    Console.WriteLine(iv);                // [1,3) / (5,10]

await a.ApplyAllAsync(b.Ops);             // converge by exchanging ops, never state
```

Rules behind that sketch:

- **Value types where the Go type is a value.** `readonly record struct Interval`,
  `readonly record struct Span`, `sealed record Op`. Structural equality,
  `with` expressions, no defensive copies needed.
- **Async where I/O happens, sync where it does not.** Store contracts are
  `ValueTask` + `CancellationToken`; `Ranges`, `Ops`, `Contains`, `Overlaps`,
  `Boxes`, `Layers` stay synchronous (they read a cached view).
- **No `null` where `empty` is meant.** `IReadOnlyList<T>` returns, never `nil`.
- **Enums for the wire-visible closed sets**, with `[JsonStringEnumMemberName]`
  (`.NET 9+`) so `"closed"`, `"add"`, `"lww"` survive serialization.
- **`TimeProvider` instead of `time.Now()`** — a testability upgrade the Go
  version cannot have without an interface.
- **`EngineOptions` as a `sealed record`** instead of Go's functional options:
  `RangeSet.OpenAsync(dir, StrategyKind.Lww, new EngineOptions { SnapshotEvery = 50 })`.
- **Capability interfaces, not one fat one.** `IEventLog`, `ISnapshotStore`,
  `ICompactableLog`, `IEventStore : IEventLog, ISnapshotStore`.
- **Static classes for the free functions.** `Intervals.Normalize(...)`,
  `Boxes.Difference(...)` mirror `interval.Normalize` / `space.Difference`.
  Extension methods on `IReadOnlyList<Interval>` are the alternative; prefer the
  static class so the API stays discoverable and AOT-friendly.
- **`net10.0` only.** Multi-targeting `netstandard2.0` would cost
  `TimeProvider`, `PriorityQueue`, `Lock`, collection expressions and
  `[JsonStringEnumMemberName]` for a shrinking audience.
- **AOT/trimming-compatible**: source-generated `JsonSerializerContext` for all
  wire types, no reflection-based serialization, `IsAotCompatible=true`.

Preserve the one asymmetry that will bite anyone who assumes uniformity: a 1-D
interval's zero value is `[0,0]` with **both bounds closed**, while a `space.Box`
with `nil` bound slices is **half-open**. The port must keep this exactly.

---

## 5. Translation traps

These are the places where a faithful line-by-line port produces a subtly wrong
library.

### 5.1 Concurrency does not transliterate

The Go engine holds a write mutex **across** `store.Append` and retries on
`ErrVersionConflict` (up to three `catchUp` rounds). C# cannot hold a
`System.Threading.Lock` across `await`. Pick deliberately:

1. `SemaphoreSlim(1,1)` async lock around `ApplyAsync` — identical observable
   semantics, simplest to reason about and to test. **Recommended for v1.**
2. Optimistic: append outside the lock against `_version`, then take a short
   lock to fold, else catch up and retry.
3. Single-writer actor over a `Channel<Op>`.

Synchronous readers should not block behind I/O: keep the materialized view
behind a volatile reference swap, so `Ranges` only ever takes a short lock.

### 5.2 JSON must be byte-deterministic

- Go's `encoding/json` **sorts map keys** when marshalling; `System.Text.Json`
  writes dictionaries in insertion order. `meta.Union` marshals a map, so the
  C# implementation must sort keys (ordinal) or the scalar tie-break
  (`bytes.Compare(a, b) > 0`) resolves differently.
- The tie-break compares **raw serialized member bytes**, whitespace included.
  Compare `ReadOnlySpan<byte>` of the serialized member, not re-serialized
  nodes.
- Field names and `omitempty` semantics must match: `origin`, `ref`, `minBound`,
  `maxBound`, `meta`, `snapshot`.
- **Decision required:** is cross-language interop a goal (a Go replica and a
  C# replica sharing one `.jsonl` stream)? If yes, add a fixture-parity suite —
  golden files generated by the Go implementation, read and byte-compared in
  C#. That is also the cheapest possible confidence in the whole port. If no,
  only C#-internal determinism is needed and the JSON work drops by ~3 days.

### 5.3 Floating point and formatting

- Endpoints are **compared, never combined** — except in `Path.Span` (division)
  and `Path.Point` (multiply-add). IEEE 754 makes both correctly rounded on
  every .NET runtime; do **not** introduce `Math.FusedMultiplyAdd`.
- `ToString()` must use `CultureInfo.InvariantCulture`. Both Go (`%v`) and .NET
  print shortest round-trip doubles, so `1` stays `1`; pin this with a test that
  runs under a comma-decimal culture.

### 5.4 The three algorithmic set pieces

| Piece | Why it is risky | Mitigation |
| --- | --- | --- |
| `interval/sweep.go` | doubled-line "fine point" sweep with lazy heap deletion | port the `finePoint` model verbatim; differential test against brute force on a coordinate-compressed grid |
| `space/set.go` `subtractBox` | bounded per-axis worklist, bound-slice materialisation/dropping | pure function — port, then property-test that the result is disjoint, cover-preserving, and that `A \ B` re-united with `A ∩ B` is `A` |
| `space/merge_index.go` | 604 LOC of buckets + pair heap + grid + disjoint fast path | it is an **optimization**; `merge_scan.go` is the reference and both are held to the same output. Ship scan-only, add the indexed path only when a consumer sees >2k-box covers, and re-measure the threshold (2048 in Go) with BenchmarkDotNet |

### 5.5 Interfaces and seams

- `store.Log` + optional `Snapshotter`/`Compactor` via type assertion →
  capability interfaces tested with `is`.
- Go functional options → `sealed record EngineOptions` + `with`.
- `strategy.Strategy` enum stays an enum (`StrategyKind`) — the engine switches
  on it, so a class hierarchy would only add indirection.
- `meta.Merge` → a delegate; document the semilattice laws (commutative,
  associative, idempotent) as the contract, exactly as the Go doc comment does.
- `Canonicalizer` → a delegate over an immutable box list.

### 5.6 Licensing

The port is a **derived work** of MPL-2.0 code. The C# repository must stay
MPL-2.0 (file-level copyleft), with a `NOTICE` naming the original. It cannot be
relicensed to MIT or Apache-2.0.

---

## 6. Quality machinery, mapped

| Go | C# |
| --- | --- |
| `testify` | xUnit v3 (MTP) + Shouldly/AwesomeAssertions for language-like assertions |
| `pgregory.net/rapid` | **CsCheck 5** (targets `net10.0`, PCG shrinking, `SampleParallel` for the Jepsen-style gossip scenarios, `SampleModelBased` for oracle comparison). FsCheck 3.6 is the alternative |
| biogo interval-tree oracle | do **not** port it: use (a) an independently written brute-force reference in the test project and (b) golden fixtures from the Go implementation |
| `go test -fuzz` | no direct equivalent: SharpFuzz (libFuzzer) over JSONL parsing and `Path.Span`, or skip fuzzing in v1 and rely on CsCheck + fixtures |
| `gremlins` (≥0.8) | **Stryker.NET 5.1**, `thresholds: { high: 80, low: 60, break: 80 }` — same bar, same documented reason (equivalent mutants in capacity hints and guarded strict comparisons) |
| `golangci-lint` | built-in analyzers at `latest-all` + warnings-as-errors + `dotnet format --verify-no-changes` |
| `cover` at 100% | coverlet.msbuild `Threshold=100`, scoped per library project |
| Codecov | Codecov via `dotnet test --collect:XPlat Code Coverage` |
| `scripts/*.sh` | `build.sh` / `test.sh` / `quality-gate.sh` wrappers, so the README keeps saying one command |
| CI jobs (checks, quality-gate on dispatch, kurrent-itest, demos) | `build`, `test+coverage`, `quality-gate` (dispatch, 45 min), `kurrent-itest` (service container), `pack` — no demo jobs beyond the three console samples |

Two behaviours worth copying rather than inventing: the **manual-dispatch
quality gate** (keeps PR checks fast) and the **real-server integration job**
(no fakes for KurrentDB).

---

## 7. Open decisions

1. **Cross-language wire compatibility** with the Go library — same `.jsonl`
   stream, same op JSON bytes? (Yes: +~3 days for the fixture suite, and a
   much stronger guarantee. No: simpler JSON rules.)
2. **Package identity.** Is `eventfulranges.net` the repository/product name,
   with NuGet IDs `EventfulRanges` and `EventfulRanges.Kurrent`? (`EventfulRanges.Net`
   as a package ID would collide with the framework suffix convention.)
3. ~~**Milestone order**~~ — answered: version from `0.0.1` upwards, stay in
   `0.0.x` until n-dimensional parity, and release `0.1.0` then.
4. **License** — confirm MPL-2.0 for the port (derived work).
5. **`net10.0` only** — confirm no `netstandard2.0` audience is needed.
6. **Indexed merge** — now, or when a consumer actually needs >2k-box covers?

---

## 8. Risks, ranked

1. `space/merge_index.go` — highest complexity, lowest functional necessity.
   Defer it; the scan implementation is the reference.
2. Async append + convergence under concurrent writers — the Go lock cannot be
   transliterated; the `SemaphoreSlim` choice must be made explicitly and tested
   with a parallel gossip scenario, not a sequential one.
3. Byte-level JSON divergence (map ordering, raw-byte tie-break) silently
   breaking cross-language interop — only detectable with golden fixtures.
4. Mutation score in geometry code: many mutants are equivalent; keep the Go
   project's 0.8 threshold and its documented justification rather than chasing
   100% and weakening tests to get there.
5. Losing the oracle. The Go tests are only an oracle because they compare
   against an *independent* implementation. A C# port that tests itself against
   itself proves nothing — hence the brute-force reference and the Go fixtures.
6. NuGet ID availability and the publish secret — publish `0.0.1` early to claim
   the ID (it is permanent), keep `NUGET_API_KEY` in the repository secrets, and
   switch to OIDC Trusted Publishing only if the publisher gets registered.
