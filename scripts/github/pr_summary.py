#!/usr/bin/env python3
"""Regenerate a pull request's auto-summary block from the pull request's own commits.

Run:  python -m scripts.github.pr_summary --pr <N> [--repo owner/name] [--dry-run]

The commit list comes from the GitHub API, never the local checkout, which may be on a
different branch than the pull request being summarised. Conventional commits are grouped
by type; a pull request that references several issues (``Closes``/``Refs`` trailers) gets
one block per issue in the template's format (``**Issue #N — Title:**``, the issue's first
Objective paragraph, then its commits). The result replaces the text between the
``<!-- auto-summary:start -->`` / ``<!-- auto-summary:end -->`` markers and nothing else.

Ported from ``paragon-stats/tools/pr-summary.cs``; the body format follows this
repository's ``.github/pull_request_template.md`` and ``.claude/rules/gitops.md``.
"""

from __future__ import annotations

import argparse
import json
import re
from collections.abc import Mapping, Sequence
from dataclasses import dataclass

from scripts.github.cli_utils import resolve_repo
from scripts.github.gh_cli import ActionableArgumentParser, GhRunner, run_actionable_main, run_json, run_text

START = "<!-- auto-summary:start -->"
END = "<!-- auto-summary:end -->"

_SUBJECT_RE = re.compile(r"^(?P<type>\w+)(?:\((?P<scope>[^)]+)\))?!?:\s*(?P<summary>.+)$")
_ISSUE_RE = re.compile(r"(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|refs?)\s*:?\s+#(\d+)", re.IGNORECASE)

# Conventional Commit types in the order they are listed; anything else lands under "Other"
# so that no commit on the pull request is ever left out of the summary.
_HEADINGS: tuple[tuple[str, str], ...] = (
    ("feat", "Features"),
    ("fix", "Bug Fixes"),
    ("perf", "Performance"),
    ("security", "Security"),
    ("revert", "Reverts"),
    ("refactor", "Refactor"),
    ("docs", "Documentation"),
    ("test", "Testing"),
    ("ci", "CI/CD"),
    ("build", "Build"),
    ("style", "Style"),
    ("chore", "Chores"),
)
_OTHER = "Other"


@dataclass(frozen=True)
class Commit:
    """One commit on the pull request, parsed as a Conventional Commit."""

    sha: str
    type: str
    scope: str | None
    summary: str
    issues: tuple[int, ...]


@dataclass(frozen=True)
class IssueInfo:
    """The parts of a linked issue the summary quotes."""

    number: int
    title: str
    objective: str
    acceptance: str


def split_message(message: str) -> tuple[str, str]:
    """Split a raw commit message into (subject, body); the body starts after the first blank line."""
    normalized = message.replace("\r\n", "\n").strip("\n")
    subject, newline, rest = normalized.partition("\n")
    return subject.strip(), rest.lstrip("\n") if newline else ""


def extract_issues(text: str) -> tuple[int, ...]:
    """Issue numbers named by ``Closes``/``Fixes``/``Resolves``/``Refs`` trailers, sorted, without duplicates."""
    return tuple(sorted({int(n) for n in _ISSUE_RE.findall(text)}))


def parse_commit(sha: str, subject: str, body: str) -> Commit:
    """Parse a subject and body into a :class:`Commit`; a non-conventional subject gets type ``other``."""
    match = _SUBJECT_RE.match(subject)
    if match is None:
        return Commit(sha, "other", None, subject, extract_issues(f"{subject}\n{body}"))
    return Commit(
        sha,
        match.group("type").lower(),
        match.group("scope"),
        match.group("summary"),
        extract_issues(f"{subject}\n{body}"),
    )


def section(body: str, label: str) -> str:
    """The content of the issue-form section headed ``label`` (``##`` or ``###``), or ``""``."""
    pattern = re.compile(
        r"^#{2,3}\s*" + re.escape(label) + r"\s*$(?P<content>.*?)(?=^#{1,3}\s|\Z)",
        re.IGNORECASE | re.MULTILINE | re.DOTALL,
    )
    match = pattern.search(body)
    return match.group("content").strip() if match else ""


def group_commits(commits: Sequence[Commit]) -> str:
    """Commits as ``####`` groups in the fixed type order, each line ``- **scope:** summary (sha)``."""
    buckets: dict[str, list[Commit]] = {heading: [] for _, heading in _HEADINGS}
    buckets[_OTHER] = []
    by_type = dict(_HEADINGS)
    for commit in commits:
        buckets[by_type.get(commit.type, _OTHER)].append(commit)
    out: list[str] = []
    for heading, group in buckets.items():
        if not group:
            continue
        out.append(f"#### {heading}")
        out.extend(f"- {f'**{c.scope}:** ' if c.scope else ''}{c.summary} ({c.sha})" for c in group)
        out.append("")
    return "\n".join(out)


