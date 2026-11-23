# Releasing

Publishing is a tag, never a console command. `git push --tags` is the whole
release.

## Version policy

- `0.0.x` while the library is being ported. `0.1.0` is the **n-dimensional
  parity** release and the first version worth pinning.
- The version lives in exactly one place: `<Version>` in
  `src/EventfulRanges/EventfulRanges.csproj`. The release workflow refuses a tag
  that does not match it.

## Cutting a release

```shell
./scripts/bump-version.sh patch     # or an explicit X.Y.Z
$EDITOR CHANGELOG.md                # write what changed and how it was proved
./scripts/quality-gate.sh
git commit -am "0.0.2: ..."
git tag v0.0.2
git push && git push --tags
```

`bump-version.sh` refuses to work on a dirty tree, refuses a version that does
not increase, refuses a tag that already exists, and refuses `0.1.x` without
`--parity`.

## What the tag does

`.github/workflows/release.yml`:

1. checks the tag against the project version;
2. runs the quality gate (format, warnings-as-errors build, tests, CRAP ≤ 10);
3. packs `.nupkg` and `.snupkg` into `build/packages`;
4. mirrors the package to GitHub Packages;
5. exchanges GitHub's OIDC token for a one-hour NuGet key and pushes to
   nuget.org.

Both feeds use `--skip-duplicate`, so re-running a failed release is safe and
idempotent.

## One-time setup

NuGet Publishing uses **Trusted Publishing**: no API key is stored anywhere.

On nuget.org → your username → **Trusted Publishing** → add a policy:

| Field | Value |
| --- | --- |
| Repository owner | `d-led` |
| Repository | `eventfulranges.net` |
| Workflow file | `release.yml` (file name only) |
| Environment | leave empty |

The `user:` in the login step is the nuget.org **profile name**, not an email.
If the profile name is not `d-led`, change it in `release.yml`.

Notes that matter:

- A policy on a private repository starts as *pending full activation* and is
  only good for 7 days until the first successful publish, which locks it to the
  repository's identity.
- A temporary key lasts one hour and one OIDC token yields exactly one key, so
  the login step sits right before the push, after the long quality gate.
- GitHub Packages is the authenticated mirror, not the install path: consumers
  need credentials there even for a public package. It has no symbol server, so
  `.snupkg` goes to nuget.org only.
