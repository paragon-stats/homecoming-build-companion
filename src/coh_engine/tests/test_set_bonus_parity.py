"""Mids-parity goldens: the set-bonus virtual power and its folded totals.

``shield_scrapper_set_bonuses`` slots real SetO pieces (Luck of the Gambler,
Red Fortune) that trip tier bonuses, per-enhancement uniques, and the Rule of
Five. The Mids dump harness assembled ``CurrentBuild.SetBonusVirtualPower`` and
dumped its effects (``set_bonus_virtual_power.json``); the port must reproduce the
effect sequence and each magnitude, then fold them into ``Totals``/``TotalsCapped``
at IEEE-754 single precision.

The generic-IO fixtures carry no set bonuses, so their virtual power is empty —
the regression anchor proving the enhancement pass is a no-op without set data.

Re-baseline (re-run ``tools/mids-dump`` with the builds dir) after any Homecoming
DB update.
"""

import json
from pathlib import Path
from typing import Any

import pytest

from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.attribmod import AttribMods, load_attribmod
from coh_engine.base_totals import (
    EngineConfig,
    EnumMaps,
    ServerData,
    compute_base_totals,
    load_engine_config,
    load_enum_maps,
    load_server_data,
)
from coh_engine.effect import effect_mag, load_powers_effects
from coh_engine.endurance_budget import MIDS_TOTALS_SCALAR_FIELDS, MIDS_TOTALS_VECTOR_FIELDS
from coh_engine.enhancement import EnhancementRecord, load_build_slots, load_enhancement_effects
from coh_engine.maths import MathTables, f32, load_maths
from coh_engine.set_bonuses import SetBonusDb, build_set_bonus_power, load_set_bonus_db

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"

SET_BONUS_BUILD = "shield_scrapper_set_bonuses"
MIXED_SETS_BUILD = "shield_scrapper_mixed_sets"
GLOBAL_ENDRDX_BUILD = "shield_scrapper_global_endrdx"
SET_BONUS_BUILDS = [SET_BONUS_BUILD, MIXED_SETS_BUILD, GLOBAL_ENDRDX_BUILD]
NO_SET_BONUS_BUILDS = ["shield_scrapper_slotted", "hasten_2rech", "shield_scrapper_noslots"]

VECTOR_FIELDS = MIDS_TOTALS_VECTOR_FIELDS
# Speed caps, stealth and threat are not compared for these builds.
_NOT_COMPARED = {"MaxFlySpd", "MaxJumpSpd", "StealthPvE", "StealthPvP", "ThreatLevel", "MaxRunSpd"}
SCALAR_FIELDS = {k: v for k, v in MIDS_TOTALS_SCALAR_FIELDS.items() if k not in _NOT_COMPARED}


@pytest.fixture(scope="module")
def mods() -> AttribMods:
    return load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")


@pytest.fixture(scope="module")
def classes() -> ArchetypeDb:
    return load_archetypes(MIDS / "archetypes.json")


@pytest.fixture(scope="module")
def enums() -> EnumMaps:
    return load_enum_maps(MIDS / "enums.json")


@pytest.fixture(scope="module")
def config() -> EngineConfig:
    return load_engine_config(MIDS / "config.json")


@pytest.fixture(scope="module")
def server() -> ServerData:
    return load_server_data(MIDS / "server_data.json")


@pytest.fixture(scope="module")
def tables() -> MathTables:
    return load_maths(FIXTURES / "Maths.mhd")


@pytest.fixture(scope="module")
def enh_db() -> dict[int, EnhancementRecord]:
    return dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))


@pytest.fixture(scope="module")
def set_db() -> SetBonusDb:
    return load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json")


def _golden_vp(name: str) -> dict[str, Any]:
    with open(MIDS / "builds" / name / "set_bonus_virtual_power.json", encoding="utf-8") as fh:
        data: dict[str, Any] = json.load(fh)
    return data


def _totals(name: str) -> dict[str, Any]:
    with open(MIDS / "builds" / name / "totals.json", encoding="utf-8") as fh:
        data: dict[str, Any] = json.load(fh)
    return data


def _build_virtual_power(name: str, enh_db: dict[int, EnhancementRecord], set_db: SetBonusDb) -> Any:
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / name / "slots.json")
    return build_set_bonus_power(powers, slots, enh_db, set_db, force_level=50, disable_pve=False)


