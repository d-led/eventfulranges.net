#!/usr/bin/env bash
# Runs every test with coverage collection. Coverage is read by scripts/crap.py.
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf build/test-results
dotnet test EventfulRanges.sln \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory build/test-results
