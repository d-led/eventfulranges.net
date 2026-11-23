# Work on the library locally

From a fresh clone to a green quality gate, then knowing where a change goes.
Follow it once; after that, `./scripts/quality-gate.sh` is the loop.

## 1. Prerequisites

| Tool | Why |
| --- | --- |
| .NET 10 SDK | `global.json` pins `10.0.403` with `latestFeature` roll-forward |
| `python3` | `scripts/crap.py`, the complexity budget |
| `git` | the version bump refuses to run outside a clean tree |

## 2. Build and test

```shell
dotnet build EventfulRanges.sln --configuration Release
dotnet test EventfulRanges.sln --configuration Release
```

The build has no warnings: `TreatWarningsAsErrors` is on, and the analyzers run
at `latest-recommended`. The test run ends with `Passed!` and a count.

## 3. Run the gate

```shell
./scripts/quality-gate.sh
```

It checks formatting (`dotnet format --verify-no-changes`), builds, runs the
tests with coverage, then applies the CRAP budget. It ends with
`QUALITY GATE PASSED`. Nothing else needs to be remembered; this is also what the
release workflow runs before a package is packed.

## 4. Add a behaviour

The example: `Interval.Meets` answers "is the union of these two contiguous?".
Suppose it did not exist.

1. **Write the failing test.** In `tests/EventfulRanges.Tests/IntervalTests.cs`,
   name it after the sentence you would say out loud:

   ```csharp
   [Fact]
   public void Meets_is_true_for_overlapping_and_touching_intervals()
   {
       var half = new Interval(1, 5, Bound.Closed, Bound.Open);

       half.Meets(new Interval(3, 9)).ShouldBeTrue();
       half.Meets(new Interval(5, 9)).ShouldBeTrue();
       half.Meets(new Interval(7, 9)).ShouldBeFalse();
   }
   ```

2. **Watch it fail** for the reason you expect: `dotnet test` reports
   `Interval does not contain a definition for 'Meets'`.

3. **Make it pass** with the smallest change in `src/EventfulRanges/Interval.cs`.

4. **Run the gate.** If the method is now complex, the gate says so — that is the
   [quality model](../explanation/quality-model.md) doing its job.

When the rule must hold for *all* inputs, put it in
`tests/EventfulRanges.Tests/Properties/IntervalAlgebraProperties.cs` instead and
compare against a brute-force answer, not against the implementation.

## 5. Where things live

| Path | Contents |
| --- | --- |
| `src/EventfulRanges/Interval.cs` | the range type and its algebra |
| `src/EventfulRanges/Intervals.cs`, `Intervals.Sweep.cs` | set operations and the doubled-line sweep |
| `src/EventfulRanges/Bound.cs`, `IntervalError.cs`, `InvalidIntervalException.cs` | inclusivity and the validation vocabulary |
| `src/EventfulRanges/Op.cs`, `Strategies.cs`, `Engine.cs`, `RangeSet.cs` | the 1-D stack, from operation to facade |
| `src/EventfulRanges/Box.cs`, `Boxes*.cs`, `Canonicalizer.cs` | box geometry: normalize, difference, adjacent merge, components, path queries |
| `src/EventfulRanges/BoxOp.cs`, `BoxStrategies.cs`, `BoxEngine.cs`, `BoxSet.cs` | the n-D stack, from operation to facade |
| `src/EventfulRanges/BoxMerger.cs`, `CoverGrid.cs`, `RTree.cs` | the indexed merge and the spatial index |
| `tests/EventfulRanges.Tests/` | behaviour tests; `Properties/` holds oracle tests |
| `scripts/` | gate, CRAP, version bump, packing |
| `docs/users/`, `docs/developers/` | the four documentation quadrants |

## 6. What will block you, on purpose

- **Warnings are errors.** Fix the code, do not suppress the analyzer; the only
  suppression in the repository is `CA1707` in the test project, because test
  names are sentences.
- **Every public member has XML documentation.** The build generates the
  documentation file, so a missing summary is an error.
- **Every method stays under CRAP 10.** Split it, test it, or both.
- **`0.1.0` needs `--parity`.** The bump script refuses to cross from `0.0.x`
  into a `0.1+` minor without `--parity`, the flag that marked the parity
  release.
