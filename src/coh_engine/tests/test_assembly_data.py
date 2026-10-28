"""Tests for coh_engine.assembly_data on hand-made records in the export's shapes."""

import json
from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest

from coh_engine.assembly_data import (
    EFFECT_KEYS,
    AssemblyPower,
    BuildPowerAssembly,
    load_assembled_powers,
    load_assembly_powers,
    load_build_power_assembly,
)
from coh_engine.effect import Effect, Power, _parse_effect
from coh_engine.enhancement import load_enhancement_effects
from coh_engine.maths import f32

MakePower = Callable[..., Power]
MakeAssemblyPower = Callable[..., AssemblyPower]
MakeBuildPower = Callable[..., BuildPowerAssembly]

_EXTENSION = {
    "Summon": "Set_Bonus.Set_Bonus.Force_Feedback",
    "nSummon": 8120,
    "Duration": 5.0,
    "EffectId": "Ef_1",
    "Reward": "",
    "ActiveConditionals": [{"Key": "Pool.Fighting.Boxing:Active", "Value": "true"}],
    "IsEnhancementEffect": True,
    "Absorbed_Power_nID": 77,
}


def _effect_record(index: int = 0, **over: Any) -> dict[str, Any]:
    base: dict[str, Any] = {
        "Index": index,
        "EffectType": "GrantPower",
        "DamageType": "None",
        "MezType": "None",
        "ETModifies": "None",
        "Scale": 1.0,
        "nMagnitude": 1.0,
        "nDuration": 5.0,
        "AttribType": "Magnitude",
        "Aspect": "Cur",
        "ModifierTable": "Melee_Ones",
        "nModifierTable": 0,
        "ToWho": "Self",
        "PvMode": "Any",
        "Stacking": "No",
        "Suppression": 0,
        "Buffable": False,
        "Resistible": False,
        "IgnoreED": False,
        "IgnoreScaling": False,
        "VariableModified": False,
        "BaseProbability": 0.33333334,
        "Probability": 0.33333334,
        "ProcsPerMinute": 0.0,
        "Ticks": 0,
        "DelayedTime": 0.0,
        "EffectClass": "Primary",
        "SpecialCase": "None",
        "nIDClassName": -1,
        "Absorbed_Effect": False,
        "Absorbed_PowerType": "Auto_",
        "Absorbed_Class_nID": -1,
        "ActiveConditionalsCount": 1,
        "ExprMagnitude": "",
        "ExprDuration": "",
        "ExprProbability": "",
        "DisplayPercentage": False,
        **_EXTENSION,
    }
    return {**base, **over}


def _power_record(**over: Any) -> dict[str, Any]:
    base: dict[str, Any] = {
        "Nid": 9213,
        "FullName": "Boosts.Crafted_Force_Feedback_F.Crafted_Force_Feedback_F",
        "PowerType": "Auto_",
        "PowerSetType": "Boost",
        "EntitiesAffected": ["Caster"],
        "EntitiesAutoHit": ["Caster", "Friend"],
        "ActivatePeriod": 0.5,
        "RechargeTime": 10.0,
        "BaseRechargeTime": 10.0,
        "CastTime": 1.17,
        "AoEModifier": 1.5,
        "Enhancements": [3, 4],
        "HasGrantPowerEffect": True,
        "HasPowerOverrideEffect": False,
        "AbsorbSummonEffects": False,
        "AbsorbSummonAttributes": True,
        "Effects": [_effect_record()],
    }
    return {**base, **over}


def _write(tmp_path: Path, name: str, data: Any) -> Path:
    path = tmp_path / name
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


def test_assembly_power_record_loads_every_field(tmp_path: Path) -> None:
    powers = load_assembly_powers(_write(tmp_path, "assembly_powers.json", [_power_record()]))
    power = powers[9213]
    assert power.power_set_type == "Boost"
    assert power.entities_auto_hit == frozenset({"Caster", "Friend"})
    assert power.cast_time == f32(1.17)
    assert power.aoe_modifier == f32(1.5)
    assert power.enhancements == frozenset({3, 4})
    assert (power.has_grant_power_effect, power.absorb_summon_attributes) == (True, True)
    (fx,) = power.effects
    assert fx.summon == "Set_Bonus.Set_Bonus.Force_Feedback"
    assert fx.n_summon == 8120
    assert fx.duration == f32(5.0)
    assert fx.effect_id == "Ef_1"
    assert fx.conditionals == (("Pool.Fighting.Boxing:Active", "true"),)
    assert fx.is_enhancement_effect
    assert fx.absorbed_power_nid == 77


@pytest.mark.parametrize(
    "record",
    [
        {k: v for k, v in _power_record().items() if k != "AoEModifier"},
        _power_record(Effects=[{k: v for k, v in _effect_record().items() if k != "nSummon"}]),
    ],
)
def test_assembly_power_record_missing_a_key_is_refused(record: dict[str, Any], tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="E85"):
        load_assembly_powers(_write(tmp_path, "assembly_powers.json", [record]))


