#!/usr/bin/env bash
# Runs the benchmarks and leaves the report in build/reports/benchmarks.
#
# The numbers this produces are what Boxes.IndexedMergeThreshold is set from, so
# a change to either merge implementation should be measured before the constant
# is touched. A full run is a few minutes; --job short cuts it to a smoke test.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet run --project benchmarks/EventfulRanges.Benchmarks \
    --configuration Release \
    -- \
    --artifacts build/reports/benchmarks \
    "$@"
