"""Tests for the committed GitHub ruleset and security-and-analysis request bodies."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

_DIR = Path(__file__).resolve().parents[3] / "docs" / "ci" / "github-rulesets"
_READ_ONLY = ("id", "node_id", "_links", "created_at", "updated_at", "source", "source_type", "current_user_can_bypass")


def _load(name: str) -> dict[str, Any]:
    data: dict[str, Any] = json.loads((_DIR / name).read_text(encoding="utf-8"))
    return data


def test_bypass_is_the_operator_in_pull_request_mode() -> None:
    assert _load("main-ruleset.json")["bypass_actors"] == [
        {"actor_id": 35460307, "actor_type": "User", "bypass_mode": "pull_request"}
    ]


def test_pull_request_rule_requires_code_owner_review() -> None:
    rules = _load("main-ruleset.json")["rules"]
    params = next(r["parameters"] for r in rules if r["type"] == "pull_request")
    assert params["required_approving_review_count"] == 1
    assert params["require_code_owner_review"] is True


def test_ruleset_carries_no_read_only_field() -> None:
    assert not set(_READ_ONLY) & set(_load("main-ruleset.json"))


def test_security_body_enables_exactly_the_three_settings() -> None:
    body = _load("security-and-analysis.json")
    assert body == {
        "security_and_analysis": {
            "secret_scanning": {"status": "enabled"},
            "secret_scanning_non_provider_patterns": {"status": "enabled"},
            "secret_scanning_push_protection": {"status": "enabled"},
        }
    }
