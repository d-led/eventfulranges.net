# Cut a release

Publishing is a tag. There is no command that publishes from a developer
machine, and no API key to hold.

## Steps

```shell
./scripts/bump-version.sh patch     # or an explicit X.Y.Z
$EDITOR CHANGELOG.md                # say what changed and how it was proved
./scripts/quality-gate.sh
git commit -am "0.0.2: ..."
git tag v0.0.2
git push && git push --tags
```

The bump script updates the single source of truth (`<Version>` in the project
file) and opens the changelog section. It refuses to run on a dirty tree, refuses
a version that does not increase, refuses a tag that already exists, and refuses
`0.1.x` without `--parity` — `0.1.0` is the n-dimensional parity release.

The tag starts [the release pipeline](../reference/release-pipeline.md), which
checks the tag against the project version, runs the gate, packs, mirrors to
GitHub Packages and publishes to nuget.org.

## If the release fails

Read the failing step; every step names its own remedy.

| Failure | Remedy |
| --- | --- |
| `tag_version` ≠ `project_version` | the tag and `<Version>` disagree; bump, retag |
| Quality gate red | fix the code or the test; re-tag the same version after a new commit |
| `Token exchange failed (HTTP 401)` | the nuget.org policy does not match; see below |
| `409` / duplicate | `--skip-duplicate` already absorbs it; re-running is safe |
| GitHub Packages push fails | that feed is a mirror; nuget.org is the canonical one |

### `Token exchange failed (HTTP 401)`

```text
Make sure you are using the username of the policy creator, not the policy owner:
No matching trust policy owned by user 'X' was found.
```

nuget.org found no policy it could attribute to that profile. In order of
likelihood:

1. no policy exists yet;
2. a policy field differs — most often the workflow typed with its path
   (`.github/workflows/release.yml`) instead of the file name (`release.yml`), or
   a repository name that is not exact;
3. the policy belongs to an organization, so the login must name the profile that
   *created* it;
4. the policy's scope allows new *versions* only, and this is a new package.

Fix the policy, or set the `NUGET_USER` repository variable (Settings → Secrets
and variables → Actions → Variables), then re-run the failed job — no commit and
no new tag required, because the variable is read at run time:

```shell
gh run rerun <run-id> --failed
```

Nothing is published until that step succeeds, and `--skip-duplicate` makes every
retry safe.

## After it succeeds

1. `curl -s -o /dev/null -w '%{http_code}' https://api.nuget.org/v3-flatcontainer/eventfulranges/index.json`
   returns `200`, and the version appears in the index.
2. The GitHub Packages mirror lists the same version.
3. Bump the README's install snippet if it pins a version.
