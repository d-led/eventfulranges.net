#!/usr/bin/env bash
# Builds the NuGet package and its symbol package into build/packages.
# Publishing happens only from a version tag, in .github/workflows/release.yml.
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf build/packages
dotnet pack src/EventfulRanges/EventfulRanges.csproj \
    --configuration Release \
    --output build/packages \
    -p:ContinuousIntegrationBuild=true
