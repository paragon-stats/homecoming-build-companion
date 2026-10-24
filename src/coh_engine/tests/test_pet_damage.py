"""Pet damage: summon powers resolved from canonical data, and the pet damage source."""

import json
from math import inf
from pathlib import Path
from typing import Any

import pytest

from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.attack_chain import arcana_cast_time, plan_attack_chain
from coh_engine.attack_profile import classify_attack, load_canonical_powers
from coh_engine.attribmod import AttribMods, load_attribmod
from coh_engine.damage_model import (
    SOURCE_PET,
    AttackProfile,
    ChainAttack,
    DamageContext,
    Delivery,
    EnduranceBudget,
    Movement,
    PetSummon,
    Role,
    Scenario,
    Shape,
    Stance,
    evaluate_damage,
)
from coh_engine.pet_damage import DEFAULT_PET_DAMAGE_ENHANCEMENT, build_pet_summons, pet_source

REPO = Path(__file__).parents[3]
CANONICAL = REPO / "data" / "canonical"
FIXTURES = Path(__file__).parent / "fixtures"
SPIDERLINGS = "Training_Gadgets.Crab_Spider_Training.Summon_Spiderlings"
SOLDIERS = "Mastermind_Summon.Mercenaries.Soldiers"
CLAW = "Villain_Pets.Arachnobot_Spiderling.Claw"

MODS = load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")
CLASSES = load_archetypes(FIXTURES / "mids" / "archetypes.json")
UNLIMITED = EnduranceBudget(max_end=1.0e9, recovery_per_sec=0.0, toggle_drain_per_sec=0.0)
MOVEMENT = Movement(speed_ft_per_sec=20.0, standoff_ft=40.0)


def _summon_attack(root: Path, name: str) -> ChainAttack:
    power = load_canonical_powers(root / "powers", [name])[name]
    return ChainAttack(
        name,
        0.0,
        power["activation_time"],
        power["recharge_time"],
        power["endurance_cost"],
        classify_attack(power),
    )


def _build(name: str, **kw: Any) -> tuple[PetSummon, ...]:
    return build_pet_summons(
        [_summon_attack(CANONICAL, name)], canonical_root=CANONICAL, attribmod=MODS, classes=CLASSES, **kw
    )


def _ctx(*pets: PetSummon) -> DamageContext:
    return DamageContext(
        attacks=(),
        budget=UNLIMITED,
        stance=Stance.MELEE,
        scenario=Scenario(targets=1),
        movement=MOVEMENT,
        archetype_class="Class_Test",
        pets=pets,
    )


def _pet_attack(name: str, damage: float, cast: float, recharge: float) -> ChainAttack:
    profile = AttackProfile(name, Delivery.MELEE, Shape.SINGLE, Role.CHAIN, 7.0, 0.0, 0.0, 0)
    return ChainAttack(name, damage, cast, recharge, 0.0, profile)


def _pet(duration: float, count: int = 2, cast: float = 2.03, recharge: float = 100.0, end: float = 26.0) -> PetSummon:
    return PetSummon(
        summon_name="S",
        pet_name="P",
        pet_attacks=(_pet_attack("P.Hit", 100.0, 1.0, 1.0),),
        pet_count=count,
        duration=duration,
        summon_cast_time=cast,
        summon_recharge=recharge,
        summon_end_cost=end,
        damage_buff_baseline=1.95,
    )


def _single_dps(pet: PetSummon) -> float:
    plan = plan_attack_chain(
        pet.pet_attacks, stance=Stance.MELEE, movement=MOVEMENT, scenario=Scenario(), budget=UNLIMITED
    )
    assert plan.best_overall is not None
    return plan.best_overall.dps


