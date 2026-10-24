"""Tests for the Mids reference damage input (``coh_engine.damage_reference``)."""

import json
from pathlib import Path
from typing import Any

import pytest

from coh_engine.damage_reference import load_damage_refs
from coh_engine.maths import f32

_BUILDS = Path(__file__).parent / "fixtures" / "mids" / "builds"
_TYPES = ("Smashing", "Lethal", "Fire", "Cold", "Energy", "Negative", "Toxic", "Psionic")


def _write(root: Path, name: str, data: object) -> None:
    (root / name).write_text(json.dumps(data), encoding="utf-8")


def _power(name: str, effects: list[dict[str, Any]], index: int = 0) -> tuple[dict[str, Any], dict[str, Any]]:
    """Build matching (enhanced, structural) records for one synthetic power."""
    enhanced = {
        "BuildIndex": index,
        "FullName": name,
        "DamagePerActivation": 0,
        "CastTime": 1.5,
        "RechargeTime": 10,
        "EndCost": 5,
        "Effects": [
            {"Index": i, "Math_Mag": e["mag"], "Math_Duration": e.get("dur", 0)} for i, e in enumerate(effects)
        ],
    }
    structural = {
        "BuildIndex": index,
        "FullName": name,
        "PowerType": "Click",
        "ActivatePeriod": 0,
        "StatInclude": False,
        "Effects": [
            {"Index": i, "EffectType": e["kind"], "ToWho": e["who"], "DamageType": e["type"]}
            for i, e in enumerate(effects)
        ],
    }
    return enhanced, structural


def _dump(root: Path, *powers: tuple[dict[str, Any], dict[str, Any]]) -> Path:
    _write(root, "enhanced_powers.json", [p[0] for p in powers])
    _write(root, "powers_effects.json", [p[1] for p in powers])
    return root


def _res(mag: float, types: tuple[str, ...] = _TYPES) -> list[dict[str, Any]]:
    return [{"kind": "Resistance", "who": "Target", "type": t, "mag": mag, "dur": 20} for t in types]


def test_damaging_attack_from_fixture() -> None:
    refs = load_damage_refs(_BUILDS / "shield_scrapper_slotted")
    ref = refs["Scrapper_Melee.Dark_Melee.Soul_Drain"]
    assert ref.damage_per_activation == f32(68.81765)
    assert ref.cast_time == f32(2.37)
    assert ref.recharge_time == 120
    assert ref.end_cost == f32(15.6)
    assert (ref.power_type, ref.stat_include, ref.activate_period) == ("Click", False, 0.0)


def test_self_damage_buff_sums_stacking_groups_per_type() -> None:
    refs = load_damage_refs(_BUILDS / "shield_scrapper_slotted")
    ref = refs["Scrapper_Melee.Dark_Melee.Soul_Drain"]
    assert ref.self_damage_buff == pytest.approx(0.6)  # +10% and +50% groups stack
    assert ref.self_damage_buff_duration == 30


def test_self_damage_buff_click_from_fixture() -> None:
    ref = load_damage_refs(_BUILDS / "arsenal_dominator_cardiac")["Dominator_Assault.Arsenal_Assault.Power_Up"]
    assert ref.self_damage_buff == f32(0.34)
    assert ref.self_damage_buff_duration == 10
    assert ref.damage_per_activation == 0


def test_target_damage_debuff_is_not_a_self_buff() -> None:
    ref = load_damage_refs(_BUILDS / "arsenal_dominator_cardiac")["Dominator_Control.Arsenal_Control.Tear_Gas"]
    assert ref.self_damage_buff == 0.0
    assert ref.target_res_debuff == 0.0


@pytest.mark.parametrize("build", sorted(p.name for p in _BUILDS.iterdir() if p.is_dir()))
def test_every_fixture_build_loads(build: str) -> None:
    refs = load_damage_refs(_BUILDS / build)
    assert refs
    assert all(r.full_name == k for k, r in refs.items())


