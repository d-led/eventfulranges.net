#!/usr/bin/env bash
# Starts a single-node KurrentDB for the integration tests.
#
# The image is amd64; on Apple silicon Docker runs it under emulation, which is
# slower but behaves the same. The store keeps no data that matters, so the
# volumes are throwaway.
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose up -d --wait
echo "KurrentDB is up: set KURRENTDB_CONNECTION='esdb://localhost:2113?tls=false'"
