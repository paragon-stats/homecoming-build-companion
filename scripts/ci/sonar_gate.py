#!/usr/bin/env python3
"""Fail the ``sonar`` job while a gated rule still has open findings.

SonarQube Cloud's pull-request analysis reports only issues a pull request introduces, so a
PR that leaves an old finding in place still shows zero. This gate reads the rules in
``scripts/ci/sonar_gate_rules/*.txt`` (one ``<rule-key> <path-prefix>`` per line, ``#`` starts
a comment) and checks them against:

- the analyzer results the scanner writes to ``.sonarqube/out/*/Issues.json`` during the build
    (``csharpsquid`` rules; every analysed file, old or new, so this is a full proof);
- on a pull request, the findings open on ``main`` (each must sit in a file, or for most
    rules on a line, the PR changes) and, for rules outside C#, the PR's own analysis;
- on a push to ``main``, the findings open on ``main``.

Run as ``python -m scripts.ci.sonar_gate --rules-dir DIR --sarif-root DIR [--pull-request N]``.
Exit codes: 0 pass, 1 findings or missing scanner output, 2 malformed rules file.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import urllib.request
from collections.abc import Callable, Iterable, Iterator
from pathlib import Path
from typing import Any, NamedTuple
from urllib.parse import unquote, urlencode, urlparse

PROJECT_KEY = "paragon-stats_homecoming-build-companion"
API_URL = "https://sonarcloud.io/api/issues/search"
PAGE_SIZE = 500
CSHARP = "csharpsquid:"
_HUNK = re.compile(r"^@@ -(\d+)(?:,(\d+))? \+\d+(?:,\d+)? @@")
_SONAR_CSHARP_ID = re.compile(r"S\d+")
_DRIVE = re.compile(r"/[A-Za-z]:")
_REF = re.compile(r"(?a)\w[\w./-]*")

Fetch = Callable[[str], dict[str, Any]]
GitDiff = Callable[[str], str]


class RuleFileError(Exception):
    """A rules file has a line that is not ``<rule-key> <path-prefix>``."""


class ScannerOutputError(Exception):
    """The scanner's analyzer results are missing, unreadable or empty."""


class Entry(NamedTuple):
    rule: str
    prefix: str


class Finding(NamedTuple):
    rule: str
    path: str
    line: int


class Scan(NamedTuple):
    files: int
    projects: int
    total: int
    findings: set[Finding]


class Diff(NamedTuple):
    files: set[str]
    old_lines: dict[str, set[int]]


def parse_rules(rules_dir: Path) -> list[Entry]:
    """Read every ``*.txt`` in the directory; raise ``RuleFileError`` naming file and line."""
    entries: list[Entry] = []
    for file in sorted(rules_dir.glob("*.txt")):
        for number, raw in enumerate(file.read_text(encoding="utf-8").splitlines(), 1):
            text = raw.split("#", 1)[0].strip()
            if not text:
                continue
            parts = text.split()
            if len(parts) != 2 or ":" not in parts[0]:
                raise RuleFileError(f"{file}:{number}: expected '<rule-key> <path-prefix>', got {raw!r}")
            entries.append(Entry(parts[0], parts[1]))
    return entries


def _repo_path(uri: str, root: str) -> str:
    path = unquote(urlparse(uri).path)
    if _DRIVE.match(path):
        path = path[1:]
    if path.casefold().startswith(root.casefold() + "/"):
        path = path[len(root) + 1 :]
    return path


def _location(result: dict[str, Any]) -> tuple[str, int]:
    first = (result.get("locations") or [{}])[0]
    # SARIF 1.0 (Roslyn's default) nests the file under resultFile; 2.1 under physicalLocation.
    place = first.get("resultFile") or first.get("physicalLocation", {})
    uri = place.get("uri") or place.get("artifactLocation", {}).get("uri", "")
    return uri, place.get("region", {}).get("startLine", 0)


def _results(file: Path) -> Iterator[dict[str, Any]]:
    try:
        document = json.loads(file.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError) as error:
        raise ScannerOutputError(f"{file}: unreadable ({error})") from error
    runs = document.get("runs") if isinstance(document, dict) else None
    if not isinstance(runs, list) or not runs:
        raise ScannerOutputError(f"{file}: not SARIF (no runs)")
    for run in runs:
        yield from run.get("results") or []


def _expected_projects(sarif_root: Path) -> list[Path]:
    """Folders of the projects the scanner analysed: each writes a ``ProjectInfo.xml`` unless excluded."""
    infos = sarif_root.rglob("ProjectInfo.xml")
    return sorted(i.parent for i in infos if "<IsExcluded>true</IsExcluded>" not in i.read_text(encoding="utf-8-sig"))


def read_sarif(sarif_root: Path, repo_root: str) -> Scan:
    """Read every project's ``Issues.json``; fail when a file is absent or malformed, not when it is clean."""
    files = sorted(sarif_root.rglob("Issues.json"))
    if not files:
        raise ScannerOutputError(f"scanner output missing: no Issues.json under {sarif_root}")
    projects = _expected_projects(sarif_root)
    if not projects:
        raise ScannerOutputError(f"no ProjectInfo.xml under {sarif_root}: cannot tell which projects were analysed")
    absent = [p.name for p in projects if not (p / "Issues.json").is_file()]
    if absent:
        raise ScannerOutputError(f"Issues.json missing for {len(absent)} of {len(projects)} projects: {absent}")
    total = 0
    findings: set[Finding] = set()
    for file in files:
        for result in _results(file):
            total += 1
            rule = result.get("ruleId", "")
            if _SONAR_CSHARP_ID.fullmatch(rule):
                uri, line = _location(result)
                findings.add(Finding(CSHARP + rule, _repo_path(uri, repo_root), line))
    return Scan(len(files), len(projects), total, findings)


