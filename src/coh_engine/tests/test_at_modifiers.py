"""AT-inherent damage modifiers (issue #69): coverage of every playable class, pinned defaults, overrides."""

import json
from pathlib import Path
from types import MappingProxyType

import pytest

from coh_engine.at_modifiers import at_modifier_functions, at_modifier_rules
from coh_engine.damage_model import (
    SOURCE_CHAIN,
    SOURCE_PET,
    DamageContext,
    EnduranceBudget,
    ModifierResult,
    Movement,
    Scenario,
    SourceResult,
    Stance,
    evaluate_damage,
    registered_damage_modifiers,
)
from coh_engine.maths import f32

_ARCHETYPES = Path(__file__).parent / "fixtures" / "mids" / "archetypes.json"
_PLAYABLE = [a["ClassName"] for a in json.loads(_ARCHETYPES.read_text(encoding="utf-8")) if a["Playable"]]


def _ctx(cls: str, **assumptions: float) -> DamageContext:
    return DamageContext(
        attacks=(),
        budget=EnduranceBudget(max_end=100.0, recovery_per_sec=2.0, toggle_drain_per_sec=0.5),
        stance=Stance.MELEE,
        scenario=Scenario(),
        movement=Movement(speed_ft_per_sec=20.0, standoff_ft=40.0),
        archetype_class=cls,
        assumptions=MappingProxyType({f"{cls}.{k}": v for k, v in assumptions.items()}),
    )


def _results(ctx: DamageContext) -> list[ModifierResult]:
    return [r for fn in at_modifier_functions() if (r := fn(ctx)) is not None]


def _mult(cls: str, **assumptions: float) -> float:
    (result,) = _results(_ctx(cls, **assumptions))
    return result.multiplier


_ADDITIVE = {
    "Class_Brute",
    "Class_Blaster",
    "Class_Defender",
    "Class_Sentinel",
    "Class_Mastermind",
    "Class_Peacebringer",
    "Class_Warshade",
}


@pytest.mark.parametrize("cls", _PLAYABLE)
def test_damage_buffs_are_additive_and_damage_multipliers_are_not(cls: str) -> None:
    """Fury-style buffs add to the strength already applied; Containment and crits multiply it."""
    (result,) = _results(_ctx(cls))
    assert result.additive is (cls in _ADDITIVE)


def test_every_playable_class_resolves_to_exactly_one_result() -> None:
    """Fails if a class in archetypes.json gains no rule. NPC classes (Playable false) are ignored."""
    assert _PLAYABLE, "fixture has no playable classes"
    for cls in _PLAYABLE:
        results = _results(_ctx(cls))
        assert len(results) == 1, f"{cls} resolved to {len(results)} AT modifiers"
        assert results[0].assumption
        assert at_modifier_rules()[cls] == results[0].modifier
    assert set(at_modifier_rules()) == set(_PLAYABLE)


def test_npc_class_gets_no_modifier() -> None:
    """Rules gate on the class: an NPC class matches nothing."""
    assert _results(_ctx("Class_Boss_Grunt")) == []


def test_rules_are_registered_with_the_damage_model() -> None:
    """Importing the module registers every rule under its own name."""
    assert set(at_modifier_rules().values()) <= set(registered_damage_modifiers())


