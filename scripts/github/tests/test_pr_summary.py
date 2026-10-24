"""Tests for scripts.github.pr_summary: the PR auto-summary regenerated from the PR's own commits."""

from __future__ import annotations

import argparse
import json
from typing import ClassVar

import pytest

from scripts.github.conftest import ExpectedCall, QueueRunner, make_call, make_runner
from scripts.github.gh_cli import GhResult
from scripts.github.pr_summary import (
    END,
    START,
    Commit,
    IssueInfo,
    build_parser,
    build_summary,
    extract_issues,
    fetch_commits,
    fetch_issue,
    group_commits,
    handle,
    main,
    parse_commit,
    replace_markers,
    section,
    split_message,
)

REPO = "owner/repo"


class TestSplitMessage:
    def test_subject_and_body(self) -> None:
        assert split_message("feat(core): add parser\n\nLong body.\nCloses #5\n") == (
            "feat(core): add parser",
            "Long body.\nCloses #5",
        )

    def test_subject_only(self) -> None:
        assert split_message("chore: subject only") == ("chore: subject only", "")

    def test_crlf_and_missing_blank_separator(self) -> None:
        assert split_message("fix: x\r\nRefs #9\r\n") == ("fix: x", "Refs #9")


class TestParseCommit:
    def test_conventional_with_scope(self) -> None:
        commit = parse_commit("aaa1111", "feat(core): parse damage events", "Closes #21")
        assert commit == Commit("aaa1111", "feat", "core", "parse damage events", (21,))

    def test_breaking_marker_and_no_scope(self) -> None:
        commit = parse_commit("bbb2222", "fix!: handle empty log", "")
        assert (commit.type, commit.scope, commit.summary, commit.issues) == ("fix", None, "handle empty log", ())

    def test_non_conventional_subject_is_other(self) -> None:
        commit = parse_commit("ccc3333", "Merge branch tidy-up", "")
        assert (commit.type, commit.summary) == ("other", "Merge branch tidy-up")


class TestExtractIssues:
    def test_every_trailer_form_and_dedup(self) -> None:
        text = "Closes #7\nrefs: #3\nFixes #7\nResolves #12\nRelates to #99 is not a trailer\nsee #4 is not either"
        assert extract_issues(text) == (3, 7, 12)


class TestSection:
    BODY = "### Type\n\nwork-package\n\n### Objective\n\nFirst paragraph.\n\nSecond paragraph.\n\n### Risk\n\nlow\n"

    def test_returns_the_section_content(self) -> None:
        assert section(self.BODY, "Objective") == "First paragraph.\n\nSecond paragraph."

    def test_missing_section_is_empty(self) -> None:
        assert section(self.BODY, "Acceptance criteria") == ""

    def test_last_section_runs_to_end(self) -> None:
        assert section(self.BODY, "Risk") == "low"


class TestGroupCommits:
    def test_groups_in_fixed_order_with_scope_and_sha(self) -> None:
        commits = [
            parse_commit("c1", "chore: tidy", ""),
            parse_commit("f1", "feat(core): parse", ""),
            parse_commit("x1", "revert(core): undo parse", ""),
            parse_commit("o1", "Merge something", ""),
            parse_commit("f2", "feat: second", ""),
        ]
        text = group_commits(commits)
        assert (
            text.index("#### Features")
            < text.index("#### Reverts")
            < text.index("#### Chores")
            < text.index("#### Other")
        )
        assert "- **core:** parse (f1)\n- second (f2)\n" in text
        assert "- Merge something (o1)" in text

    def test_empty_is_empty(self) -> None:
        assert group_commits([]) == ""