def fetch_issues(fetch: Fetch, rules: list[str], scope: dict[str, str]) -> list[Finding]:
    """Page through the open issues of the rules for a branch or pull request."""
    found: list[Finding] = []
    page = 1
    while True:
        query = {"componentKeys": PROJECT_KEY, "resolved": "false", "ps": PAGE_SIZE, "p": page}
        data = fetch(f"{API_URL}?{urlencode({**query, 'rules': ','.join(rules), **scope})}")
        issues = data["issues"]
        found += [Finding(i["rule"], i["component"].split(":", 1)[1], i.get("line", 0)) for i in issues]
        if not issues or len(found) >= data.get("total", 0):
            return found
        page += 1


def _diff_name(line: str) -> str:
    name = line[4:]
    return "" if name == "/dev/null" else name[2:]


def parse_diff(text: str) -> Diff:
    """Map ``git diff -U0`` output to the changed files and the changed or deleted old-side lines."""
    files: set[str] = set()
    old_lines: dict[str, set[int]] = {}
    current = ""
    in_header = False
    for line in text.splitlines():
        hunk = _HUNK.match(line)
        if line.startswith("diff --git"):
            in_header, current = True, ""
        elif in_header and line.startswith("--- "):
            current = _diff_name(line)
            files.add(current)
        elif in_header and line.startswith("+++ "):
            files.add(_diff_name(line))
        elif hunk:
            in_header = False
            count = 1 if hunk[2] is None else int(hunk[2])
            old_lines.setdefault(current, set()).update(range(int(hunk[1]), int(hunk[1]) + count))
    files.discard("")
    return Diff(files, old_lines)


def _hits(entry: Entry, findings: Iterable[Finding]) -> list[Finding]:
    return [f for f in findings if f.rule == entry.rule and f.path.startswith(entry.prefix)]


def _untouched(findings: list[Finding], diff: Diff) -> list[Finding]:
    """Findings the PR's diff does not reach: the file for C# and S3776, else the line."""
    result = []
    for finding in findings:
        file_level = finding.rule.startswith(CSHARP) or finding.rule.endswith(":S3776") or not finding.line
        touched = finding.path in diff.files and (file_level or finding.line in diff.old_lines.get(finding.path, ()))
        if not touched:
            result.append(finding)
    return result


def _http_json(url: str) -> dict[str, Any]:
    with urllib.request.urlopen(url, timeout=60) as response:
        data: dict[str, Any] = json.load(response)
    return data


def _git_diff(base: str) -> str:
    return subprocess.run(
        ["git", "diff", "-U0", "--no-renames", f"{base}...HEAD"], capture_output=True, text=True, check=True
    ).stdout


def _record(summary: dict[Entry, list[str]], failures: list[str], entry: Entry, check: str, bad: list[Finding]) -> None:
    summary[entry].append(f"{check}={len(bad)}")
    failures.extend(f"{check}: {f.rule} {f.path}:{f.line}" for f in bad)


def _evaluate(
    args: argparse.Namespace, entries: list[Entry], csharp: set[Finding], fetch: Fetch, git_diff: GitDiff
) -> tuple[dict[Entry, list[str]], list[str]]:
    pull = args.pull_request is not None
    rules = sorted({e.rule for e in entries})
    on_main = fetch_issues(fetch, rules, {"branch": "main"}) if entries else []
    other = [r for r in rules if not r.startswith(CSHARP)]
    on_pr = fetch_issues(fetch, other, {"pullRequest": str(args.pull_request)}) if pull and other else []
    diff = parse_diff(git_diff(args.base)) if pull and entries else Diff(set(), {})

    summary: dict[Entry, list[str]] = {e: [] for e in entries}
    failures: list[str] = []
    for entry in entries:
        in_csharp = entry.rule.startswith(CSHARP)
        if in_csharp:
            _record(summary, failures, entry, "scanner", _hits(entry, csharp))
        if not pull:
            _record(summary, failures, entry, "main", _hits(entry, on_main))
            continue
        _record(summary, failures, entry, "baseline", _untouched(_hits(entry, on_main), diff))
        if not in_csharp:
            _record(summary, failures, entry, "pull-request", _hits(entry, on_pr))
    return summary, failures


def run(args: argparse.Namespace, fetch: Fetch, git_diff: GitDiff) -> int:
    try:
        entries = parse_rules(args.rules_dir)
    except RuleFileError as error:
        print(error, file=sys.stderr)
        return 2
    try:
        scan = read_sarif(args.sarif_root, args.repo_root)
    except ScannerOutputError as error:
        print(f"FAIL {error}")
        return 1
    print(
        f"read {scan.files} Issues.json files for {scan.projects} expected projects: "
        f"{scan.total} results, {len(scan.findings)} csharpsquid"
    )
    summary, failures = _evaluate(args, entries, scan.findings, fetch, git_diff)
    for entry, counts in summary.items():
        print(f"{entry.rule} {entry.prefix} {' '.join(counts)}")
    for failure in failures:
        print(f"FAIL {failure}")
    return 1 if failures else 0


def main(argv: list[str] | None = None, *, fetch: Fetch = _http_json, git_diff: GitDiff = _git_diff) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--rules-dir", type=Path, required=True)
    parser.add_argument("--sarif-root", type=Path, required=True)
    parser.add_argument("--pull-request", type=int)
    parser.add_argument("--base", default="origin/main", type=lambda v: v if _REF.fullmatch(v) else parser.error(v))
    parser.add_argument("--repo-root", default=Path.cwd().as_posix())
    return run(parser.parse_args(argv), fetch, git_diff)


if __name__ == "__main__":
    sys.exit(main())
