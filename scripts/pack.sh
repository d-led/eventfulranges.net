#!/usr/bin/env bash
# Builds the NuGet package and its symbol package into build/packages.
# Publishing happens only from a version tag, in .github/workflows/release.yml.
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf build/packages
for project in src/EventfulRanges/EventfulRanges.csproj src/EventfulRanges.Kurrent/EventfulRanges.Kurrent.csproj; do
    dotnet pack "${project}" \
        --configuration Release \
        --output build/packages \
        -p:ContinuousIntegrationBuild=true
done
