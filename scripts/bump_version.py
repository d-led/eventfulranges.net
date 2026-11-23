#!/usr/bin/env python3
"""Bumps the package version and opens the matching CHANGELOG section.

    scripts/bump-version.sh patch           # 0.0.1 -> 0.0.2
    scripts/bump-version.sh 0.0.7           # explicit
    scripts/bump-version.sh 0.1.0 --parity  # only once n-dimensional parity is done

The project files are the single source of truth for the version: the release
workflow refuses a tag that does not match them, and both packages carry it.
"""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CSPROJS = [
    ROOT / "src" / "EventfulRanges" / "EventfulRanges.csproj",
    ROOT / "src" / "EventfulRanges.Kurrent" / "EventfulRanges.Kurrent.csproj",
]
CHANGELOG = ROOT / "CHANGELOG.md"

VERSION = re.compile(r"<Version>([^<]+)</Version>")
SEMVER = re.compile(r"^(\d+)\.(\d+)\.(\d+)$")


def fail(message: str) -> None:
    print(f"bump: {message}", file=sys.stderr)
    raise SystemExit(1)


def git(*args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=ROOT, check=True, capture_output=True, text=True
    ).stdout.strip()


def parse(text: str) -> tuple[int, int, int]:
    match = SEMVER.match(text)
    if not match:
        fail(f"'{text}' is not a plain X.Y.Z version")
    return (int(match.group(1)), int(match.group(2)), int(match.group(3)))


def resolve(request: str, current: tuple[int, int, int]) -> str:
    if request == "patch":
        major, minor, patch = current
        return f"{major}.{minor}.{patch + 1}"
    parse(request)
    return request


def open_changelog_section(version: str) -> None:
    changelog = CHANGELOG.read_text(encoding="utf-8")
    heading = f"## {version}"

    if heading in changelog:
        print(f"CHANGELOG.md already has {heading}")
        return

    first_release = re.search(r"^## ", changelog, flags=re.MULTILINE)
    if first_release is None:
        changelog = f"{changelog.rstrip()}\n\n{heading}\n"
    else:
        changelog = (
            f"{changelog[: first_release.start()]}{heading}\n\n{changelog[first_release.start() :]}"
        )

    CHANGELOG.write_text(changelog, encoding="utf-8")
    print(f"opened {heading} in CHANGELOG.md")


def main(argv: list[str]) -> int:
    requests = [argument for argument in argv[1:] if not argument.startswith("--")]
    parity = "--parity" in argv[1:]

    if len(requests) != 1:
        fail("usage: bump-version.sh <patch|X.Y.Z> [--parity]")

    text = CSPROJS[0].read_text(encoding="utf-8")
    found = VERSION.search(text)
    if not found:
        fail(f"no <Version> in {CSPROJS[0].relative_to(ROOT)}")

    current_text = found.group(1)
    current = parse(current_text)
    target_text = resolve(requests[0], current)
    target = parse(target_text)

    if target <= current:
        fail(f"{target_text} is not greater than the current {current_text}")

    if current[1] == 0 and target[1] >= 1 and not parity:
        fail(
            "0.1.0 is reserved for n-dimensional parity; "
            "pass --parity once the n-dimensional half is complete"
        )

    if git("status", "--porcelain", "--untracked-files=no"):
        fail("the working tree has uncommitted changes; bump on a clean tree")

    if git("tag", "--list", f"v{target_text}"):
        fail(f"tag v{target_text} already exists")

    for project in CSPROJS:
        project_text = project.read_text(encoding="utf-8")
        if f"<Version>{current_text}</Version>" not in project_text:
            fail(f"{project.relative_to(ROOT)} does not carry {current_text}")
        project.write_text(
            project_text.replace(
                f"<Version>{current_text}</Version>", f"<Version>{target_text}</Version>"
            ),
            encoding="utf-8",
        )
    open_changelog_section(target_text)

    print(f"version {current_text} -> {target_text}")
    print()
    print("next:")
    print(f"  git commit -am '{target_text}: ...'")
    print(f"  git tag v{target_text}")
    print("  git push && git push --tags")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