@pytest.mark.parametrize("name", SET_BONUS_BUILDS)
def test_assembled_virtual_power_matches_golden_sequence(
    name: str, enh_db: dict[int, EnhancementRecord], set_db: SetBonusDb
) -> None:
    """The assembled virtual power reproduces Mids' effect sequence exactly.

    Sequence order is load-bearing (float32 bucket sums are order-dependent), so
    this asserts positional equality of the structural key, not a multiset.
    """
    virtual = _build_virtual_power(name, enh_db, set_db)
    golden = _golden_vp(name)
    assert len(virtual.effects) == golden["EffectCount"]
    got = [(fx.effect_type, fx.damage_type, fx.mez_type, fx.et_modifies) for fx in virtual.effects]
    want = [(e["EffectType"], e["DamageType"], e["MezType"], e["ETModifies"]) for e in golden["Effects"]]
    assert got == want


@pytest.mark.parametrize("name", SET_BONUS_BUILDS)
def test_assembled_virtual_power_magnitudes_match_golden(
    name: str, mods: AttribMods, classes: ArchetypeDb, set_db: SetBonusDb, enh_db: dict[int, EnhancementRecord]
) -> None:
    """Each assembled bonus effect's computed Mag matches the dumped Mag."""
    virtual = _build_virtual_power(name, enh_db, set_db)
    at_index = classes.nid_from_uid_class(_totals(name)["Class"])
    golden = _golden_vp(name)
    for fx, want in zip(virtual.effects, golden["Effects"], strict=True):
        assert effect_mag(fx, virtual, mods, classes, at_index) == f32(want["Mag"])


def test_unique_and_tier_co_activate_from_two_pieces(enh_db: dict[int, EnhancementRecord], set_db: SetBonusDb) -> None:
    """Two Luck of the Gambler pieces (one the +Recharge unique) yield two bonuses.

    Deflection slots LotG_F (+Global Recharge, a >=1-piece special) and LotG_E: the
    2-piece count fires the Regeneration tier AND the +Recharge special — the exact
    "unique plus tier" case, distinct virtual buffs from the same set in one power.
    """
    powers = load_powers_effects(MIDS / "builds" / MIXED_SETS_BUILD / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / MIXED_SETS_BUILD / "slots.json")
    deflection = next(p.build_index for p in powers if p.full_name.endswith(".Deflection"))
    virtual = build_set_bonus_power(
        [p for p in powers if p.build_index == deflection],
        {deflection: slots[deflection]},
        enh_db,
        set_db,
        force_level=50,
        disable_pve=False,
    )
    kinds = {(fx.effect_type, fx.et_modifies) for fx in virtual.effects}
    assert ("Enhancement", "RechargeTime") in kinds  # the +Recharge unique (>=1 piece)
    assert ("Regeneration", "None") in kinds  # the 2-piece tier


def test_three_sets_in_one_power_fire_three_distinct_tiers(
    enh_db: dict[int, EnhancementRecord], set_db: SetBonusDb
) -> None:
    """Weave slots three different sets (2 pieces each); each fires its own first tier.

    Luck of the Gambler -> Regeneration, Kismet -> Recovery, Red Fortune -> Resistance:
    the per-set tally keeps the three counts independent, so all three 2-piece tiers
    activate from one 6-slot power.
    """
    powers = load_powers_effects(MIDS / "builds" / MIXED_SETS_BUILD / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / MIXED_SETS_BUILD / "slots.json")
    weave = next(p.build_index for p in powers if p.full_name.endswith(".Weave"))
    virtual = build_set_bonus_power(
        [p for p in powers if p.build_index == weave],
        {weave: slots[weave]},
        enh_db,
        set_db,
        force_level=50,
        disable_pve=False,
    )
    effect_types = {fx.effect_type for fx in virtual.effects}
    assert {"Regeneration", "Recovery", "Resistance"} <= effect_types


def test_rule_of_five_caps_luck_of_the_gambler_recharge(
    enh_db: dict[int, EnhancementRecord], set_db: SetBonusDb
) -> None:
    """Six slotted Luck of the Gambler +Recharge specials fold as five (Rule of Five).

    The fixture slots the +Recharge unique in six powers; the exact-bonus-id counter
    drops the sixth, so exactly five ``Enhancement/RechargeTime`` effects of magnitude
    0.075 survive (the seventh RechargeTime effect is Red Fortune's 0.05 tier).
    """
    virtual = _build_virtual_power(SET_BONUS_BUILD, enh_db, set_db)
    lotg_recharge = [
        fx
        for fx in virtual.effects
        if fx.effect_type == "Enhancement" and fx.et_modifies == "RechargeTime" and abs(fx.scale - 0.075) < 1e-6
    ]
    assert len(lotg_recharge) == 5


