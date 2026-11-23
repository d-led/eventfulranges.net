#!/usr/bin/env bash
# The CRAP gate: fails when a method's complexity is out of proportion to its tests. CI runs it.
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf coverage
mkdir -p build
if ! dotnet test EventfulRanges.sln --collect:"XPlat Code Coverage" --results-directory ./coverage >build/crap-test.log 2>&1; then
    tail -n 40 build/crap-test.log
    exit 1
fi
python3 scripts/crap.py