def _build_files(
    tmp_path: Path, *, effects: list[dict[str, Any]] | None = None, slots: Any = None
) -> tuple[Path, Path]:
    extended = {"Index": 0, **_EXTENSION}
    record = {
        k: v
        for k, v in _power_record().items()
        if k not in ("Nid", "FullName", "PowerType", "ActivatePeriod", "RechargeTime", "CastTime", "Enhancements")
    }
    record = {
        **record,
        "BuildIndex": 3,
        "SubPowers": [{"NIDPower": 12, "StatInclude": True}],
        "Effects": [extended] if effects is None else effects,
    }
    slot_records = [{"BuildIndex": 3, "ProcInclude": True, "Slots": []}] if slots is None else slots
    return _write(tmp_path, "power_assembly.json", [record]), _write(tmp_path, "slots.json", slot_records)


def _build_power(make_power: MakePower) -> Power:
    raw = {k: v for k, v in _effect_record().items() if k not in _EXTENSION}
    return make_power(
        _parse_effect(raw), build_index=3, nid_power=500, full_name="Pool.Speed.Hasten", enhancements=(9,)
    )


def test_build_power_record_joins_onto_its_power(tmp_path: Path, make_power: MakePower) -> None:
    power = _build_power(make_power)
    assert power.effects[0].summon == ""  # a powers_effects.json record carries no assembly keys
    build = load_build_power_assembly(*_build_files(tmp_path), [power])[3]
    assert build.proc_include
    assert build.sub_powers == ((12, True),)
    assert (build.power.nid, build.power.full_name, build.power.enhancements) == (
        500,
        "Pool.Speed.Hasten",
        frozenset({9}),
    )
    assert build.power.aoe_modifier == f32(1.5)
    (fx,) = build.power.effects
    assert fx.summon == "Set_Bonus.Set_Bonus.Force_Feedback"
    assert fx.conditionals == (("Pool.Fighting.Boxing:Active", "true"),)
    assert fx.scale == power.effects[0].scale


@pytest.mark.parametrize(
    ("files", "code"),
    [
        ({"effects": []}, "E85"),  # the power's effect has no extended keys
        ({"effects": [{"Index": 0}]}, "E85"),  # an extended entry missing its keys
        ({"slots": [{"BuildIndex": 3, "Slots": []}]}, "E85"),  # no ProcInclude
        ({"slots": []}, "E86"),  # no slots record for the power
    ],
)
def test_build_power_record_gaps_are_refused(
    files: dict[str, Any], code: str, tmp_path: Path, make_power: MakePower
) -> None:
    with pytest.raises(ValueError, match=code):
        load_build_power_assembly(*_build_files(tmp_path, **files), [_build_power(make_power)])


def test_build_power_without_an_assembly_record_is_refused(tmp_path: Path, make_power: MakePower) -> None:
    assembly, slots = _build_files(tmp_path)
    other = make_power(build_index=4)
    with pytest.raises(ValueError, match="E86"):
        load_build_power_assembly(assembly, slots, [other])


def test_build_power_assembly_record_missing_a_key_is_refused(tmp_path: Path, make_power: MakePower) -> None:
    assembly, slots = _build_files(tmp_path)
    records = json.loads(assembly.read_text(encoding="utf-8"))
    del records[0]["SubPowers"]
    assembly.write_text(json.dumps(records), encoding="utf-8")
    with pytest.raises(ValueError, match="E85"):
        load_build_power_assembly(assembly, slots, [_build_power(make_power)])


def test_assembled_powers_load_in_order(tmp_path: Path) -> None:
    record = {
        "BuildIndex": 2,
        "FullName": "Pool.Speed.Hasten",
        "Effects": [_effect_record(0), _effect_record(1, EffectType="Enhancement")],
    }
    assembled = load_assembled_powers(_write(tmp_path, "assembled_powers.json", [record]))
    assert [fx.effect_type for fx in assembled[2]] == ["GrantPower", "Enhancement"]
    with pytest.raises(ValueError, match="E85"):
        load_assembled_powers(_write(tmp_path, "bad.json", [{"BuildIndex": 2, "Effects": []}]))


def test_effect_keys_cover_what_the_parser_reads() -> None:
    effect = _parse_effect(_effect_record())
    assert isinstance(effect, Effect)
    assert len(EFFECT_KEYS) == len(set(EFFECT_KEYS))


def test_enhancement_record_reads_its_own_power(tmp_path: Path) -> None:
    record = {"Nid": 640, "StaticIndex": 641, "TypeName": "SetO", "Superior": False, "nIDSet": 106, "Effects": []}
    plain = load_enhancement_effects(_write(tmp_path, "a.json", [record]))[640]
    assert (plain.is_proc, plain.power_nid) == (False, -1)
    proc = load_enhancement_effects(_write(tmp_path, "b.json", [{**record, "IsProc": True, "PowerNid": 9213}]))[640]
    assert (proc.is_proc, proc.power_nid) == (True, 9213)


def test_conftest_factories_build_the_contract_shapes(make_build_power: MakeBuildPower) -> None:
    build = make_build_power(build_index=5, proc_include=True, power_type="Click")
    assert (build.build_index, build.proc_include, build.power.power_type, build.power.effects) == (
        5,
        True,
        "Click",
        (),
    )
