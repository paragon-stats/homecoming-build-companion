"""Tests for attack classification (#67), against the real canonical power files."""

import json
from collections.abc import Mapping
from pathlib import Path
from typing import Any

import pytest

from coh_engine.attack_profile import category_disagreement, classify_attack, load_canonical_powers
from coh_engine.damage_model import AttackProfile, Delivery, Role, Shape

type Powers = Mapping[str, Mapping[str, Any]]

POWERS = Path(__file__).parents[3] / "data" / "canonical" / "powers"
CSS = "Arachnos_Soldiers.Crab_Spider_Soldier."

TABLE = [
    (CSS + "Channelgun", Delivery.RANGED, Shape.SINGLE, Role.CHAIN),
    (CSS + "Slice", Delivery.MELEE, Shape.SINGLE, Role.CHAIN),
    (CSS + "Arm_Lash", Delivery.MELEE, Shape.CONE, Role.CHAIN),
    (CSS + "Suppression", Delivery.RANGED, Shape.CONE, Role.CHAIN),
    (CSS + "CS_Venom_Grenade", Delivery.RANGED, Shape.TARGETED_AOE, Role.CHAIN),
    (CSS + "Frenzy", Delivery.MELEE, Shape.PBAOE, Role.CHAIN),
    (CSS + "Omega_Maneuver", Delivery.RANGED, Shape.LOCATION, Role.PATCH),
    (CSS + "Aim", Delivery.MELEE, Shape.SINGLE, Role.BUFF_CLICK),
    ("Training_Gadgets.Crab_Spider_Training.Summon_Spiderlings", None, None, Role.PET_SUMMON),
    ("Arachnos_Soldiers.Bane_Spider_Soldier.Pulverize", Delivery.MELEE, Shape.SINGLE, Role.CHAIN),
    ("Brute_Defense.Fiery_Aura.Blazing_Aura", Delivery.MELEE, Shape.PBAOE, Role.AURA),
    ("Mastermind_Summon.Mercenaries.Soldiers", None, None, Role.PET_SUMMON),
]


@pytest.fixture(scope="module")
def powers() -> Powers:
    return load_canonical_powers(POWERS, [row[0] for row in TABLE])


@pytest.mark.parametrize(("name", "delivery", "shape", "role"), TABLE)
def test_real_power_classification(
    powers: Powers, name: str, delivery: Delivery | None, shape: Shape | None, role: Role
) -> None:
    profile = classify_attack(powers[name])
    assert profile.full_name == name
    assert profile.role is role
    if delivery is not None:
        assert (profile.delivery, profile.shape) == (delivery, shape)


def test_profile_figures(powers: Powers) -> None:
    arm_lash = classify_attack(powers[CSS + "Arm_Lash"])
    assert (arm_lash.range_ft, arm_lash.max_targets) == (10.0, 5)
    assert arm_lash.arc_rad == pytest.approx(0.5236)
    assert "Melee AoE Damage" in arm_lash.set_categories
    assert classify_attack(powers[CSS + "Frenzy"]).radius_ft == 10.0


def test_real_powers_agree_with_their_categories(powers: Powers) -> None:
    assert [category_disagreement(classify_attack(p)) for p in powers.values()] == [None] * len(powers)


def test_melee_threshold_is_a_parameter(powers: Powers) -> None:
    assert classify_attack(powers[CSS + "Arm_Lash"], melee_range_ft_max=5.0).delivery is Delivery.RANGED


def _power(**over: Any) -> dict[str, Any]:
    base: dict[str, Any] = {
        "full_name": "X.Y.Z",
        "type": "Click",
        "effect_area": "SingleTarget",
        "range": 40.0,
        "radius": 0.0,
        "arc": 0.0,
        "max_targets_hit": 0,
        "allowed_boostset_cats": [],
        "effects": [],
    }
    return base | over


def _effect(attrib: str, aspect: str, target: str, children: tuple[dict[str, Any], ...] = ()) -> dict[str, Any]:
    template = {"attribs": [attrib], "aspect": aspect, "target": target}
    return {"templates": [template], "child_effects": list(children)}


