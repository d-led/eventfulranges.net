#!/usr/bin/env python3
"""CRAP metric per method, from a coverlet cobertura report.

    CRAP(m) = complexity(m)^2 * (1 - coverage(m))^3 + complexity(m)

A method with coverage 1 is never worse than its complexity; a method with no
coverage is punished quadratically. Anything over 10 wants splitting, testing, or
both.

    dotnet test EventfulRanges.sln \
        --collect:"XPlat Code Coverage" --results-directory ./coverage
    python3 scripts/crap.py
"""

from __future__ import annotations

import glob
import sys
import xml.etree.ElementTree as ElementTree

THRESHOLD = 10.0
GENERATED = ("JsonContext", ".g.cs", "AssemblyInfo", "/obj/")
TESTS = ("/tests/",)

# Code whose tests need infrastructure the gate cannot guarantee. The KurrentDB
# backend's tests skip unless a server answers, so its coverage is a property of
# the machine rather than of the change: measuring it here would fail every run
# without Docker and flake on the ones with it. `scripts/itest-kurrent.sh` and
# the `kurrent` CI job are where it is measured, and the exclusion is printed so
# nobody mistakes the silence for a clean bill.
# Cobertura names the file relative to the project, so the marker has no leading
# slash.
INFRASTRUCTURE_BACKED = ("EventfulRanges.Kurrent/",)

# A library has no composition root: every part is measured where it lives.
COMPOSITION_ROOTS: tuple[str, ...] = ()

# Closed dispatch tables: one arm per case, one line of work in each arm. Their
# complexity is the length of the list, not tangled logic, and pulling the list
# apart hides the one thing a reader has to see at once — every case the code
# handles. They are still printed, so a table that grows into real logic does
# not hide here.
DISPATCH_TABLES: dict[str, str] = {}


def crap(complexity: int, coverage: float) -> float:
    return complexity**2 * (1.0 - coverage) ** 3 + complexity


def collect(report_path: str, only: str | None) -> dict[str, tuple[float, int, float, str]]:
    """One row per method; a method covered by any report counts as covered."""
    root = ElementTree.parse(report_path).getroot()
    found: dict[str, tuple[float, int, float, str]] = {}

    for cls in root.iter("class"):
        class_name = cls.get("name", "")
        source = cls.get("filename", "")
        short_name = class_name.split(".")[-1]

        if only and not class_name.startswith(only):
            continue
        if any(marker in source for marker in GENERATED):
            continue
        if any(marker in source for marker in TESTS):
            continue
        if any(marker in source for marker in INFRASTRUCTURE_BACKED):
            continue
        if short_name in COMPOSITION_ROOTS:
            continue

        for method in cls.iter("method"):
            method_name = method.get("name", "?")

            # The compiler emits members that are not ours to test.
            if method_name.startswith("!"):
                continue

            key = f"{short_name}.{method_name}"
            coverage = float(method.get("line-rate", "0"))
            complexity = int(method.get("complexity", "1"))
            previous = found.get(key)

            if previous is None or coverage > previous[2]:
                found[key] = (crap(complexity, coverage), complexity, coverage, source)

    return found


def report(report_paths: list[str], only: str | None) -> int:
    methods: dict[str, tuple[float, int, float, str]] = {}

    for path in report_paths:
        for key, row in collect(path, only).items():
            previous = methods.get(key)
            if previous is None or row[2] > previous[2]:
                methods[key] = row

    rows = sorted(
        (
            (score, complexity, coverage, key)
            for key, (score, complexity, coverage, _) in methods.items()
        ),
        reverse=True,
    )
    over = [row for row in rows if row[0] > THRESHOLD]
    flagged = [row for row in over if row[3] not in DISPATCH_TABLES]
    tables = [row for row in over if row[3] in DISPATCH_TABLES]
    average = sum(row[0] for row in rows) / len(rows) if rows else 0.0
    uncovered = sum(1 for row in rows if row[2] == 0.0)
    line_rate = sum(row[2] for row in rows) / len(rows) if rows else 0.0

    print(f"reports      {len(report_paths)}")
    print(f"methods      {len(rows)}   uncovered {uncovered}   average CRAP {average:.1f}")
    print(f"covered rate {line_rate:.1%} (method average)")
    print(f"over {THRESHOLD:>3.0f}     {len(flagged)}   named dispatch tables {len(tables)}")
    print(f"excluded     {len(INFRASTRUCTURE_BACKED)} infrastructure-backed module(s): tests need a server")
    print()

    for title, group in (("over budget", flagged), ("dispatch tables", tables)):
        if not group:
            continue

        print(f"{title}:")
        print(f"{'CRAP':>7}  {'cplx':>4}  {'cov':>6}  method")

        for score, complexity, coverage, key in group:
            print(f"{score:7.1f}  {complexity:4d}  {coverage:6.0%}  {key}")

            if key in DISPATCH_TABLES:
                print(f"{'':>22}  {DISPATCH_TABLES[key]}")

        print()

    return 1 if flagged else 0


def main(argv: list[str]) -> int:
    pattern = argv[1] if len(argv) > 1 else "coverage/**/coverage.cobertura.xml"
    only = argv[2] if len(argv) > 2 else "EventfulRanges"
    files = sorted(glob.glob(pattern, recursive=True))

    if not files:
        print(f"no coverage report matched {pattern}", file=sys.stderr)
        return 2

    return report(files, only)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
