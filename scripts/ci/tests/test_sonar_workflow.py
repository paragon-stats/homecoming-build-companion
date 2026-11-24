"""Tests for the SonarQube scanner exclusions in the sonar workflow."""

from __future__ import annotations

import re
from pathlib import Path

import pytest

_WORKFLOW = Path(__file__).resolve().parents[3] / ".github" / "workflows" / "sonar.yml"


@pytest.mark.parametrize("prop", ["sonar.exclusions", "sonar.cpd.exclusions"])
def test_nested_test_fixtures_are_excluded(prop: str) -> None:
    match = re.search(rf'/d:{re.escape(prop)}="([^"]*)"', _WORKFLOW.read_text(encoding="utf-8"))
    assert match is not None
    assert "**/tests/fixtures/**" in match.group(1).split(",")


def test_scan_skips_fork_and_dependabot_pull_requests_but_runs_on_push() -> None:
    match = re.search(r"^  sonar:\n(?:    #.*\n)*    if: (.+)$", _WORKFLOW.read_text(encoding="utf-8"), re.MULTILINE)
    assert match is not None
    condition = match.group(1)
    assert "github.event_name != 'pull_request' ||" in condition
    assert "!github.event.pull_request.head.repo.fork" in condition
    assert "github.actor != 'dependabot[bot]'" in condition
