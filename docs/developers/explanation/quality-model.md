# Why the gate looks like this

The library's value is that its answers are exact. A gate that only measured
line coverage would reward testing the easy branches, so this repository measures
**complexity against tests** instead.

## What runs

```shell
./scripts/quality-gate.sh
```

| Step | Script | Fails when |
| --- | --- | --- |
| format | `lint.sh` | any file differs from `dotnet format` |
| build | `lint.sh` | any warning — warnings are errors |
| tests + coverage | `test.sh` | any test fails |
| complexity budget | `crap.sh` → `crap.py` | any method scores above 10 |

The same script runs before a package is packed, so a release cannot be greener
than a pull request.

## CRAP, and why 10

```text
CRAP(m) = complexity(m)² · (1 − coverage(m))³ + complexity(m)
```

With complete coverage, the score is the method's complexity. Without tests,
complexity is punished cubically. A method at complexity 12 that nothing tests
scores 1,740; the same method with tests scores 12 and still fails.

Ten is the budget because a function that needs more than ten independent paths
is usually two functions wearing one name. When the gate fails, the three honest
answers are: split it, test it, or both. A method deliberately kept as a table of
one-line cases can be named in `DISPATCH_TABLES` inside `crap.py` — it is then
printed but not counted, so a table that grows into logic cannot hide.

The current numbers: 313 tests, 407 methods measured, 10 uncovered, average CRAP
2.6, nothing over budget. The uncovered ones are `ToString`/`Equals` overrides and
the rare error path of a log that has already moved on.

## Behaviour tests

Test names are sentences, and assertions read as prose:

```csharp
new Interval(0, 0)
    .Touches(new Interval(0, 7, Bound.Open, Bound.Closed))
    .ShouldBeTrue();
```

`CA1707` (no underscores in identifiers) is suppressed in the test project alone,
because those underscores are the word separators of a sentence. Everywhere else
the analyzer stands.

## Property tests against an oracle

Coverage says which lines ran. It says nothing about whether the answers are
right, so the algebraic laws are checked against a brute-force implementation:

- Generators build intervals from a **small integer grid** (`0..10`) with random
  bounds. Degeneracies — shared endpoints, point intervals, open/closed
  disagreements — are then common rather than rare.
- The oracle is membership at a set of **probe points**: every integer on the
  grid plus the point just after it. Because coverage can only change at an
  endpoint, and every endpoint is an integer, probing those is exhaustive — not a
  sample.
- Properties compare the library with the oracle (`Normalize` preserves
  coverage, `Union` covers exactly the points either input covers, `Difference`
  covers exactly the points of the first not in the second), plus the laws that
  do not need an oracle at all: idempotence, order independence, sortedness,
  disjointness, and the winner named by `Sweep`.

The oracle is written independently, in the test project. An implementation that
checked itself would prove nothing.

## What the gate does not do yet

Named plainly, so nobody mistakes green for finished:

- **No mutation testing.** `Stryker.NET` at break-80 is planned for the strategy
  layer, where equivalent mutants are the interesting question.
- **No coverage percentage gate.** Coverage is reported and read, not enforced; a
  method with no test already fails through CRAP.
- **No benchmarks.** The sweep and the indexed merge will be measured with
  `BenchmarkDotNet` when the merge path lands; until a threshold is measured, a
  number in the repository would be a guess.
- **No culture matrix.** One test pins `ToString` under `de-DE` and invariant
  culture is used explicitly everywhere, which is cheaper than a matrix and
  catches the same class of bug.