def test_redirected_power_keeps_picked_name_and_redirect_damage() -> None:
    ref = load_damage_refs(_BUILDS / "arsenal_dominator_cardiac")["Dominator_Assault.Arsenal_Assault.Sniper_Rifle"]
    assert ref.damage_per_activation == f32(237.73366)


def test_mapping_is_read_only() -> None:
    refs = load_damage_refs(_BUILDS / "shield_scrapper_slotted")
    with pytest.raises(TypeError):
        refs["x"] = None  # type: ignore[index]


def test_target_res_debuff_all_types(tmp_path: Path) -> None:
    # No fixture build carries a target -Res debuff, so this branch is covered synthetically.
    root = _dump(tmp_path, _power("P.Venom", _res(-0.2)))
    ref = load_damage_refs(root)["P.Venom"]
    assert ref.target_res_debuff == f32(0.2)
    assert ref.target_res_debuff_duration == 20


def test_target_res_debuff_on_a_toggle_is_extracted_like_a_click(tmp_path: Path) -> None:
    """A -Res toggle (Enervating Field, Venomous Gas) folds the same as a click; only PowerType differs."""
    enhanced, structural = _power("P.Field", _res(-0.3))
    structural.update(PowerType="Toggle", ActivatePeriod=2.0, StatInclude=True)
    ref = load_damage_refs(_dump(tmp_path, (enhanced, structural)))["P.Field"]
    assert (ref.power_type, ref.activate_period) == ("Toggle", 2.0)
    assert (ref.target_res_debuff, ref.target_res_debuff_duration) == (f32(0.3), 20)


def test_target_res_debuff_takes_weakest_type(tmp_path: Path) -> None:
    effects = _res(-0.3)
    effects[-1]["mag"] = -0.1  # weakest type last, so "first" and "weakest" differ
    effects[-1]["dur"] = 7
    ref = load_damage_refs(_dump(tmp_path, _power("P.Weak", effects)))["P.Weak"]
    assert ref.target_res_debuff == f32(0.1)
    assert ref.target_res_debuff_duration == 7


@pytest.mark.parametrize("name", ["enhanced_powers.json", "powers_effects.json"])
def test_corrupt_dump_file_is_e43(tmp_path: Path, name: str) -> None:
    root = _dump(tmp_path, _power("P.Any", []))
    (root / name).write_text("", encoding="utf-8")
    with pytest.raises(ValueError, match=f"E43: .*{name.replace('.', chr(92) + '.')} is not valid JSON"):
        load_damage_refs(root)


def test_partial_type_debuff_and_resistance_buff_are_ignored(tmp_path: Path) -> None:
    partial = _power("P.Partial", _res(-0.2, _TYPES[:3]))
    positive = _power("P.Positive", _res(0.2), index=1)
    refs = load_damage_refs(_dump(tmp_path, partial, positive))
    assert refs["P.Partial"].target_res_debuff == 0.0
    assert refs["P.Positive"].target_res_debuff == 0.0


def test_missing_file_is_e40(tmp_path: Path) -> None:
    _write(tmp_path, "enhanced_powers.json", [])
    with pytest.raises(ValueError, match=r"E40: .*powers_effects\.json not found"):
        load_damage_refs(tmp_path)


def test_power_in_one_file_only_is_e41(tmp_path: Path) -> None:
    both, other = _power("P.Both", []), _power("P.Extra", [], index=1)
    _write(tmp_path, "enhanced_powers.json", [both[0], other[0]])
    _write(tmp_path, "powers_effects.json", [both[1]])
    with pytest.raises(ValueError, match=r"E41: .*only in enhanced_powers\.json: \[1\]"):
        load_damage_refs(tmp_path)


def test_effect_missing_from_enhanced_is_e42(tmp_path: Path) -> None:
    enhanced, structural = _power("P.Gap", _res(-0.2, _TYPES[:1]))
    enhanced["Effects"] = []
    with pytest.raises(ValueError, match=r"E42: P\.Gap effect Index 0"):
        load_damage_refs(_dump(tmp_path, (enhanced, structural)))
