#!/usr/bin/env python3
"""Drop results in generated build output from CodeQL SARIF files before upload.

CodeQL ignores ``paths-ignore`` for a language analysed with a build, so the C#
job writes its SARIF to a directory, runs this script over it
(``python -m scripts.ci.filter_sarif <directory>``) and uploads the result.
A result is removed when the path of its primary location has an ``obj``
segment (``src/Foo/obj/...``); ``Objects.cs`` and ``objects/`` stay.
Rules, artifacts and every other part of the file are left as written.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

_GENERATED = re.compile(r"(^|/)obj/")


def _is_generated(result: dict) -> bool:
    locations = result.get("locations") or [{}]
    uri = locations[0].get("physicalLocation", {}).get("artifactLocation", {}).get("uri", "")
    return bool(_GENERATED.search(uri))


def filter_file(path: Path) -> tuple[int, int]:
    """Rewrite one SARIF file; return (removed, remaining) result counts."""
    sarif = json.loads(path.read_text(encoding="utf-8"))
    removed = 0
    remaining = 0
    for run in sarif.get("runs", []):
        results = run.get("results", [])
        kept = [result for result in results if not _is_generated(result)]
        removed += len(results) - len(kept)
        remaining += len(kept)
        run["results"] = kept
    path.write_text(json.dumps(sarif), encoding="utf-8")
    return removed, remaining


def main(argv: list[str]) -> int:
    """Filter every ``*.sarif`` file in the directory named by ``argv[0]``."""
    files = sorted(Path(argv[0]).glob("*.sarif"))
    if not files:
        print(f"no SARIF file in {argv[0]}", file=sys.stderr)
        return 1
    for path in files:
        removed, remaining = filter_file(path)
        print(f"{path.name}: removed {removed} generated-file results, {remaining} remain")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
