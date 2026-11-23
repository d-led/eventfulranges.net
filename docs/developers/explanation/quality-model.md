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
one-line cases can be named in `DISPATCH_TABLES` inside `crap.py`. It is then
printed but not counted, so a table that grows into logic cannot hide.

At the `0.1.0` gate run: 380 tests, 482 methods measured, 28 uncovered, average
CRAP 2.7, nothing over budget. The uncovered ones are `ToString`/`Equals`
overrides, the rare error path of a log that has already moved on, and the
fifteen methods of `KurrentLog`. Their tests skip when no KurrentDB is reachable,
so the gate reports them as uncovered rather than as passed.

That last number is the one honest wrinkle in the gate: an unset
`KURRENTDB_CONNECTION` lowers coverage without failing anything. The CRAP budget
still catches a complex method with no test, and `scripts/itest-kurrent.sh` and
the `kurrent` CI job are what exercise the store for real.

## Mutation testing, and why it is not in the gate

```shell
./scripts/mutation.sh
```

Stryker.NET rewrites a comparison, a boundary or a whole statement and asks
whether any test notices. It answers a question the gate cannot: coverage proves a
line ran, mutation proves an assertion would have failed had the line been wrong.
It is a separate script because a full run takes tens of minutes, and because a
surviving mutant is a question — a missing test, or a mutant no test can kill.

At the `0.1.0` run the library scores **77%**: 1505 mutants, 1147 killed, 39
timed out, 319 survived. The interval arithmetic alone scores 92%, and the port's
first run of the whole library was 76%. That gap is a set of contract tests the
run named: the public surface refuses a null argument at 52 entry points, which
nothing had asserted before.

Reading the survivors matters more than the number. They fall into three groups:

- **Bookkeeping with no observable effect** (most of them): a dictionary capacity
  hint, the order of the candidate buckets, the exact width of a bucket key, which
  of two boxes that both cover a third receives its metadata, and whether a cover
  known to be disjoint still looks for boxes to swallow. The answer is the same
  either way. The property test against the pairwise scan holds the two merge
  implementations to it, and pinning those mutants would mean testing the index
  instead of the merge.
- **Argument guards Stryker cannot attribute.** 52 mutants of
  `ArgumentNullException.ThrowIfNull` come back with an empty `coveredBy`: the
  probe for a statement like this is not mapped to the tests that execute it, so
  the tests that do exercise the guard cannot be credited. They are neither killed
  nor a gap, and no test can change the report.
- **Real gaps, which were fixed rather than counted.** A lost sort in the merge
  result, a candidate the grid could not reach, and a box too wide to register.
  Each is now a test.

The break is set at 75, below the baseline, so the run fails on a regression while
the target of 80 stays honest about what it would take to reach.

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
- Properties compare the library with the oracle: `Normalize` preserves
  coverage, `Union` covers exactly the points either input covers, `Difference`
  covers exactly the points of the first that are not in the second. Further
  properties need no oracle at all: idempotence, order independence, sortedness,
  disjointness, and the winner named by `Sweep`.

The oracle is written independently, in the test project. An implementation that
checked itself would prove nothing.

## What the gate does not do yet

So nobody mistakes green for finished:

- **Mutation testing is not in the gate.** It runs, and it scores above the bar,
  but a full run is tens of minutes and its survivors need reading. See above.
- **No coverage percentage gate.** Coverage is reported and read, not enforced; a
  method with no test already fails through CRAP.
- **Benchmarks are reported, not enforced.** `scripts/bench.sh` measures the two
  merges on a partitioned grid; nothing fails if a change makes the indexed path
  three times slower. The threshold is a claim with a harness behind it, not a
  test.
- **The KurrentDB tests need a server.** They skip without one, so the gate alone
  cannot prove the store works; the `kurrent` CI job does.
- **No culture matrix.** One test pins `ToString` under `de-DE` and invariant
  culture is used explicitly everywhere, which is cheaper than a matrix and
  catches the same class of bug.