def _first_paragraph(text: str) -> str:
    return text.split("\n\n", 1)[0].strip()


def _issue_block(issue: IssueInfo, commits: Sequence[Commit]) -> str:
    # An issue only appears here because a commit references it, so its group is never empty.
    lines = [f"**Issue #{issue.number} — {issue.title}:**"]
    if issue.objective:
        lines.append(_first_paragraph(issue.objective))
    lines.extend(("", group_commits(commits).rstrip()))
    return "\n".join(lines)


def build_summary(commits: Sequence[Commit], issues: Mapping[int, IssueInfo]) -> str:
    """The text between the markers: per-issue blocks for two or more issues, otherwise one flat list."""
    if len(issues) >= 2:
        blocks = [
            _issue_block(issue, [c for c in commits if issue.number in c.issues]) for _, issue in sorted(issues.items())
        ]
        orphans = [c for c in commits if not c.issues]
        if orphans:
            blocks.append("**Other changes:**\n\n" + group_commits(orphans).rstrip())
        return "\n\n".join(blocks).strip()
    parts: list[str] = []
    single = next(iter(issues.values()), None)
    if single is not None and single.objective:
        parts.append(_first_paragraph(single.objective))
    parts.append(group_commits(commits).rstrip())
    return "\n\n".join(p for p in parts if p).strip()


def replace_markers(body: str, summary: str) -> str:
    """``body`` with the text between the markers replaced; a body without markers is returned as is."""
    pattern = re.compile(re.escape(START) + ".*?" + re.escape(END), re.DOTALL)
    return pattern.sub(lambda _: f"{START}\n{summary}\n{END}", body, count=1)


_COMMITS_JQ = ".[] | select((.parents|length) < 2) | [.sha[0:7], .commit.message] | @json"


def fetch_commits(runner: GhRunner, repo: str, pr: int) -> list[Commit]:
    """The pull request's non-merge commits, oldest first, from the API (paginated)."""
    raw = run_text(runner, ["gh", "api", "--paginate", f"repos/{repo}/pulls/{pr}/commits", "--jq", _COMMITS_JQ])
    commits: list[Commit] = []
    for line in raw.splitlines():
        if not line.strip():
            continue
        sha, message = json.loads(line)
        subject, body = split_message(message)
        commits.append(parse_commit(sha, subject, body))
    return commits


def fetch_issue(runner: GhRunner, repo: str, number: int) -> IssueInfo:
    """Title, Objective and Acceptance criteria of one issue."""
    data = run_json(runner, ["gh", "api", f"repos/{repo}/issues/{number}"])
    body = data.get("body") or ""
    return IssueInfo(number, data["title"], section(body, "Objective"), section(body, "Acceptance criteria"))


def _plural(count: int, word: str) -> str:
    return f"{count} {word}{'' if count == 1 else 's'}"


def build_parser() -> argparse.ArgumentParser:
    """CLI parser: ``--pr`` is required; ``--repo`` falls back to the environment and the checkout."""
    parser = ActionableArgumentParser(description="Regenerate a pull request's auto-summary from its commits")
    parser.add_argument("--repo", default=None, help="Repo owner/name (default: GITHUB_REPOSITORY, then gh repo view)")
    parser.add_argument("--pr", type=int, default=None, help="Pull request number")
    parser.add_argument("--dry-run", action="store_true", help="Print the updated body instead of writing it")
    return parser


def handle(args: argparse.Namespace, parser: argparse.ArgumentParser, runner: GhRunner) -> int:
    """Regenerate the summary for one pull request and write it back unless nothing changed."""
    if args.pr is None:
        raise ValueError("--pr is required")
    repo = resolve_repo(args, runner)
    commits = fetch_commits(runner, repo, args.pr)
    numbers = sorted({n for c in commits for n in c.issues})
    issues = {n: fetch_issue(runner, repo, n) for n in numbers}
    summary = build_summary(commits, issues)
    body = run_json(runner, ["gh", "api", f"repos/{repo}/pulls/{args.pr}"]).get("body") or ""
    updated = replace_markers(body, summary)
    if args.dry_run:
        print(updated)
        return 0
    if updated == body:
        print("No auto-summary markers found (or no change); PR body left untouched.")
        return 0
    run_text(runner, ["gh", "pr", "edit", str(args.pr), "--repo", repo, "--body-file", "-"], input_text=updated)
    print(f"Updated PR #{args.pr} auto-summary ({_plural(len(commits), 'commit')}, {_plural(len(issues), 'issue')}).")
    return 0


def main() -> int:
    """Entry point."""
    return run_actionable_main(
        build_parser=build_parser,
        handler=handle,
        examples=[
            "python -m scripts.github.pr_summary --pr 74",
            "python -m scripts.github.pr_summary --pr 74 --dry-run",
        ],
    )


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