def test_spiderlings_resolve_from_canonical_data() -> None:
    (pet,) = _build(SPIDERLINGS)
    assert (pet.pet_name, pet.pet_count, pet.duration) == ("Spiderling", 3, 240.0)
    assert pet.summon_recharge == 600.0
    assert pet.summon_cast_time == pytest.approx(2.03)
    assert pet.summon_end_cost == 26.0
    assert pet.damage_buff_baseline == pytest.approx(1.0 + DEFAULT_PET_DAMAGE_ENHANCEMENT)
    # Ranged_Levelminus2 at caster level 50 (row 49) holds 48.
    assert "level 48" in pet.label
    assert {a.full_name for a in pet.pet_attacks} == {
        "Villain_Pets.Arachnobot_Spiderling.Chain_Gun",
        CLAW,
    }
    assert all(a.end_cost == 0.0 and a.profile.role is Role.CHAIN for a in pet.pet_attacks)


def test_claw_damage_hand_calculation() -> None:
    """Claw: scale 0.68 x |Melee_Damage| 55.61 (Class_Minion_Pets, row 47) x (1 + 0.95) = 73.7389.

    Spiderling level 48 reads row 47 of the Melee_Damage table; 0.68 x 55.61 = 37.8148, x 1.95 = 73.7389.
    """
    (pet,) = _build(SPIDERLINGS)
    claw = next(a for a in pet.pet_attacks if a.full_name == CLAW)
    assert claw.damage == pytest.approx(0.68 * 55.61 * 1.95, rel=1e-5)
    assert (claw.cast_time, claw.recharge_time) == (0.67, 2.0)


def test_damage_enhancement_override_and_ticks() -> None:
    """Chain Gun: 0.28 x |Ranged_Damage| 44.488 x 4 ticks (1 s at 0.3 s, first on application) = 49.8266 unenhanced."""
    (pet,) = _build(SPIDERLINGS, damage_enhancement={SPIDERLINGS: 0.0})
    gun = next(a for a in pet.pet_attacks if a.full_name.endswith("Chain_Gun"))
    assert gun.damage == pytest.approx(0.28 * 44.488 * 4, rel=1e-5)
    assert pet.damage_buff_baseline == 1.0


def test_pet_level_follows_caster_level_and_ones_tables() -> None:
    (low,) = _build(SPIDERLINGS, caster_level=30)
    assert "level 28" in low.label
    soldiers = _build(SOLDIERS, caster_level=40)
    assert all("level 40" in p.label for p in soldiers)


def test_mastermind_soldiers_resolve_two_entities() -> None:
    soldier, medic = _build(SOLDIERS)
    assert (soldier.pet_name, soldier.pet_count, soldier.duration) == ("Soldier", 2, inf)
    assert (medic.pet_name, medic.pet_count) == ("Medic", 1)
    # The summon's cast and endurance are charged once, on the first entity.
    assert soldier.summon_cast_time > 0 and medic.summon_cast_time == 0.0
    assert soldier.summon_end_cost > 0 and medic.summon_end_cost == 0.0
    assert soldier.summon_recharge == medic.summon_recharge == 5.0
    assert {a.full_name for a in soldier.pet_attacks} == {
        "Mastermind_Pets.Soldier.Brawl",
        "Mastermind_Pets.Soldier.ASMG_Burst",
    }
    assert all(a.damage > 0 for p in (soldier, medic) for a in p.pet_attacks)
    # ASMG Burst: the conditional (requires_expression) group is skipped, only the Fallback group counts.
    burst = next(a for a in soldier.pet_attacks if a.full_name.endswith("ASMG_Burst"))
    assert burst.damage == pytest.approx(0.29 * 26.088 * 3 * 1.95, rel=1e-5)