def test_damage_in_child_effect_is_found() -> None:
    nested = _effect("Fire_Dmg", "Absolute", "AnyAffected")
    outer = {"templates": [], "child_effects": [nested]}
    assert classify_attack(_power(effects=[outer])).role is Role.CHAIN


def test_chain_area_is_targeted_aoe() -> None:
    jump = _effect("Fire_Dmg", "Absolute", "AnyAffected")
    assert classify_attack(_power(effect_area="Chain", effects=[jump])).shape is Shape.TARGETED_AOE


def test_unknown_effect_area_is_e36() -> None:
    with pytest.raises(ValueError, match=r"E36: X\.Y\.Z.*'Map'"):
        classify_attack(_power(effect_area="Map"))


def test_non_damaging_power_is_e37() -> None:
    buff = _effect("Defense", "Current", "Self")
    with pytest.raises(ValueError, match=r"E37: X\.Y\.Z"):
        classify_attack(_power(effects=[buff]))


def test_self_damage_buff_on_a_toggle_is_e37() -> None:
    buff = _effect("Fire_Dmg", "Strength", "Self")
    with pytest.raises(ValueError, match="E37"):
        classify_attack(_power(type="Toggle", effects=[buff]))


def test_entity_without_pet_category_is_patch() -> None:
    spawn = _effect("Create_Entity", "Current", "AnyAffected")
    assert classify_attack(_power(effects=[spawn])).role is Role.PATCH
    pets = _power(effects=[spawn], allowed_boostset_cats=["Pet Damage"])
    assert classify_attack(pets).role is Role.PET_SUMMON


def _profile(delivery: Delivery, cats: list[str]) -> AttackProfile:
    return AttackProfile("X.Y.Z", delivery, Shape.SINGLE, Role.CHAIN, 40.0, 0.0, 0.0, 0, tuple(cats))


def test_category_disagreement_both_ways() -> None:
    assert "classified MELEE" in str(category_disagreement(_profile(Delivery.MELEE, ["Ranged Damage"])))
    assert "classified RANGED" in str(category_disagreement(_profile(Delivery.RANGED, ["Melee AoE Damage"])))


@pytest.mark.parametrize(
    ("delivery", "cats"),
    [
        (Delivery.MELEE, ["Universal Damage Sets", "Brute Archetype Sets"]),
        (Delivery.MELEE, ["Melee Damage"]),
        (Delivery.RANGED, ["Ranged Damage"]),
        (Delivery.RANGED, ["Melee Damage", "Ranged Damage"]),
    ],
)
def test_category_agreement_is_none(delivery: Delivery, cats: list[str]) -> None:
    assert category_disagreement(_profile(delivery, cats)) is None


def _write(root: Path, full_name: str, content: str) -> Path:
    path = root.joinpath(*(s.lower() for s in full_name.split("."))).with_suffix(".json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return path


def test_unknown_name_is_e35(tmp_path: Path) -> None:
    _write(tmp_path, "A.B.Here", json.dumps({"full_name": "A.B.Here"}))
    with pytest.raises(ValueError, match=r"E35.*\['A\.B\.Gone'\]"):
        load_canonical_powers(tmp_path, ["A.B.Here", "A.B.Gone"])


def test_empty_file_is_skipped_and_fallback_finds_the_real_one(tmp_path: Path) -> None:
    _write(tmp_path, "A.B.Both", "")
    other = tmp_path / "elsewhere" / "both.json"
    other.parent.mkdir()
    other.write_text(json.dumps({"full_name": "A.B.Both"}), encoding="utf-8")
    assert load_canonical_powers(tmp_path, ["A.B.Both"])["A.B.Both"]["full_name"] == "A.B.Both"


def test_empty_file_alone_is_e35(tmp_path: Path) -> None:
    _write(tmp_path, "A.B.Empty", "")
    with pytest.raises(ValueError, match="E35"):
        load_canonical_powers(tmp_path, ["A.B.Empty"])
