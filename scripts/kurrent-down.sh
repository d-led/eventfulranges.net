#!/usr/bin/env bash
# Stops the integration-test KurrentDB and drops its volumes.
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose down --volumes
