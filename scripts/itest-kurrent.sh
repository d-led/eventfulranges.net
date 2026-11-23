#!/usr/bin/env bash
# The KurrentDB integration tests, against a server started by kurrent-up.sh.
#
# These are the only tests in the repository that need a running server, so they
# are not part of quality-gate.sh: the gate skips them when KURRENTDB_CONNECTION
# is unset, and this script is what runs them for real.
set -euo pipefail
cd "$(dirname "$0")/.."

export KURRENTDB_CONNECTION="${KURRENTDB_CONNECTION:-esdb://localhost:2113?tls=false}"

rm -rf build/test-results/kurrent
dotnet test tests/EventfulRanges.Kurrent.Tests \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory build/test-results/kurrent