def test_timed_pet_uptime_and_amortisation() -> None:
    pet = _pet(duration=240.0, count=3, recharge=600.0)
    result = pet_source(_ctx(pet))
    assert result is not None
    cycle = arcana_cast_time(2.03) + 600.0
    uptime = 240.0 / cycle
    assert result.source == SOURCE_PET
    assert result.dps == pytest.approx(_single_dps(pet) * 3 * uptime, rel=1e-5)
    assert result.timeline_share == pytest.approx(arcana_cast_time(2.03) / cycle, rel=1e-5)
    assert result.end_per_sec == pytest.approx(26.0 / cycle, rel=1e-5)
    assert result.detail == (("P", result.dps),)
    assert result.damage_buff_baseline == pytest.approx(1.95)
    assert "approximate" in result.label


def test_long_lived_timed_pet_is_capped_at_full_uptime() -> None:
    pet = _pet(duration=1000.0)
    result = pet_source(_ctx(pet))
    assert result is not None
    assert result.dps == pytest.approx(_single_dps(pet) * 2, rel=1e-5)
    assert result.timeline_share > 0


def test_permanent_pet_has_full_uptime_and_no_timeline_cost() -> None:
    pet = _pet(duration=inf)
    result = pet_source(_ctx(pet))
    assert result is not None
    assert result.dps == pytest.approx(_single_dps(pet) * 2, rel=1e-5)
    assert (result.timeline_share, result.end_per_sec) == (0.0, 0.0)


def test_zero_cast_summon_adds_no_timeline_share() -> None:
    result = pet_source(_ctx(_pet(duration=240.0, cast=0.0, end=0.0)))
    assert result is not None
    assert result.timeline_share == 0.0
    assert result.dps == pytest.approx(_single_dps(_pet(240.0)) * 2, rel=1e-5)


def test_baseline_is_damage_weighted() -> None:
    weak = _pet(inf, count=1)
    strong = PetSummon("S2", "Q", (_pet_attack("Q.Hit", 300.0, 1.0, 1.0),), 1, inf, 0.0, 5.0, 0.0, 1.0)
    result = pet_source(_ctx(weak, strong))
    assert result is not None
    d_weak, d_strong = result.detail[0][1], result.detail[1][1]
    assert result.damage_buff_baseline == pytest.approx((d_weak * 1.95 + d_strong * 1.0) / (d_weak + d_strong))


def test_pet_without_attacks_contributes_nothing() -> None:
    support = PetSummon("S", "Medic", (), 1, inf, 0.0, 5.0, 0.0)
    result = pet_source(_ctx(support))
    assert result is not None
    assert (result.dps, result.damage_buff_baseline) == (0.0, 1.0)


def test_no_pets_gives_no_source() -> None:
    assert pet_source(_ctx()) is None


def test_end_to_end_through_evaluate_damage() -> None:
    pets = _build(SPIDERLINGS)
    report = evaluate_damage(_ctx(*pets), sources=[(pet_source, False)], modifiers=[])
    (result,) = report.sources
    assert result.dps > 0
    assert result.timeline_share == pytest.approx(arcana_cast_time(2.03) / (arcana_cast_time(2.03) + 600.0), rel=1e-5)
    assert report.total_dps == pytest.approx(result.dps)
    assert report.scaled_dps[SOURCE_PET] == pytest.approx(result.dps)


def test_mastermind_soldiers_dps_is_positive_and_permanent() -> None:
    soldier, medic = _build(SOLDIERS)
    result = pet_source(_ctx(soldier, medic))
    assert result is not None
    assert result.dps > 0 and result.timeline_share == 0.0
    assert [name for name, _ in result.detail] == ["Soldier", "Medic"]


# --- error paths and edge cases on synthetic canonical trees ---------------------------------------------------


