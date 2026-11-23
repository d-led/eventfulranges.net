#!/usr/bin/env bash
# Static gate: formatting is already applied, and the build has no warnings.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet format EventfulRanges.sln --verify-no-changes
dotnet build EventfulRanges.sln --configuration Release
