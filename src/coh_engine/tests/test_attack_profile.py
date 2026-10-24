"""Tests for attack classification (#67), against the real canonical power files."""

import json
from collections.abc import Mapping
from pathlib import Path
from typing import Any

import pytest

from coh_engine.attack_profile import (
    Resolver,
    applies_in_pve,
    canonical_power_path,
    canonical_resolver,
    category_disagreement,
    classify_attack,
    foe_res_debuff_templates,
    load_canonical_powers,
)
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


@pytest.mark.parametrize(
    "name",
    ["Warshade_Offensive.Umbral_Blast.Shadow_Bolt", "Feral_Might.Feral_Might.Brutal_Swipe"],
)
def test_an_effectless_power_classifies_through_its_default_redirect(name: str) -> None:
    """Kheldian blasts (``!Source.Mode?``) and Feral Might (``Always``) carry their effects on a redirect."""
    resolve = canonical_resolver(POWERS)
    power = resolve(name)
    assert power is not None and not power["effects"]
    assert classify_attack(power, resolve=resolve).role is Role.CHAIN
    with pytest.raises(ValueError, match="E37"):
        classify_attack(power)


def test_a_redirect_on_an_undecidable_condition_is_not_followed() -> None:
    """Shadow Recall redirects on ``target.isFriend?``, which no static reading can settle."""
    resolve = canonical_resolver(POWERS)
    power = resolve("Warshade_Defensive.Umbral_Aura.Shadow_Recall")
    assert power is not None
    with pytest.raises(ValueError, match="E37"):
        classify_attack(power, resolve=resolve)


def test_pve_applicability_rule() -> None:
    """A group applies in PvE when unconditional or gated only on the critter test; PvP-only never applies."""
    assert applies_in_pve({"requires_expression": ""})
    assert applies_in_pve({"requires_expression": " target>enttype eq 'critter' "})
    assert not applies_in_pve({"requires_expression": "Source.Mode?(x)"})
    assert not applies_in_pve({"requires_expression": "", "is_pvp": "PVP_ONLY"})


def test_foe_res_debuff_templates_follow_links_and_skip_pvp() -> None:
    """Freezing Rain's -Res lives in its summoned patch; Melt Armor's is its own; Aim has none."""
    resolve = canonical_resolver(POWERS)
    rain = resolve("Defender_Buff.Storm_Summoning.Fog")
    melt = resolve("Defender_Buff.Thermal_Radiation.Melt_Armor")
    assert rain is not None and melt is not None
    rain_res = foe_res_debuff_templates(rain, resolve)
    assert rain_res and all(t["table"].endswith("Res_Dmg") for t in rain_res)
    assert len({(t["table"], t["scale"], t["duration"]) for t in rain_res}) == len(rain_res)  # no PvP duplicate
    assert foe_res_debuff_templates(melt, resolve)
    assert foe_res_debuff_templates(melt, None)  # own templates need no resolver
    aim = resolve("Arachnos_Soldiers.Crab_Spider_Soldier.Aim")
    assert aim is not None and foe_res_debuff_templates(aim, resolve) == []


def test_damage_in_child_effect_is_found() -> None:
    nested = _effect("Fire_Dmg", "Absolute", "AnyAffected")
    outer = {"templates": [], "child_effects": [nested]}
    assert classify_attack(_power(effects=[outer])).role is Role.CHAIN


def test_damaging_auto_power_is_an_aura_not_a_chain_attack() -> None:
    """An Auto power is never activated, so its damage runs beside the chain like a toggle aura."""
    burn = _effect("Fire_Dmg", "Absolute", "AnyAffected")
    assert classify_attack(_power(type="Auto", range=0.0, effect_area="AoE", effects=[burn])).role is Role.AURA


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


def test_bom_prefixed_file_loads(tmp_path: Path) -> None:
    path = _write(tmp_path, "A.B.Bom", "")
    path.write_bytes(b"\xef\xbb\xbf" + json.dumps({"full_name": "A.B.Bom"}).encode())
    assert load_canonical_powers(tmp_path, ["A.B.Bom"])["A.B.Bom"]["full_name"] == "A.B.Bom"


def test_corrupt_file_alone_is_e38_with_path(tmp_path: Path) -> None:
    path = _write(tmp_path, "A.B.Bad", "{")
    with pytest.raises(ValueError, match=r"E38: .*bad\.json is not valid JSON") as info:
        load_canonical_powers(tmp_path, ["A.B.Bad"])
    assert str(path) in str(info.value)


def test_corrupt_unrelated_candidate_is_skipped_when_another_matches(tmp_path: Path) -> None:
    _write(tmp_path, "A.B.Both", "")  # empty derived file forces the rglob fallback
    unrelated = json.dumps({"full_name": "X.Y.Both"})
    for folder, content in (("aaa", "{"), ("mmm", unrelated), ("zzz", json.dumps({"full_name": "A.B.Both"}))):
        other = tmp_path / folder / "both.json"
        other.parent.mkdir()
        other.write_text(content, encoding="utf-8")
    assert load_canonical_powers(tmp_path, ["A.B.Both"])["A.B.Both"]["full_name"] == "A.B.Both"