def _write(root: Path, kind: str, name: str, data: dict[str, Any]) -> None:
    path = root / kind / (name.lower().replace(".", "/") + ".json" if kind == "powers" else f"{name.lower()}.json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data), encoding="utf-8")


def _template(attribs: list[str], **kw: Any) -> dict[str, Any]:
    base: dict[str, Any] = {
        "attribs": attribs,
        "aspect": "Current",
        "target": "AnyAffected",
        "table": "Ranged_Ones",
        "scale": 1.0,
        "magnitude": 1.0,
        "duration": "UntilKilled",
        "application_period": 0.0,
        "params": {"entity_def": "Tst_Pet"},
    }
    base.update(kw)
    return base


def _effect(*templates: dict[str, Any], **kw: Any) -> dict[str, Any]:
    base: dict[str, Any] = {
        "chance": 1.0,
        "ppm": 0.0,
        "requires_expression": "",
        "is_pvp": "EITHER",
        "templates": list(templates),
        "child_effects": [],
    }
    base.update(kw)
    return base


def _power(full_name: str, effects: list[dict[str, Any]], power_type: str = "Click", **kw: Any) -> dict[str, Any]:
    base: dict[str, Any] = {
        "full_name": full_name,
        "type": power_type,
        "effect_area": "SingleTarget",
        "range": 7.0,
        "radius": 0.0,
        "arc": 0.0,
        "max_targets_hit": 0,
        "allowed_boostset_cats": ["Pet Damage"],
        "activation_time": 1.0,
        "recharge_time": 2.0,
        "activate_period": 0.0,
        "endurance_cost": 5.0,
        "effects": effects,
    }
    base.update(kw)
    return base


def _damage(**kw: Any) -> dict[str, Any]:
    return _template(["Smashing_Dmg"], aspect="Absolute", table="Ranged_Damage", params=None, **kw)


def _tree(
    tmp_path: Path, summon_effects: list[dict[str, Any]], *, entity: bool = True, pets: bool = True
) -> ChainAttack:
    summon = _power("Tst.Set.Summon", summon_effects, effect_area="Location", range=60.0)
    _write(tmp_path, "powers", "Tst.Set.Summon", summon)
    if entity:
        data = {
            "name": "Tst_Pet",
            "defaults": {"character_class_name": "minion_pets", "power_full_names": ["Tst.Set.Zap", "Tst.Set.Aura"]},
            "levels": [],
        }
        _write(tmp_path, "entities", "Tst_Pet", data)
    if pets:
        zap = _power(
            "Tst.Set.Zap",
            [
                _effect(_damage(duration="1 seconds", application_period=0.5)),
                _effect(_damage(), requires_expression="Source.Mode?(x)"),
                _effect(_damage(), ppm=3.5),
                _effect(_damage(), is_pvp="PVP"),
                _effect(_damage(duration="UntilKilled", application_period=0.5)),
                _effect(_damage(), child_effects=[_effect(_damage(), is_pvp="PVP")]),
                _effect(_template(["Smashing_Dmg"], aspect="Absolute", target="Self", params=None)),
            ],
        )
        aura = _power("Tst.Set.Aura", [_effect(_damage())], power_type="Toggle")
        _write(tmp_path, "powers", "Tst.Set.Zap", zap)
        _write(tmp_path, "powers", "Tst.Set.Aura", aura)
    profile = AttackProfile("Tst.Set.Summon", Delivery.RANGED, Shape.LOCATION, Role.PET_SUMMON, 60.0, 0.0, 0.0, 0)
    return ChainAttack("Tst.Set.Summon", 0.0, 1.0, 100.0, 5.0, profile)


def _synthetic(tmp_path: Path, summon_effects: list[dict[str, Any]], **kw: Any) -> tuple[PetSummon, ...]:
    summon = _tree(tmp_path, summon_effects)
    return build_pet_summons([summon], canonical_root=tmp_path, attribmod=MODS, classes=CLASSES, **kw)


def _create(**kw: Any) -> list[dict[str, Any]]:
    return [_effect(_template(["Create_Entity"], **kw))]


def test_synthetic_pet_counts_ticks_and_skips_non_attacks(tmp_path: Path) -> None:
    """Zap: 44.488 (Ranged_Damage, Class_Minion_Pets) x 3 ticks (1 s at 0.5 s), plus 1 tick for each of two more groups = 5 x 44.488.

    The conditional, procs-per-minute, PVP-only (also as a child) and self-targeted groups are skipped.
    """
    (pet,) = _synthetic(tmp_path, _create(), damage_enhancement={"Tst.Set.Summon": 0.0})
    assert pet.pet_name == "Tst_Pet" and pet.duration == inf and pet.pet_count == 1
    (zap,) = pet.pet_attacks  # the toggle aura is not an attack
    assert zap.damage == pytest.approx(44.488 * 5, rel=1e-5)


def test_timed_duration_text_is_parsed(tmp_path: Path) -> None:
    (pet,) = _synthetic(tmp_path, _create(duration="90 seconds"))
    assert pet.duration == 90.0


def test_non_pet_power_is_rejected() -> None:
    power = load_canonical_powers(CANONICAL / "powers", ["Training_Gadgets.Training_and_Gadgets.Call_Reinforcements"])
    (name, data) = next(iter(power.items()))
    profile = classify_attack(data)
    wrong = ChainAttack(
        name, 0.0, 1.0, 1.0, 0.0, AttackProfile(name, profile.delivery, profile.shape, Role.CHAIN, 0, 0, 0, 0)
    )
    with pytest.raises(ValueError, match="E64:"):
        build_pet_summons([wrong], canonical_root=CANONICAL, attribmod=MODS, classes=CLASSES)


def test_missing_entity_file_is_e60(tmp_path: Path) -> None:
    summon = _tree(tmp_path, _create(), entity=False, pets=False)
    with pytest.raises(ValueError, match="E60: entity Tst_Pet"):
        build_pet_summons([summon], canonical_root=tmp_path, attribmod=MODS, classes=CLASSES)


def test_missing_pet_power_files_are_e60(tmp_path: Path) -> None:
    summon = _tree(tmp_path, _create(), pets=False)
    with pytest.raises(ValueError, match=r"E60: pet powers of Tst_Pet missing"):
        build_pet_summons([summon], canonical_root=tmp_path, attribmod=MODS, classes=CLASSES)


def test_unknown_class_is_e61(tmp_path: Path) -> None:
    summon = _tree(tmp_path, _create())
    with pytest.raises(ValueError, match="E61: entity Tst_Pet has class 'minion_pets'"):
        build_pet_summons([summon], canonical_root=tmp_path, attribmod=MODS, classes=ArchetypeDb(()))


def test_unknown_table_is_e61(tmp_path: Path) -> None:
    summon = _tree(tmp_path, _create(table="Ranged_Levelminus2"))
    with pytest.raises(ValueError, match=r"E61: Tst\.Set\.Summon uses modifier table 'Ranged_Levelminus2'"):
        build_pet_summons([summon], canonical_root=tmp_path, attribmod=AttribMods(()), classes=CLASSES)


def test_pet_level_below_one_is_e61(tmp_path: Path) -> None:
    summon = _tree(tmp_path, _create(table="Ranged_Levelminus2"))
    with pytest.raises(ValueError, match="E61: table Ranged_Levelminus2 gives pet level 0"):
        build_pet_summons([summon], canonical_root=tmp_path, attribmod=MODS, classes=CLASSES, caster_level=0)


@pytest.mark.parametrize(
    ("effects", "message"),
    [
        ([], "has no Create_Entity template"),
        (_create(params={"entity_def": None}), "no params.entity_def"),
        (_create(params=None), "no params.entity_def"),
        (_create(duration="5 minutes"), "duration is '5 minutes'"),
        (_create(duration="abc seconds"), "duration is 'abc seconds'"),
    ],
)
def test_unusable_summon_data_is_e62(tmp_path: Path, effects: list[dict[str, Any]], message: str) -> None:
    summon = _tree(tmp_path, effects, entity=False, pets=False)
    with pytest.raises(ValueError, match=f"E62: .*{message}"):
        build_pet_summons([summon], canonical_root=tmp_path, attribmod=MODS, classes=CLASSES)
