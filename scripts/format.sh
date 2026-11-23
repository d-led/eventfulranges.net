#!/usr/bin/env bash
# Applies the repository formatting rules.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet format EventfulRanges.sln