@pytest.mark.parametrize(
    ("cls", "expected"),
    [
        # data/canonical/powers/inherent/inherent/rage_buff.json: 0.02 * kRage; fury 75 is approximate.
        ("Class_Brute", 2.5),
        # containment.json has no magnitude: share 0.5 of +100%, approximate.
        ("Class_Controller", 1.5),
        # scourge.json help text (double damage); chance 0.10 approximate.
        ("Class_Corruptor", 1.1),
        # defiance.json has no magnitude: bonus 0.30 approximate.
        ("Class_Blaster", 1.3),
        # vigilance.json: +0.3 solo.
        ("Class_Defender", 1.3),
        # critical_hit.json help text (double damage); chance 0.10 approximate.
        ("Class_Scrapper", 1.1),
        # assassination.json: 0.10 + 0.03 * 1 team member, double damage.
        ("Class_Stalker", 1.13),
        # scrutiny.json help text: +5% per Sentinel.
        ("Class_Sentinel", 1.05),
        # supremacy.json: +0.25 to MastermindPets.
        ("Class_Mastermind", 1.25),
        # cosmic_balance.json / dark_sustenance.json: no allies solo.
        ("Class_Peacebringer", 1.0),
        ("Class_Warshade", 1.0),
        # domination.json, gauntlet.json, spider_conditioning.json, widow_conditioning.json: no damage effect.
        ("Class_Dominator", 1.0),
        ("Class_Tanker", 1.0),
        ("Class_Arachnos_Soldier", 1.0),
        ("Class_Arachnos_Widow", 1.0),
    ],
)
def test_default_multiplier(cls: str, expected: float) -> None:
    """Each class's default expected-value multiplier, with its source cited above."""
    assert _mult(cls) == pytest.approx(expected, rel=1e-6)


@pytest.mark.parametrize(
    ("cls", "overrides", "expected"),
    [
        ("Class_Brute", {"fury_level": 100.0}, 3.0),
        ("Class_Controller", {"contained_share": 1.0}, 2.0),
        ("Class_Corruptor", {"scourge_chance": 0.5}, 1.5),
        ("Class_Blaster", {"damage_bonus": 0.5}, 1.5),
        ("Class_Defender", {"team_size": 3.0}, 1.1),
        ("Class_Defender", {"team_size": 8.0}, 1.0),
        ("Class_Scrapper", {"crit_chance": 0.2, "crit_damage_mult": 3.0}, 1.4),
        ("Class_Stalker", {"team_size": 8.0, "crit_damage_mult": 2.0}, 1.34),
        ("Class_Sentinel", {"sentinels": 2.0}, 1.1),
        ("Class_Mastermind", {"pets_in_range": 0.5}, 1.125),
        ("Class_Peacebringer", {"damage_allies": 2.0}, 1.4),
        ("Class_Warshade", {"damage_allies": 3.0}, 1.6),
    ],
)
def test_assumption_override_changes_result(cls: str, overrides: dict[str, float], expected: float) -> None:
    """A ``<Class>.<param>`` assumption moves the multiplier."""
    assert _mult(cls, **overrides) == pytest.approx(expected, rel=1e-6)


def test_supremacy_scales_only_pets() -> None:
    """Supremacy targets SOURCE_PET; through evaluate_damage the chain is untouched."""
    ctx = _ctx("Class_Mastermind")
    (result,) = _results(ctx)
    assert result.applies_to == frozenset({SOURCE_PET})
    sources = [
        (lambda c: SourceResult(source=SOURCE_CHAIN, dps=100.0), False),
        (lambda c: SourceResult(source=SOURCE_PET, dps=100.0), False),
    ]
    report = evaluate_damage(ctx, sources=sources, modifiers=at_modifier_functions())
    assert report.scaled_dps[SOURCE_CHAIN] == 100.0
    assert report.scaled_dps[SOURCE_PET] == f32(125.0)


def test_neutral_results_say_why() -> None:
    """A neutral archetype carries an assumption explaining the 1.0."""
    (result,) = _results(_ctx("Class_Tanker"))
    assert result.multiplier == 1.0
    assert "no damage effect" in result.assumption


@pytest.mark.parametrize(
    ("cls", "key", "bad"),
    [
        ("Class_Controller", "contained_share", 1.5),
        ("Class_Brute", "fury_level", -1.0),
        ("Class_Defender", "team_size", 0.0),
    ],
)
def test_out_of_range_assumption_raises_e50(cls: str, key: str, bad: float) -> None:
    """An out-of-range value raises E50 naming the key."""
    with pytest.raises(ValueError, match=rf"E50: assumption {cls}\.{key}="):
        _results(_ctx(cls, **{key: bad}))


def test_rule_mapping_is_read_only() -> None:
    """The class-to-rule mapping cannot be mutated by callers."""
    with pytest.raises(TypeError):
        at_modifier_rules()["Class_X"] = "x"  # type: ignore[index]
