#!/usr/bin/env bash
# Mutation testing with Stryker.NET. Fails if the mutation score drops below the
# break threshold in tests/EventfulRanges.Tests/stryker-config.json.
#
# Measured baseline: 77%, from 1505 mutants — 1147 killed, 39 timed out, 319
# survived. Of the survivors, 52 are argument guards Stryker reports with an empty
# `coveredBy`: it cannot attribute those statements to a test, so the tests that do
# exercise them cannot be credited. They are read as neither killed nor a gap. The
# break is set below the baseline so a regression still fails the run; the target is
# 80, and what stands in the way is the bookkeeping cluster below plus those
# unattributed guards, not a missing test anyone can write.
#
# This is not part of quality-gate.sh: a full run is tens of minutes, and a
# surviving mutant is a question rather than a failure — it either names a test
# that is missing or a mutant no test can kill. Run it before a release, and read
# the report rather than only the number.
#
# The survivors that are real questions are in the adjacent merge's indexes
# (BoxMerger, CoverGrid): a capacity hint, the order of the candidate buckets,
# the width of a bucket key and the tie-break between two boxes that both cover a
# third change nothing a caller can observe, because the merge still answers the
# same cover and still joins the same metadata. Pinning them would mean testing
# the indexes instead of the merge, which is the opposite of what the property
# test against the pairwise scan is for. Everything that does change what a
# caller sees — a lost sort, an unreachable candidate, a swallowed box — is
# killed, so the bar is the scored threshold rather than a survivor count of
# zero.
set -euo pipefail
cd "$(dirname "$0")/../tests/EventfulRanges.Tests"

dotnet tool restore
dotnet tool run dotnet-stryker --config-file stryker-config.json
