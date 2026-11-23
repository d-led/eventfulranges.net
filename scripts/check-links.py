#!/usr/bin/env python3
"""Checks the documentation's links.

Every relative link must resolve on disk. `README.md` ships inside the NuGet
package and is rendered on nuget.org, where a relative link has nothing to
resolve against, so it may only use absolute URLs.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PACKED = {ROOT / "README.md"}
SKIP_DIRS = {"bin", "obj", "build", "coverage", ".git"}
LINK = re.compile(r"\]\(([^)\s]+)\)")
ABSOLUTE = ("http://", "https://", "mailto:", "#")


def markdown_files() -> list[Path]:
    files: list[Path] = []
    for path in ROOT.rglob("*"):
        if path.suffix not in {".md", ".txt"}:
            continue
        if any(part in SKIP_DIRS for part in path.relative_to(ROOT).parts):
            continue
        files.append(path)
    return sorted(files)


def main() -> int:
    broken: list[str] = []
    packaged: list[str] = []
    files = markdown_files()

    for path in files:
        relative_path = path.relative_to(ROOT)
        for target in LINK.findall(path.read_text(encoding="utf-8")):
            if target.startswith(ABSOLUTE):
                continue
            if path in PACKED:
                packaged.append(f"{relative_path} -> {target}")
                continue
            anchor = target.split("#")[0]
            if anchor and not (path.parent / anchor).resolve().exists():
                broken.append(f"{relative_path} -> {target}")

    for row in broken:
        print(f"broken link: {row}", file=sys.stderr)
    for row in packaged:
        print(f"the packaged README needs an absolute link: {row}", file=sys.stderr)

    print(
        f"checked {len(files)} files: {len(broken)} broken, "
        f"{len(packaged)} relative in the packaged README"
    )
    return 1 if broken or packaged else 0


if __name__ == "__main__":
    sys.exit(main())
