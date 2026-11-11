"""Tests for scripts.ci.report_status_context.

Validates that the report-status-context script posts commit statuses
to the PR head SHA (not merge_commit_sha) and provides diagnostic output
for CI log troubleshooting.

Background: GitHub rulesets evaluate required status checks against the
PR head commit SHA. The merge_commit_sha is unstable — GitHub recalculates
it when the base branch changes, orphaning any statuses posted there.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from scripts.ci.report_status_context import (
    _job_status_to_state,
    main,
)

_COMMON_ARGS = [
    "--github-token",
    "token",
    "--pr-number",
    "42",
    "--job-status",
    "success",
    "--status-context",
    "TDD Testing Pipeline / Hub Python",
    "--target-url",
    "https://example.invalid/run",
]


class TestJobStatusToState:
    """Test _job_status_to_state mapping."""

    @pytest.mark.parametrize(
        ("job_status", "expected"),
        [
            ("success", "success"),
            ("failure", "failure"),
            ("cancelled", "error"),
            ("anything-else", "error"),
            ("", "error"),
        ],
    )
    def test_maps_status_to_state(self, job_status: str, expected: str) -> None:
        """Test job status maps to correct GitHub commit status state."""
        result = _job_status_to_state(job_status)
        assert result == expected, f"Expected '{expected}' for job_status='{job_status}', got '{result}'"


class TestMainRequiresGithubRepository:
    """Test GITHUB_REPOSITORY env var requirement."""

    def test_exits_2_when_missing(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test exits 2 when GITHUB_REPOSITORY is not set."""
        monkeypatch.delenv("GITHUB_REPOSITORY", raising=False)
        exit_code = main(_COMMON_ARGS)
        assert exit_code == 2, "Expected exit code 2 when GITHUB_REPOSITORY is missing"


