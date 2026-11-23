# AGENTS.md

EventfulRanges is a C#/.NET 10 port of the Go library
[eventfulranges](https://github.com/d-led/eventfulranges): a CRDT over
real-valued ranges. At `0.0.1` the package contains the interval algebra the
operation log and the n-dimensional half will be built on.

## Build, test, verify

```shell
dotnet build EventfulRanges.sln --configuration Release   # warnings are errors
dotnet test EventfulRanges.sln --configuration Release    # behaviour + property tests
./scripts/format.sh                                        # apply formatting
./scripts/quality-gate.sh                                  # the gate: must end "QUALITY GATE PASSED"
./scripts/bump-version.sh patch                            # version + changelog heading
./scripts/pack.sh                                          # build/packages/*.nupkg + *.snupkg
```

Requires the .NET 10 SDK (pinned in `global.json`) and `python3` for
`scripts/crap.py`.

## Codebase map

| Path | Contents |
| --- | --- |
| `src/EventfulRanges/Interval.cs` | the range type: validation, membership, overlap, merge, subtract, intersect |
| `src/EventfulRanges/Intervals.cs` | set operations and canonical order |
| `src/EventfulRanges/Intervals.Sweep.cs` | doubled-line sweep, lazy-deletion heap |
| `src/EventfulRanges/Bound.cs`, `IntervalError.cs`, `InvalidIntervalException.cs` | inclusivity, validation vocabulary |
| `src/EventfulRanges/Cover.cs` | one piece of a swept union |
| `tests/EventfulRanges.Tests/` | xUnit behaviour tests; `Properties/` holds CsCheck oracle tests; `Support/` holds helpers |
| `scripts/` | gate, CRAP, version bump, pack |
| `.github/workflows/ci.yml` | build + tests on `main` and pull requests; quality gate on dispatch |
| `.github/workflows/release.yml` | tag-driven publish |

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
- **`Meets` is the merge predicate.** `Normalize` merges exactly when
  `Overlaps || Touches`; do not add a second notion of adjacency.
- **Intervals are permissive values.** Constructors do not throw; `Validate`
  reports, `EnsureValid` throws. Property tests need to build malformed values.
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
- **Publishing only from a tag.** Never `dotnet nuget push` by hand; the release
  workflow does it with Trusted Publishing (no stored key).
- **License stays MPL-2.0** — a derived work; keep `NOTICE` accurate as more of
  the original is ported.
