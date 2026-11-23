#!/usr/bin/env bash
# The single quality gate: formatting, build, links, tests, and the CRAP budget.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== format and build"
./scripts/lint.sh

echo "== links"
python3 scripts/check-links.py

echo "== tests"
./scripts/test.sh

echo "== crap"
./scripts/crap.sh

echo "QUALITY GATE PASSED"
