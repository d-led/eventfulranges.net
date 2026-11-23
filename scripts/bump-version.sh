#!/usr/bin/env bash
# Bumps the version and opens a CHANGELOG section. Publishing is separate: a tag triggers release.yml.
set -euo pipefail
cd "$(dirname "$0")/.."

python3 scripts/bump_version.py "$@"