class TestHeadShaResolution:
    """Test that the script resolves and posts to the PR head SHA."""

    def test_queries_head_sha_not_merge_commit(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test the API call uses .head.sha jq query, not merge_commit_sha."""
        monkeypatch.setenv("GITHUB_REPOSITORY", "owner/repo")
        calls: list[list[str]] = []

        def _fake_run_gh(args: list[str], env: dict[str, str]) -> str:
            assert isinstance(env, dict), "env must be a dict"
            calls.append(args)
            return "abc123head" if len(calls) == 1 else ""

        monkeypatch.setattr("scripts.ci.report_status_context._run_gh", _fake_run_gh)
        exit_code = main(_COMMON_ARGS)
        assert exit_code == 0, f"Expected exit 0, got {exit_code}"

        get_args = calls[0]
        jq_query = next(arg for idx, arg in enumerate(get_args) if get_args[idx - 1] == "--jq")
        assert ".head.sha" in jq_query, f"Expected .head.sha in jq query, got: {jq_query}"
        assert "merge_commit_sha" not in jq_query, (
            f"jq query must not reference merge_commit_sha (unstable SHA): {jq_query}"
        )

    def test_posts_to_resolved_head_sha(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test the POST targets the SHA returned by the head.sha API query."""
        monkeypatch.setenv("GITHUB_REPOSITORY", "owner/repo")
        calls: list[list[str]] = []

        def _fake_run_gh(args: list[str], env: dict[str, str]) -> str:
            assert isinstance(env, dict), "env must be a dict"
            calls.append(args)
            return "abc123head" if len(calls) == 1 else ""

        monkeypatch.setattr("scripts.ci.report_status_context._run_gh", _fake_run_gh)
        main(_COMMON_ARGS)
        post_args = calls[1]
        assert any("statuses/abc123head" in arg for arg in post_args), (
            f"Expected POST to statuses/abc123head, got: {post_args}"
        )

    def test_passes_context_name_to_post(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test the status-context value is forwarded to the POST call."""
        monkeypatch.setenv("GITHUB_REPOSITORY", "owner/repo")
        calls: list[list[str]] = []

        def _fake_run_gh(args: list[str], env: dict[str, str]) -> str:
            assert isinstance(env, dict), "env must be a dict"
            calls.append(args)
            return "deadbeef" if len(calls) == 1 else ""

        monkeypatch.setattr("scripts.ci.report_status_context._run_gh", _fake_run_gh)
        main(_COMMON_ARGS)
        post_args = calls[1]
        assert any(arg == "context=TDD Testing Pipeline / Hub Python" for arg in post_args), (
            f"Expected context=TDD Testing Pipeline / Hub Python in POST args, got: {post_args}"
        )

    def test_exits_2_when_sha_is_null(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test exits 2 when API returns null for head SHA."""
        monkeypatch.setenv("GITHUB_REPOSITORY", "owner/repo")

        def _fake_run_gh(args: list[str], env: dict[str, str]) -> str:
            assert isinstance(args, list)
            assert isinstance(env, dict)
            return "null"

        monkeypatch.setattr("scripts.ci.report_status_context._run_gh", _fake_run_gh)
        exit_code = main(_COMMON_ARGS)
        assert exit_code == 2, "Expected exit 2 when head SHA is null"

    def test_exits_2_when_sha_is_empty(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test exits 2 when API returns empty string for head SHA."""
        monkeypatch.setenv("GITHUB_REPOSITORY", "owner/repo")

        def _fake_run_gh(args: list[str], env: dict[str, str]) -> str:
            assert isinstance(args, list)
            assert isinstance(env, dict)
            return ""

        monkeypatch.setattr("scripts.ci.report_status_context._run_gh", _fake_run_gh)
        exit_code = main(_COMMON_ARGS)
        assert exit_code == 2, "Expected exit 2 when head SHA is empty"

    def test_exits_2_when_api_call_fails(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test exits 2 when gh API call raises RuntimeError."""
        monkeypatch.setenv("GITHUB_REPOSITORY", "owner/repo")

        def _fake_run_gh(args: list[str], env: dict[str, str]) -> str:
            raise RuntimeError("gh command failed with exit code 1. stderr:\nAPI error")

        monkeypatch.setattr("scripts.ci.report_status_context._run_gh", _fake_run_gh)
        exit_code = main(_COMMON_ARGS)
        assert exit_code == 2, "Expected exit 2 when API call raises RuntimeError"

    def test_exits_1_when_post_fails(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Test exits 1 when the POST status call fails (distinct from SHA resolution failure)."""
        monkeypatch.setenv("GITHUB_REPOSITORY", "owner/repo")
        call_count = 0

        def _fake_run_gh(args: list[str], env: dict[str, str]) -> str:
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                return "deadbeef"
            raise RuntimeError("gh command failed with exit code 1. stderr:\n403 Forbidden")

        monkeypatch.setattr("scripts.ci.report_status_context._run_gh", _fake_run_gh)
        exit_code = main(_COMMON_ARGS)
        assert exit_code == 1, "Expected exit 1 when POST call fails (not 2, which is SHA resolution)"


class TestBashScript:
    """Text-based validation of report_status_context.sh."""

    def test_uses_head_sha(self) -> None:
        """Test bash script queries .head.sha, not merge_commit_sha."""
        script = Path("scripts/ci/report_status_context.sh").read_text(encoding="utf-8")
        assert ".head.sha" in script, "Bash script must query .head.sha for ruleset compatibility"
        assert "merge_commit_sha" not in script, (
            "Bash script must not reference merge_commit_sha — rulesets evaluate head SHA only"
        )

    def test_has_diagnostic_output(self) -> None:
        """Test bash script includes diagnostic output for CI log troubleshooting."""
        script = Path("scripts/ci/report_status_context.sh").read_text(encoding="utf-8")
        assert "Posting status context" in script, "Bash script must log the context being posted"
        assert "failed to resolve head SHA" in script, "Bash script must log SHA resolution failures"

    def test_has_strict_mode(self) -> None:
        """Test bash script uses strict error handling."""
        script = Path("scripts/ci/report_status_context.sh").read_text(encoding="utf-8")
        assert "set -Eeuo pipefail" in script, "Bash script must use strict mode (set -Eeuo pipefail)"

    def test_validates_required_env_vars(self) -> None:
        """Test bash script validates all required env vars."""
        script = Path("scripts/ci/report_status_context.sh").read_text(encoding="utf-8")
        for var in ("GITHUB_REPOSITORY", "PR_NUMBER", "JOB_STATUS", "STATUS_CONTEXT", "TARGET_URL", "GH_TOKEN"):
            assert f': "${{{var}:?' in script, f"Bash script must validate {var} is set"
