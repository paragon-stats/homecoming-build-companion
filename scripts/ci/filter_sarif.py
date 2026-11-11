#!/usr/bin/env python3
"""Drop results in generated build output from CodeQL SARIF files before upload.

CodeQL ignores ``paths-ignore`` for a language analysed with a build, so the C#
job writes its SARIF to a directory, runs this script over it
(``python -m scripts.ci.filter_sarif <directory>``) and uploads the result.
With ``--list-only`` nothing is rewritten, so the Python job, which uploads from ``analyze``, can print what remains.
Either way, one line per remaining result (rule id, path, start line) is printed.
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


def _describe(result: dict) -> str:
    location = (result.get("locations") or [{}])[0].get("physicalLocation", {})
    uri = location.get("artifactLocation", {}).get("uri", "")
    line = location.get("region", {}).get("startLine", 0)
    return f"{result.get('ruleId', '')} {uri}:{line}"


def filter_file(path: Path, *, rewrite: bool = True) -> tuple[int, list[str]]:
    """Filter one SARIF file; return the removed count and a sorted line per remaining result."""
    sarif = json.loads(path.read_text(encoding="utf-8"))
    removed = 0
    remaining: list[str] = []
    for run in sarif.get("runs", []):
        results = run.get("results", [])
        kept = [result for result in results if not _is_generated(result)]
        removed += len(results) - len(kept)
        remaining.extend(_describe(result) for result in kept)
        run["results"] = kept
    if rewrite:
        path.write_text(json.dumps(sarif), encoding="utf-8")
    return removed, sorted(remaining)


def main(argv: list[str]) -> int:
    """Filter every ``*.sarif`` file in the directory named by the one non-flag argument."""
    rewrite = "--list-only" not in argv
    directory = next(arg for arg in argv if arg != "--list-only")
    files = sorted(Path(directory).glob("*.sarif"))
    if not files:
        print(f"no SARIF file in {directory}", file=sys.stderr)
        return 1
    for path in files:
        removed, remaining = filter_file(path, rewrite=rewrite)
        verb = "removed" if rewrite else "would remove"
        print(f"{path.name}: {verb} {removed} generated-file results, {len(remaining)} remain")
        for line in remaining:
            print(f"  {line}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