class TestBuildSummary:
    ISSUES: ClassVar[dict[int, IssueInfo]] = {
        21: IssueInfo(21, "Damage parsing", "Parse combat damage lines.\n\nMore detail.", "- [ ] totals correct"),
        22: IssueInfo(22, "CLI robustness", "", ""),
    }

    def test_multi_issue_uses_the_template_heading_and_first_objective_paragraph(self) -> None:
        commits = [
            parse_commit("aaa1111", "feat(core): parse damage events", "Closes #21"),
            parse_commit("bbb2222", "fix(cli): handle empty log", "Refs #22"),
            parse_commit("ccc3333", "chore: tidy", ""),
        ]
        out = build_summary(commits, self.ISSUES)
        assert out.startswith("**Issue #21 — Damage parsing:**\nParse combat damage lines.\n\n#### Features\n")
        assert "More detail." not in out
        assert "- [ ] totals correct" not in out
        assert "**Issue #22 — CLI robustness:**\n\n#### Bug Fixes\n- **cli:** handle empty log (bbb2222)" in out
        assert out.endswith("**Other changes:**\n\n#### Chores\n- tidy (ccc3333)")

    def test_multi_issue_without_orphans_has_no_other_block(self) -> None:
        commits = [
            parse_commit("aaa1111", "feat(core): parse damage events", "Closes #21"),
            parse_commit("bbb2222", "fix(cli): handle empty log", "Refs #22"),
        ]
        out = build_summary(commits, self.ISSUES)
        assert "Other changes" not in out
        assert out.endswith("- **cli:** handle empty log (bbb2222)")

    def test_single_issue_has_no_heading(self) -> None:
        commits = [parse_commit("aaa1111", "feat(core): parse damage events", "Closes #21")]
        out = build_summary(commits, {21: self.ISSUES[21]})
        assert out == "Parse combat damage lines.\n\n#### Features\n- **core:** parse damage events (aaa1111)"

    def test_single_issue_without_objective_is_commits_only(self) -> None:
        out = build_summary([parse_commit("b1", "fix(cli): x", "Refs #22")], {22: self.ISSUES[22]})
        assert out == "#### Bug Fixes\n- **cli:** x (b1)"

    def test_no_issue_is_commits_only(self) -> None:
        out = build_summary([parse_commit("c1", "chore: tidy", "")], {})
        assert out == "#### Chores\n- tidy (c1)"


class TestReplaceMarkers:
    def test_replaces_between_markers_only(self) -> None:
        body = f"x\n{START}\nOLD\n{END}\ny"
        assert replace_markers(body, "NEW") == f"x\n{START}\nNEW\n{END}\ny"

    def test_without_markers_body_is_unchanged(self) -> None:
        assert replace_markers("plain body", "NEW") == "plain body"


def _commit_lines(*records: tuple[str, str]) -> str:
    # A blank line between pages is how `gh api --paginate` output can look; it must be skipped.
    return "\n".join(json.dumps(list(r)) for r in records) + "\n\n"


COMMITS_ARGV = [
    "gh",
    "api",
    "--paginate",
    f"repos/{REPO}/pulls/74/commits",
    "--jq",
    ".[] | select((.parents|length) < 2) | [.sha[0:7], .commit.message] | @json",
]


class TestFetch:
    def test_fetch_commits_parses_one_record_per_line(self) -> None:
        runner = make_runner(
            make_call(COMMITS_ARGV, _commit_lines(("aaa1111", "feat(core): x\n\nCloses #21"), ("bbb2222", "chore: y")))
        )
        commits = fetch_commits(runner, REPO, 74)
        assert [c.sha for c in commits] == ["aaa1111", "bbb2222"]
        assert commits[0].issues == (21,)
        runner.assert_exhausted()

    def test_fetch_issue_reads_title_and_sections(self) -> None:
        body = "### Objective\n\nDo the thing.\n\n### Acceptance criteria\n\n- [ ] done\n"
        runner = make_runner(make_call(["gh", "api", f"repos/{REPO}/issues/21"], {"title": "T", "body": body}))
        assert fetch_issue(runner, REPO, 21) == IssueInfo(21, "T", "Do the thing.", "- [ ] done")

    def test_fetch_issue_with_null_body(self) -> None:
        runner = make_runner(make_call(["gh", "api", f"repos/{REPO}/issues/21"], {"title": "T", "body": None}))
        assert fetch_issue(runner, REPO, 21) == IssueInfo(21, "T", "", "")


class _RecordingRunner(QueueRunner):
    """QueueRunner that also keeps the stdin text of every call (the body goes through stdin)."""

    def __init__(self, calls: list[ExpectedCall]) -> None:
        super().__init__(calls)
        self.inputs: list[str | None] = []

    def run(self, argv: list[str], *, input_text: str | None = None) -> GhResult:
        self.inputs.append(input_text)
        return super().run(argv, input_text=input_text)