@pytest.mark.parametrize("name", SET_BONUS_BUILDS)
def test_set_bonus_totals_match_mids(
    name: str,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
    set_db: SetBonusDb,
) -> None:
    """Full Totals/TotalsCapped parity for the set-bonus builds — the end-to-end anchor.

    ``shield_scrapper_set_bonuses`` exercises every fold path: typed Defense/Resistance
    (buff pass), BuffHaste from the enhancement pass (LotG +Rech specials + Red Fortune
    recharge tier), BuffAcc via IsGlobalAccuracySource (LotG accuracy tier), and BuffDam
    from a set-bonus DamageBuff. ``shield_scrapper_mixed_sets`` adds the mixed-set and
    unique-plus-tier cases (HPRegen/EndRec/BuffHaste from three sets in one power).
    """
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / name / "slots.json")
    expected = _totals(name)
    result = compute_base_totals(
        powers,
        class_name=expected["Class"],
        mods=mods,
        classes=classes,
        enums=enums,
        config=config,
        server=server,
        slots=slots,
        enh_db=enh_db,
        tables=tables,
        set_db=set_db,
    )
    for side, got in (("Totals", result.totals), ("TotalsCapped", result.totals_capped)):
        want = expected[side]
        for json_key, attr in VECTOR_FIELDS.items():
            got_vec = getattr(got, attr)
            for i, cell in enumerate(want[json_key]):
                assert f32(cell) == got_vec[i], f"{side}.{json_key}[{i}]"
        for json_key, attr in SCALAR_FIELDS.items():
            assert f32(want[json_key]) == getattr(got, attr), f"{side}.{json_key}"


def test_global_endurance_discount_folds_into_toggle_end_use(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
    set_db: SetBonusDb,
) -> None:
    """A global endurance-discount set bonus reduces every toggle's EndUse (Mids Pass3).

    Reactive Defenses' 5-piece grants ``Enhancement/EnduranceDiscount +0.0375`` — a
    global ``BuffEndRdx`` that folds into each running toggle's cost divisor, not just
    the powers slotting it. The buffed toggle cost, and hence ``Totals.EndUse``, must
    match Mids with that global term applied.
    """
    name = GLOBAL_ENDRDX_BUILD
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / name / "slots.json")
    expected = _totals(name)
    result = compute_base_totals(
        powers,
        class_name=expected["Class"],
        mods=mods,
        classes=classes,
        enums=enums,
        config=config,
        server=server,
        slots=slots,
        enh_db=enh_db,
        tables=tables,
        set_db=set_db,
    )
    assert result.totals.buff_end_rdx == f32(expected["Totals"]["BuffEndRdx"])
    assert result.totals.buff_end_rdx > 0.0  # the global term is actually present
    assert result.totals.end_use == f32(expected["Totals"]["EndUse"])


@pytest.mark.parametrize("name", NO_SET_BONUS_BUILDS)
def test_generic_io_builds_have_empty_virtual_power(
    name: str, enh_db: dict[int, EnhancementRecord], set_db: SetBonusDb
) -> None:
    """Generic-IO fixtures slot no set IOs, so the virtual power is empty (matches Mids)."""
    assert _golden_vp(name)["EffectCount"] == 0
    virtual = _build_virtual_power(name, enh_db, set_db)
    assert virtual.effects == ()


def test_set_db_is_a_no_op_without_set_ios(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
    set_db: SetBonusDb,
) -> None:
    """Passing ``set_db`` for a build with no set IOs changes nothing (empty virtual power)."""
    name = "shield_scrapper_slotted"
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / name / "slots.json")
    call: dict[str, Any] = {
        "class_name": _totals(name)["Class"],
        "mods": mods,
        "classes": classes,
        "enums": enums,
        "config": config,
        "server": server,
        "slots": slots,
        "enh_db": enh_db,
        "tables": tables,
    }
    without = compute_base_totals(powers, **call)
    with_set_db = compute_base_totals(powers, set_db=set_db, **call)
    assert without.totals == with_set_db.totals
    assert without.totals_capped == with_set_db.totals_capped