def test_corrupt_candidate_with_no_match_is_e38(tmp_path: Path) -> None:
    _write(tmp_path, "A.B.Both", "")
    other = tmp_path / "aaa" / "both.json"
    other.parent.mkdir()
    other.write_text("{", encoding="utf-8")
    with pytest.raises(ValueError, match="E38"):
        load_canonical_powers(tmp_path, ["A.B.Both"])


def test_empty_file_alone_is_e35(tmp_path: Path) -> None:
    _write(tmp_path, "A.B.Empty", "")
    with pytest.raises(ValueError, match="E35"):
        load_canonical_powers(tmp_path, ["A.B.Empty"])


COLON = "Training_Gadgets.Training_and_Gadgets.Tactical_Training:_Assault"


def test_canonical_power_path_maps_colon_and_lowercases(tmp_path: Path) -> None:
    expected = tmp_path / "training_gadgets" / "training_and_gadgets" / "tactical_training__assault.json"
    assert canonical_power_path(tmp_path, COLON) == expected


def test_colon_name_loads_from_sanitized_file(tmp_path: Path) -> None:
    path = canonical_power_path(tmp_path, COLON)
    path.parent.mkdir(parents=True)
    path.write_text(json.dumps({"full_name": COLON}), encoding="utf-8")
    assert load_canonical_powers(tmp_path, [COLON])[COLON]["full_name"] == COLON


def test_real_colon_power_loads() -> None:
    assert load_canonical_powers(POWERS, [COLON])[COLON]["full_name"] == COLON


# --- support roles, linked effects, modes_required ---------------------------------------------

LINKED = [
    ("Defender_Buff.Thermal_Radiation.Melt_Armor", Role.SUPPORT),
    ("Defender_Buff.Kinetics.Siphon_Power", Role.BUFF_CLICK),
    ("Defender_Buff.Kinetics.Kinetic_Transfer", Role.BUFF_CLICK),
    # Rule 2 wins: the Freezing Rain entity also carries a 0.009-scale Cold_Dmg tick next to its -Res.
    ("Defender_Buff.Storm_Summoning.Fog", Role.PATCH),
    ("Defender_Buff.Dark_Miasma.Tar_Patch", Role.SUPPORT),
    ("Defender_Buff.Radiation_Emission.Enervating__Field", Role.PERSISTENT_DEBUFF),
    ("Defender_Buff.Poison.Venomous_Gas", Role.PERSISTENT_DEBUFF),
    ("Corruptor_Buff.Sonic_Resonance.Sonic_Siphon", Role.SUPPORT),
    ("Defender_Buff.Poison.Envenom", Role.SUPPORT),
    ("Sentinel_Ranged.Archery.Aim", Role.BUFF_CLICK),
    ("Scrapper_Melee.Katana.Build_Up", Role.BUFF_CLICK),
    ("Peacebringer_Offensive.Luminous_Blast.Bright_Nova_Blast", Role.CHAIN),
]


@pytest.fixture(scope="module")
def resolve() -> Resolver:
    return canonical_resolver(POWERS)


def _real(resolve: Resolver, name: str) -> Mapping[str, Any]:
    power = resolve(name)
    assert power is not None
    return power


@pytest.mark.parametrize(("name", "role"), LINKED)
def test_real_support_power_roles(resolve: Resolver, name: str, role: Role) -> None:
    assert classify_attack(_real(resolve, name), resolve=resolve).role is role


def test_linked_buff_needs_the_resolver(resolve: Resolver) -> None:
    """Siphon Power's own effects only debuff the foe's damage; the self buff sits behind Execute_Power."""
    with pytest.raises(ValueError, match="E37"):
        classify_attack(_real(resolve, "Defender_Buff.Kinetics.Siphon_Power"))


def test_location_debuff_without_resolver_is_a_patch(resolve: Resolver) -> None:
    assert classify_attack(_real(resolve, "Defender_Buff.Dark_Miasma.Tar_Patch")).role is Role.PATCH


def test_nova_form_toggle_is_e37_and_nova_attack_carries_its_mode(resolve: Resolver) -> None:
    form = _real(resolve, "Peacebringer_Offensive.Luminous_Blast.Bright_Nova")
    with pytest.raises(ValueError, match="E37"):
        classify_attack(form, resolve=resolve)
    attack = classify_attack(_real(resolve, "Peacebringer_Offensive.Luminous_Blast.Bright_Nova_Blast"))
    assert attack.modes_required == ("Peacebringer_Blaster_Mode",)
    assert classify_attack(_real(resolve, "Sentinel_Ranged.Archery.Aim")).modes_required == ()


def _tpl(attrib: str, aspect: str, target: str, table: str, scale: float = 1.0, **params: Any) -> dict[str, Any]:
    template: dict[str, Any] = {"attribs": [attrib], "aspect": aspect, "target": target, "table": table, "scale": scale}
    if params:
        template["params"] = params
    return {"templates": [template], "child_effects": []}


def test_ally_resist_shield_is_not_a_debuff() -> None:
    shield = _tpl("Smashing_Dmg", "Resistance", "AnyAffected", "Ranged_Res_Dmg", 2.0)
    with pytest.raises(ValueError, match="E37"):
        classify_attack(_power(effects=[shield]))


