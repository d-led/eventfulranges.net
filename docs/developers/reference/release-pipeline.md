# Release pipeline

Facts about `.github/workflows/release.yml`. For the task, see
[cut a release](../how-to/cut-a-release.md).

## Trigger

| Event | Filter |
| --- | --- |
| `push` | tags matching `v*.*.*` only |

`main` is covered by `.github/workflows/ci.yml`; that workflow never publishes.

## Permissions

| Permission | Why |
| --- | --- |
| `contents: read` | checkout |
| `id-token: write` | the GitHub OIDC token that nuget.org exchanges for a key |
| `packages: write` | the GitHub Packages mirror |

## Steps, in order

| # | Step | Guarantee |
| --- | --- | --- |
| 1 | checkout (`fetch-depth: 0`) | full history for SourceLink |
| 2 | `actions/setup-dotnet@v4`, `10.0.x` | toolchain |
| 3 | version check | the tag minus `v` equals `<Version>` in `src/EventfulRanges/EventfulRanges.csproj`; any mismatch stops the run |
| 4 | `./scripts/quality-gate.sh` | format, warnings-as-errors build, tests, CRAP budget |
| 5 | `./scripts/pack.sh` | `build/packages/EventfulRanges.<version>.nupkg` and `.snupkg` |
| 6 | mirror to GitHub Packages | same `.nupkg` on the repository feed |
| 7 | `NuGet/login@v1` | short-lived nuget.org key, requested immediately before use |
| 8 | publish | `.nupkg` and `.snupkg` to nuget.org |

The mirror runs before the nuget.org push on purpose: if nuget.org rejects the
push, the bits are still reachable.

## Feeds

| Feed | Source | Contents | Auth for consumers |
| --- | --- | --- | --- |
| nuget.org | `https://api.nuget.org/v3/index.json` | `.nupkg` + `.snupkg` | none |
| GitHub Packages | `https://nuget.pkg.github.com/d-led/index.json` | `.nupkg` only | credentials, even for a public package |

GitHub's NuGet feed has no symbol server, which is why `.snupkg` goes to nuget.org
only.

## Credentials

nuget.org uses **Trusted Publishing**: no long-lived key is stored anywhere. The
workflow sends GitHub's signed OIDC token to `https://www.nuget.org/api/v2/token`
with audience `https://www.nuget.org`; nuget.org matches it against a policy and
returns a key valid for one hour. One OIDC token yields exactly one key.

The GitHub Packages mirror uses the built-in `GITHUB_TOKEN`, not a NuGet
credential.

### Policy on nuget.org

| Field | Value |
| --- | --- |
| Owner of the policy | the individual account (not an organization) |
| Repository owner | `d-led` |
| Repository | `eventfulranges.net` |
| Workflow file | `release.yml` (file name only) |
| Environment | empty |
| Scope | must allow publishing **new** packages |

### Repository configuration

| Kind | Name | Default | Effect |
| --- | --- | --- | --- |
| Variable | `NUGET_USER` | `d-led` | the profile named in the login step; the profile that created the policy, not an email |
| Secret | — | — | none required |

## Version policy

| Range | Meaning |
| --- | --- |
| `0.0.x` | the port in progress; every increment was a real, installable release |
| `0.1.x` | n-dimensional parity, and the first line worth pinning |
| `1.0.0` | not scheduled |

The version exists in exactly one place: `<Version>` in the project file.

## Package contents

| File | Present |
| --- | --- |
| `lib/net10.0/EventfulRanges.dll` | yes |
| `lib/net10.0/EventfulRanges.xml` | yes (XML documentation) |
| `README.md` | yes |
| `LICENSE` / license expression | `MPL-2.0` expression |
| SourceLink, deterministic build, embedded untracked sources | yes |
| Symbols | separate `.snupkg` |

## Not in the pipeline

- no demo or sample jobs;
- no documentation deployment;
- no automatic changelog generation;
- no KurrentDB integration tests (they run in the `kurrent` CI job);
- no mutation testing (`scripts/mutation.sh` runs Stryker.NET by hand, because a
  full run takes tens of minutes).
