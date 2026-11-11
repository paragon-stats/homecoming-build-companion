"""Build-type profile loading, selection, and breakpoint-ref resolution.

``build_profiles.json`` holds the nine canonical types with targets that reference
``breakpoints.json`` by dotted path (``{at}`` resolved against the build's archetype).
:mod:`coh_engine.profiles` loads both and resolves a named profile to concrete
numeric/boolean targets.
"""

from pathlib import Path
from typing import Any

import pytest

from coh_engine.profiles import (
    CANONICAL_PROFILES,
    Target,
    load_breakpoints,
    load_build_profiles,
    profile_names,
    resolve_profile,
    select_profile,
)

STRATEGY = Path(__file__).parents[3] / "strategy"


@pytest.fixture(scope="module")
def breakpoints() -> dict[str, Any]:
    return dict(load_breakpoints(STRATEGY / "breakpoints.json"))


@pytest.fixture(scope="module")
def raw_profiles() -> dict[str, Any]:
    return dict(load_build_profiles(STRATEGY / "build_profiles.json"))


def test_all_nine_canonical_profiles_present(raw_profiles: dict[str, Any]) -> None:
    """The file carries exactly the nine canonical types — no more, no invented names."""
    assert set(profile_names(raw_profiles)) == set(CANONICAL_PROFILES)
    assert len(CANONICAL_PROFILES) == 9


def test_no_hard_mode_profile(raw_profiles: dict[str, Any]) -> None:
    """Hard-mode / 4-star is a documented GAP and must not appear."""
    names = " ".join(profile_names(raw_profiles)).lower()
    assert "hard" not in names
    assert "star" not in names
    assert "advanced" not in names


def test_soft_cap_target_resolves_the_universal_breakpoint(
    raw_profiles: dict[str, Any], breakpoints: dict[str, Any]
) -> None:
    """soft-cap-defense resolves its target to the 45% PvE softcap from breakpoints."""
    profile = resolve_profile("soft-cap-defense", raw_profiles, breakpoints, at_key="Scrapper")
    assert profile.display_name == "Soft-cap defense"
    assert profile.targets == (Target(metric="defense_positional_min", op=">=", value=45.0),)


def test_resist_cap_target_resolves_the_at_specific_breakpoint(
    raw_profiles: dict[str, Any], breakpoints: dict[str, Any]
) -> None:
    """resist-cap resolves the {at} ref to the archetype's own resist cap (75 Scrapper, 90 Tanker)."""
    scrapper = resolve_profile("resist-cap", raw_profiles, breakpoints, at_key="Scrapper")
    tanker = resolve_profile("resist-cap", raw_profiles, breakpoints, at_key="Tanker")
    assert scrapper.targets[0].value == 75
    assert tanker.targets[0].value == 90


def test_at_ref_accepts_a_display_name_with_a_space(raw_profiles: dict[str, Any], breakpoints: dict[str, Any]) -> None:
    """resolve_profile normalizes a spaced display name to the underscore breakpoints key."""
    profile = resolve_profile("resist-cap", raw_profiles, breakpoints, at_key="Arachnos Soldier")
    assert profile.targets[0].value == breakpoints["archetypes"]["Arachnos_Soldier"]["res_cap_pct"]


def test_boolean_target_keeps_its_literal_value(raw_profiles: dict[str, Any], breakpoints: dict[str, Any]) -> None:
    """perma-hasten's boolean target carries the literal True, not a breakpoint ref."""
    profile = resolve_profile("perma-hasten", raw_profiles, breakpoints, at_key="Dominator")
    assert profile.targets == (Target(metric="perma_hasten", op="==", value=True),)


def test_theme_profile_has_no_numeric_targets(raw_profiles: dict[str, Any], breakpoints: dict[str, Any]) -> None:
    """Theme/RP carries no hard targets — only the universal exemplar-10 gate applies."""
    profile = resolve_profile("theme-rp", raw_profiles, breakpoints, at_key="Scrapper")
    assert profile.targets == ()


def test_select_profile_returns_the_named_profile(raw_profiles: dict[str, Any], breakpoints: dict[str, Any]) -> None:
    """select_profile resolves a goal string to its profile."""
    profile = select_profile("perma-hasten", raw_profiles, breakpoints, at_key="Scrapper")
    assert profile.name == "perma-hasten"


def test_select_unknown_profile_fails_loud(raw_profiles: dict[str, Any], breakpoints: dict[str, Any]) -> None:
    """An unknown goal name is refused (P-GOAL-001) rather than silently defaulted."""
    with pytest.raises(ValueError, match="P-GOAL-001"):
        select_profile("hard-mode", raw_profiles, breakpoints, at_key="Scrapper")


def test_unknown_breakpoint_ref_fails_loud(raw_profiles: dict[str, Any], breakpoints: dict[str, Any]) -> None:
    """A target referencing a missing breakpoint path is refused (E16), not silently zero."""
    bad = {
        "display_name": "Bad",
        "priority": [],
        "targets": [{"metric": "x", "op": ">=", "ref": "universal.does_not_exist"}],
    }
    with pytest.raises(ValueError, match="E16"):
        resolve_profile("bad", {"bad": bad}, breakpoints, at_key="Scrapper")


def test_ref_resolving_to_a_nonnumeric_node_fails_loud(breakpoints: dict[str, Any]) -> None:
    """A ref that lands on a sub-object rather than a number is refused (E16)."""
    bad = {
        "display_name": "Bad",
        "priority": [],
        "targets": [{"metric": "x", "op": ">=", "ref": "universal"}],  # a dict, not a number
    }
    with pytest.raises(ValueError, match="E16"):
        resolve_profile("bad", {"bad": bad}, breakpoints, at_key="Scrapper")


def test_ref_resolving_to_a_boolean_fails_loud() -> None:
    """A boolean breakpoint is not numeric (bool is an int subclass) and is refused (E16)."""
    breakpoints = {"universal": {"a_flag": True}}
    bad = {
        "display_name": "Bad",
        "priority": [],
        "targets": [{"metric": "x", "op": ">=", "ref": "universal.a_flag"}],
    }
    with pytest.raises(ValueError, match="E16"):
        resolve_profile("bad", {"bad": bad}, breakpoints, at_key="Scrapper")