def test_positive_scale_on_a_debuff_table_is_still_a_debuff() -> None:
    debuff = _tpl("Smashing_Dmg", "Resistance", "AnyAffected", "Ranged_Debuff_Res_Dmg", 3.0)
    assert classify_attack(_power(effects=[debuff])).role is Role.SUPPORT
    assert classify_attack(_power(type="Auto", effects=[debuff])).role is Role.PERSISTENT_DEBUFF


def test_heal_dmg_is_neither_damage_nor_resistance() -> None:
    heal = _tpl("Heal_Dmg", "Resistance", "AnyAffected", "Ranged_Res_Dmg", -2.0)
    with pytest.raises(ValueError, match="E37"):
        classify_attack(_power(effects=[heal]))
    healing_hit = _tpl("Heal_Dmg", "Absolute", "AnyAffected", "Ranged_Damage")
    with pytest.raises(ValueError, match="E37"):
        classify_attack(_power(effects=[healing_hit]))


def test_team_damage_buff_click_is_support_and_toggle_is_e37() -> None:
    team = _tpl("Fire_Dmg", "Strength", "AnyAffected", "Ranged_Buff_Dmg")
    assert classify_attack(_power(effects=[team])).role is Role.SUPPORT
    with pytest.raises(ValueError, match="E37"):
        classify_attack(_power(type="Toggle", effects=[team]))


def test_self_buff_click_with_pets_target() -> None:
    mine = _tpl("Fire_Dmg", "Strength", "SelfAndPets", "Melee_Buff_Dmg")
    assert classify_attack(_power(effects=[mine])).role is Role.BUFF_CLICK


def test_entity_roles_follow_linked_effects() -> None:
    spawn = _tpl("Create_Entity", "Current", "Self", "Ones", redirects=["L.Res"])
    pets: dict[str, Mapping[str, Any]] = {
        "L.Res": {"effects": [_tpl("Fire_Dmg", "Resistance", "AnyAffected", "Ranged_Debuff_Res_Dmg", -3.0)]},
        "L.Dmg": {"effects": [_tpl("Fire_Dmg", "Absolute", "AnyAffected", "Ranged_Damage")]},
    }
    assert classify_attack(_power(effects=[spawn]), resolve=pets.get).role is Role.SUPPORT
    both = _tpl("Create_Entity", "Current", "Self", "Ones", redirects=["L.Res", "L.Dmg", "L.Gone"])
    assert classify_attack(_power(effects=[both]), resolve=pets.get).role is Role.PATCH


def test_entity_def_powers_are_resolved_through_the_entity_file() -> None:
    spawn = _tpl("Create_Entity", "Current", "Self", "Ones", entity_def="Ent_X")
    lookup: dict[str, Mapping[str, Any]] = {
        "Ent_X": {"defaults": {"power_full_names": ["L.Res"]}},
        "L.Res": {"effects": [_tpl("Fire_Dmg", "Resistance", "AnyAffected", "Ranged_Debuff_Res_Dmg", -3.0)]},
    }
    assert classify_attack(_power(effects=[spawn]), resolve=lookup.get).role is Role.SUPPORT
    assert classify_attack(_power(effects=[spawn]), resolve=lambda _: None).role is Role.PATCH


def test_execute_power_self_buff_counts_whatever_its_target() -> None:
    link = _tpl("Execute_Power", "Absolute", "Self", "Ranged_Ones", power_names=["L.Buff"])
    lookup: dict[str, Mapping[str, Any]] = {
        "L.Buff": {"effects": [_tpl("Fire_Dmg", "Strength", "AnyAffected", "Ranged_Buff_Dmg")]}
    }
    assert classify_attack(_power(effects=[link]), resolve=lookup.get).role is Role.BUFF_CLICK


def test_resolver_returns_none_for_missing_empty_corrupt_and_caches(tmp_path: Path) -> None:
    _write(tmp_path, "A.B.Good", json.dumps({"full_name": "A.B.Good"}))
    _write(tmp_path, "A.B.Empty", "")
    _write(tmp_path, "A.B.Bad", "{")
    lookup = canonical_resolver(tmp_path)
    assert lookup("A.B.Good") == {"full_name": "A.B.Good"}
    assert (lookup("A.B.Gone"), lookup("A.B.Empty"), lookup("A.B.Bad")) == (None, None, None)
    canonical_power_path(tmp_path, "A.B.Good").unlink()
    assert lookup("A.B.Good") is not None


def test_resolver_reads_entities_beside_the_powers_root(tmp_path: Path) -> None:
    (tmp_path / "entities").mkdir()
    (tmp_path / "entities" / "pets_x.json").write_text('{"defaults": {}}', encoding="utf-8")
    (tmp_path / "entities" / "pets_bad.json").write_text("{", encoding="utf-8")
    lookup = canonical_resolver(tmp_path / "powers")
    assert lookup("Pets_X") == {"defaults": {}}
    assert (lookup("Pets_Y"), lookup("Pets_Bad")) == (None, None)
