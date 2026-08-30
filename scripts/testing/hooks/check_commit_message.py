"""Pre-commit commit-msg hook to validate conventional commit format.

Validates that commit messages follow the conventional commit specification
(``type(scope): summary`` or ``type: summary``). This ensures commit messages
are parseable by the auto-summary generator (``scripts.github.pr_auto_summary``).

Exit codes:
    0: Message is valid (conventional commit, Dependabot, or merge commit)
    1: Message does not follow conventional commit format

Usage (pre-commit commit-msg stage):
    python scripts/testing/hooks/check_commit_message.py <commit-msg-file>
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

_DEPENDABOT_RE = re.compile(r"^Bump (the\b|[a-z])", re.IGNORECASE)
_MERGE_RE = re.compile(r"^Merge ")
# Local adaptation from the kloehnwars-homelab import: the type set is an explicit
# allowlist rather than any lowercase word (upstream repo-template accepts `[a-z]+`).
#
# The list is the cross-repo SSOT, ci-templates/commit-types.yml, plus `build`.
# It previously held only 8 of these because it was transcribed from the prose in
# gitops.md rather than derived from that SSOT, which silently dropped `perf`,
# `security`, `revert` and `style` — all standard conventional-commit types
# (Angular convention, @commitlint/config-conventional, the Conventional Commits
# FAQ). `build` is kept as a local extra: it is not in the SSOT but is already in
# use here and is a config-conventional type.
#
# Ordered to mirror the SSOT: release-bearing types first (feat minor; fix, perf,
# security, revert patch), then the no-release housekeeping types.
#
# Note `revert` accepts a hand-written `revert(scope): summary`. It does NOT accept
# the message `git revert` generates by default, `Revert "<original subject>"` —
# that stays rejected, so a revert has to say what is being undone and why.
_ALLOWED_TYPES = (
    "feat",
    "fix",
    "perf",
    "security",
    "revert",
    "docs",
    "test",
    "ci",
    "chore",
    "style",
    "refactor",
    "build",
)
# The optional ``!`` is the Conventional Commits breaking-change marker, allowed after the
# type or after the scope (``feat!:`` and ``feat(engine)!:`` are both valid). It is not
# optional decoration: cliff.toml's [bump] section and the release flow in
# docs/automation/commit-message-workflow.md both key major/minor bumps off it, so a
# pattern that rejected it would block the exact message shape the release process asks
# for -- across the pre-commit hook, the commitlint job and the pr-title gate alike.
_CONVENTIONAL_RE = re.compile(rf"^({'|'.join(_ALLOWED_TYPES)})(\([^)]+\))?!?: .+")


def validate_commit_message(message: str) -> tuple[bool, str]:
    """Validate a commit message against conventional commit format.

    Returns (is_valid, reason).
    """
    lines = message.strip().splitlines()
    if not lines:
        return False, "Commit message is empty"

    subject = lines[0].strip()

    # Allow merge commits.
    if _MERGE_RE.match(subject):
        return True, "Merge commit"

    # Allow Dependabot commits.
    if _DEPENDABOT_RE.match(subject):
        return True, "Dependabot commit"

    # Validate conventional commit format.
    if _CONVENTIONAL_RE.match(subject):
        return True, "Conventional commit"

    return False, (
        f"Subject line does not follow conventional commit format: {subject!r}\n"
        "Expected: type(scope): summary  or  type: summary\n"
        f"Allowed types: {', '.join(_ALLOWED_TYPES)}\n"
        "Examples: feat(ci): add auto-summary, fix: resolve crash, docs: update README"
    )


def main() -> int:
    """Entry point for commit-msg hook."""
    if len(sys.argv) < 2:
        print("Usage: check_commit_message.py <commit-msg-file>", file=sys.stderr)
        return 2

    msg_path = Path(sys.argv[1])
    if not msg_path.exists():
        print(f"Commit message file not found: {msg_path}", file=sys.stderr)
        return 2

    message = msg_path.read_text(encoding="utf-8", errors="replace")
    is_valid, reason = validate_commit_message(message)

    if not is_valid:
        print(f"FAIL: {reason}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())  # pragma: no cover
