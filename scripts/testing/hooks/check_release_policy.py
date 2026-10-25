"""Fail when the release bump policy drifts from the org's commit-type fact-set.

The org keeps one file, ``commit-types.txt`` in ``paragon-stats/github-actions``, naming
every Conventional Commit type and the version bump it triggers. This repository restates
that policy twice: ``[tool.semantic_release.commit_parser_options]`` in ``pyproject.toml``,
which python-semantic-release reads when it cuts a release, and the allowlist in
``check_commit_message.py``, which every commit is linted against. Either copy drifting
means a commit that lints clean cuts the wrong version, or none. The commitlint workflow
runs this against the fact-set pinned to the same SHA as the release action.

Exit codes:
    0: both copies match the fact-set
    1: a copy drifted (one ``H-REL-001`` line per difference)
    2: the fact-set itself is unreadable (``E80``)

Usage (as a module, so the package import of the allowlist resolves from the repo root):
    python -m scripts.testing.hooks.check_release_policy <commit-types.txt> <pyproject.toml>
"""

from __future__ import annotations

import argparse
import sys
import tomllib
from pathlib import Path

from scripts.testing.hooks import check_commit_message


def load_fact_set(path: Path) -> dict[str, str]:
    """``{type: bump}`` from the fact-set file; ``#`` starts a comment, blank lines are skipped."""
    facts: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        cols = line.split("#", 1)[0].split()
        if not cols:
            continue
        if len(cols) != 2:
            raise ValueError(f"{path}: row {cols[0]!r} must be '<type> <bump>'")
        facts[cols[0]] = cols[1]
    return facts


def _diff(where: str, key: str, got: list[str], want: list[str]) -> str | None:
    if sorted(got) == sorted(want):
        return None
    missing = sorted(set(want) - set(got))
    extra = sorted(set(got) - set(want))
    detail = "; ".join(p for p in (f"missing {missing}" if missing else "", f"extra {extra}" if extra else "") if p)
    return f"{where}: error: H-REL-001: {key} drifted from commit-types.txt ({detail}); copy the fact-set's list"


def policy_errors(fact_set: Path, pyproject: Path) -> list[str]:
    """Every difference between the fact-set and the two local copies of the policy."""
    facts = load_fact_set(fact_set)
    with pyproject.open("rb") as fh:
        options = tomllib.load(fh)["tool"]["semantic_release"]["commit_parser_options"]
    wanted = {
        "allowed_tags": sorted(facts),
        "minor_tags": sorted(t for t, bump in facts.items() if bump == "minor"),
        "patch_tags": sorted(t for t, bump in facts.items() if bump == "patch"),
    }
    errors = [_diff(f"{pyproject}:1:1", key, options[key], want) for key, want in wanted.items()]
    hook = Path(check_commit_message.__file__).name
    errors.append(_diff(f"{hook}:1:1", "_ALLOWED_TYPES", list(check_commit_message._ALLOWED_TYPES), sorted(facts)))
    return [e for e in errors if e]


def main(argv: list[str]) -> int:
    """Entry point."""
    parser = argparse.ArgumentParser(description="Check the release bump policy against the org fact-set")
    parser.add_argument("fact_set", type=Path, help="commit-types.txt from paragon-stats/github-actions")
    parser.add_argument("pyproject", type=Path, help="pyproject.toml holding [tool.semantic_release]")
    args = parser.parse_args(argv)
    try:
        errors = policy_errors(args.fact_set, args.pyproject)
    except ValueError as exc:
        print(
            f"{args.fact_set}:1:1: error: E80: fact-set unreadable: {exc}; fix the row in commit-types.txt",
            file=sys.stderr,
        )
        return 2
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    print(f"release policy matches {args.fact_set.name} ({len(load_fact_set(args.fact_set))} types)")
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main(sys.argv[1:]))