def _args(**overrides: object) -> argparse.Namespace:
    values: dict[str, object] = {"repo": REPO, "pr": 74, "dry_run": False}
    values.update(overrides)
    return argparse.Namespace(**values)


ISSUE_BODY = "### Objective\n\nDo the thing.\n\n### Risk\n\nlow\n"
PR_BODY = f"# Pull Request\n\n## Summary\n\n{START}\n- stale\n{END}\n\n## Linked issues\n\nCloses #21\n"


class TestHandle:
    def test_writes_the_regenerated_body_through_stdin(self, capsys: pytest.CaptureFixture[str]) -> None:
        runner = _RecordingRunner(
            [
                make_call(COMMITS_ARGV, _commit_lines(("aaa1111", "feat(core): x\n\nCloses #21"))),
                make_call(["gh", "api", f"repos/{REPO}/issues/21"], {"title": "T", "body": ISSUE_BODY}),
                make_call(["gh", "api", f"repos/{REPO}/pulls/74"], {"body": PR_BODY}),
                make_call(["gh", "pr", "edit", "74", "--repo", REPO, "--body-file", "-"], ""),
            ]
        )
        assert handle(_args(), build_parser(), runner) == 0
        runner.assert_exhausted()
        written = runner.inputs[-1]
        assert written is not None
        assert f"{START}\nDo the thing.\n\n#### Features\n- **core:** x (aaa1111)\n{END}" in written
        assert written.endswith("## Linked issues\n\nCloses #21\n")
        assert "Updated PR #74 auto-summary (1 commit, 1 issue)" in capsys.readouterr().out

    def test_dry_run_prints_and_does_not_edit(self, capsys: pytest.CaptureFixture[str]) -> None:
        runner = make_runner(
            make_call(COMMITS_ARGV, _commit_lines(("aaa1111", "feat(core): x\n\nCloses #21"))),
            make_call(["gh", "api", f"repos/{REPO}/issues/21"], {"title": "T", "body": ISSUE_BODY}),
            make_call(["gh", "api", f"repos/{REPO}/pulls/74"], {"body": PR_BODY}),
        )
        assert handle(_args(dry_run=True), build_parser(), runner) == 0
        runner.assert_exhausted()
        assert "#### Features" in capsys.readouterr().out

    def test_unchanged_body_is_left_alone(self, capsys: pytest.CaptureFixture[str]) -> None:
        current = f"{START}\nDo the thing.\n\n#### Features\n- **core:** x (aaa1111)\n{END}"
        runner = make_runner(
            make_call(COMMITS_ARGV, _commit_lines(("aaa1111", "feat(core): x\n\nCloses #21"))),
            make_call(["gh", "api", f"repos/{REPO}/issues/21"], {"title": "T", "body": ISSUE_BODY}),
            make_call(["gh", "api", f"repos/{REPO}/pulls/74"], {"body": current}),
        )
        assert handle(_args(), build_parser(), runner) == 0
        runner.assert_exhausted()
        assert "left untouched" in capsys.readouterr().out

    def test_body_without_markers_is_left_alone(self, capsys: pytest.CaptureFixture[str]) -> None:
        runner = make_runner(
            make_call(COMMITS_ARGV, _commit_lines(("c1", "chore: tidy"))),
            make_call(["gh", "api", f"repos/{REPO}/pulls/74"], {"body": None}),
        )
        assert handle(_args(), build_parser(), runner) == 0
        runner.assert_exhausted()
        assert "left untouched" in capsys.readouterr().out

    def test_pr_number_is_required(self) -> None:
        parser = build_parser()
        with pytest.raises(ValueError, match="--pr"):
            handle(_args(pr=None), parser, make_runner())


def test_main_wires_the_handler_into_the_shared_runner(monkeypatch: pytest.MonkeyPatch) -> None:
    seen: dict[str, object] = {}

    def fake_run_actionable_main(**kwargs: object) -> int:
        seen.update(kwargs)
        return 0

    monkeypatch.setattr("scripts.github.pr_summary.run_actionable_main", fake_run_actionable_main)
    assert main() == 0
    assert seen["handler"] is handle
    assert seen["build_parser"] is build_parser
