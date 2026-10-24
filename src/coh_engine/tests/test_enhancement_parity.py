"""Mids-parity goldens: enhanced per-power values and slotted totals.

Each slotted fixture was loaded through Mids, recomputed with
``GenerateBuffedPowerArray()``, and dumped as ``totals.json`` (buffed Totals),
``enhanced_powers.json`` (per-power buffed scalars + effect ``Math_Mag``, plus
the unenhanced ``Base`` scalars) and ``slots.json`` (the resolved slot layout).
The Python port must reproduce all of them at IEEE-754 single precision from the
same records + the enhancement DB.

The generic Invention IOs the fixtures slot carry no set bonus, so no
``SetBonusVirtualPower`` and no ``_selfEnhance`` contribution is involved — the
enhancement flows purely through the per-effect ``Math_Mag`` multiplier path.
"""

import json
from collections import defaultdict, deque
from pathlib import Path
from typing import Any

import pytest

from coh_engine import base_totals
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
from coh_engine.enh_pipeline import aggregate_and_ed
from coh_engine.enhancement import (
    EnhancementRecord,
    load_build_slots,
    load_enhancement_effects,
)
from coh_engine.maths import MathTables, f32

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"

SLOTTED = "shield_scrapper_slotted"
HASTEN_2RECH = "hasten_2rech"
DEFLECTION_INDEX = 2
HASTEN_INDEX = 6

VECTOR_FIELDS = MIDS_TOTALS_VECTOR_FIELDS
SCALAR_FIELDS = MIDS_TOTALS_SCALAR_FIELDS


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
def enh_db() -> dict[int, EnhancementRecord]:
    return dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))


def _totals_json(name: str) -> dict[str, Any]:
    with open(MIDS / "builds" / name / "totals.json", encoding="utf-8") as fh:
        data: dict[str, Any] = json.load(fh)
    return data


def _enhanced_powers(name: str) -> list[dict[str, Any]]:
    with open(MIDS / "builds" / name / "enhanced_powers.json", encoding="utf-8") as fh:
        data: list[dict[str, Any]] = json.load(fh)
    return data


def _compute(
    name: str,
    *,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> base_totals.BaseTotals:
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / name / "slots.json")
    return compute_base_totals(
        powers,
        class_name=_totals_json(name)["Class"],
        mods=mods,
        classes=classes,
        enums=enums,
        config=config,
        server=server,
        slots=slots,
        enh_db=enh_db,
        tables=tables,
    )


@pytest.mark.parametrize("name", [SLOTTED, HASTEN_2RECH])
def test_slotted_totals_match_mids(
    name: str,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """Full Totals/TotalsCapped parity for a slotted build — the end-to-end anchor.

    Deflection's three generic Defense IOs enhance its typed Defense and its
    ``ResEffect → Defense`` debuff resistance; both must reach ``Totals.Def`` /
    ``Totals.DebuffRes`` through the ED-reduced ``Math_Mag`` multiplier.
    """
    result = _compute(
        name, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    expected = _totals_json(name)
    for side, got in (("Totals", result.totals), ("TotalsCapped", result.totals_capped)):
        want = expected[side]
        for json_key, attr in VECTOR_FIELDS.items():
            got_vec = getattr(got, attr)
            for i, cell in enumerate(want[json_key]):
                assert f32(cell) == got_vec[i], f"{name}.{side}.{json_key}[{i}]"
        for json_key, attr in SCALAR_FIELDS.items():
            assert f32(want[json_key]) == getattr(got, attr), f"{name}.{side}.{json_key}"


def test_deflection_per_effect_math_mag(
    mods: AttribMods,
    classes: ArchetypeDb,
    config: EngineConfig,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
    enums: EnumMaps,
) -> None:
    """Every Deflection effect's enhanced ``Math_Mag`` matches Mids, effect by effect.

    Buffable Defense (x4) and ``ResEffect → Defense`` (x1) are enhanced by the
    Defense IOs; the non-Buffable Elusivity effects (x2) are not. Expected =
    ``base Mag x (1 + ED(Σ slot values))``.
    """
    powers = {p.build_index: p for p in load_powers_effects(MIDS / "builds" / SLOTTED / "powers_effects.json")}
    slots = load_build_slots(MIDS / "builds" / SLOTTED / "slots.json")
    at_index = classes.nid_from_uid_class(_totals_json(SLOTTED)["Class"])
    ctx = base_totals._MagContext(
        mods=mods,
        classes=classes,
        archetype_index=at_index,
        config=config,
        enhance_names=base_totals._enhance_names(enums),
    )
    included = [p for p in powers.values() if p.stat_include]
    enh_mult = base_totals._compute_enh_multipliers(
        included, slots=slots, enh_db=enh_db, tables=tables, force_level=config.force_level, ctx=ctx
    )

    deflection = powers[DEFLECTION_INDEX]
    dumped = next(p for p in _enhanced_powers(SLOTTED) if p["BuildIndex"] == DEFLECTION_INDEX)
    # Match effects by structural identity, not array position — GBPA assembly
    # can reorder the buffed effect array relative to the raw powers_effects
    # dump. Same-identity effects (Deflection has two Defense/Ranged) are paired
    # FIFO; their enhanced Math_Mag is identical, so within-key order is moot.
    dumped_by_key: dict[tuple[str, str, str, str], deque[float]] = defaultdict(deque)
    for e in dumped["Effects"]:
        dumped_by_key[(e["EffectType"], e["DamageType"], e["MezType"], e["ETModifies"])].append(f32(e["Math_Mag"]))
    for fx in deflection.effects:
        base_mag = effect_mag(fx, deflection, mods, classes, at_index)
        mult = enh_mult.get((DEFLECTION_INDEX, fx.index), 1.0)
        key = (fx.effect_type, fx.damage_type, fx.mez_type, fx.et_modifies)
        assert dumped_by_key[key].popleft() == f32(base_mag * mult), f"Deflection {key}"
    assert all(len(q) == 0 for q in dumped_by_key.values()), "unmatched dumped effect"


@pytest.mark.parametrize(("name", "rounded"), [(SLOTTED, 0.9908), (HASTEN_2RECH, 0.8332)])
def test_hasten_buffed_recharge_matches(
    name: str,
    rounded: float,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """Hasten's enhanced RechargeTime reproduces Mids from base ÷ (1 + ED aggregate).

    The recharge aggregate (0.9908 / 0.8332) is the spec golden; dividing the
    dumped base recharge by ``1 + aggregate`` must equal the dumped buffed
    recharge with no magic constant.
    """
    slots = load_build_slots(MIDS / "builds" / name / "slots.json")[HASTEN_INDEX]
    aggregate = aggregate_and_ed(slots, aspect="RechargeTime", enh_db=enh_db, force_level=50, tables=tables)
    assert round(aggregate, 4) == rounded

    dumped = next(p for p in _enhanced_powers(name) if p["BuildIndex"] == HASTEN_INDEX)
    buffed = f32(dumped["Base"]["RechargeTime"] / f32(1.0 + aggregate))
    assert buffed == f32(dumped["RechargeTime"])


def test_empty_slots_are_a_no_op(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """Passing enhancement data for an empty-slot build changes nothing.

    Regression guard: the no-slot fixture must produce identical Totals whether
    or not slots/enh_db/tables are supplied — every multiplier collapses to 1.
    """
    name = "shield_scrapper_noslots"
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / name / "slots.json")
    call: dict[str, Any] = {
        "class_name": _totals_json(name)["Class"],
        "mods": mods,
        "classes": classes,
        "enums": enums,
        "config": config,
        "server": server,
    }
    without = compute_base_totals(powers, **call)
    with_data = compute_base_totals(powers, slots=slots, enh_db=enh_db, tables=tables, **call)
    assert without.totals == with_data.totals
    assert without.totals_capped == with_data.totals_capped


class TestEnhanceAspect:
    """``_enhance_aspect`` routes a Buffable effect by eEnhance name-identity + special cases."""

    def test_name_identity_defense(self, make_effect: Any, enums: EnumMaps) -> None:
        names = base_totals._enhance_names(enums)
        assert base_totals._enhance_aspect(make_effect(effect_type="Defense", buffable=True), names) == "Defense"

    def test_name_identity_resistance(self, make_effect: Any, enums: EnumMaps) -> None:
        # Name-identity now routes Resistance (and Recovery/Heal/...), not just Defense.
        names = base_totals._enhance_names(enums)
        assert base_totals._enhance_aspect(make_effect(effect_type="Resistance", buffable=True), names) == "Resistance"

    def test_name_identity_recovery(self, make_effect: Any, enums: EnumMaps) -> None:
        names = base_totals._enhance_names(enums)
        assert base_totals._enhance_aspect(make_effect(effect_type="Recovery", buffable=True), names) == "Recovery"

    def test_enhancement_accuracy_special_case(self, make_effect: Any, enums: EnumMaps) -> None:
        names = base_totals._enhance_names(enums)
        fx = make_effect(effect_type="Enhancement", et_modifies="Accuracy", buffable=True)
        assert base_totals._enhance_aspect(fx, names) == "Accuracy"

    def test_res_effect_defense_remap(self, make_effect: Any, enums: EnumMaps) -> None:
        names = base_totals._enhance_names(enums)
        fx = make_effect(effect_type="ResEffect", et_modifies="Defense", buffable=True)
        assert base_totals._enhance_aspect(fx, names) == "Defense"

    def test_res_effect_regeneration_remaps_to_heal(self, make_effect: Any, enums: EnumMaps) -> None:
        names = base_totals._enhance_names(enums)
        fx = make_effect(effect_type="ResEffect", et_modifies="Regeneration", buffable=True)
        assert base_totals._enhance_aspect(fx, names) == "Heal"

    def test_non_buffable_is_none(self, make_effect: Any, enums: EnumMaps) -> None:
        names = base_totals._enhance_names(enums)
        assert base_totals._enhance_aspect(make_effect(effect_type="Defense", buffable=False), names) is None

    def test_non_enhance_name_is_none(self, make_effect: Any, enums: EnumMaps) -> None:
        # ResEffect is an eEffectType but not an eEnhance name, and ETModifies is neither
        # Defense nor Regeneration -> unrouted.
        names = base_totals._enhance_names(enums)
        fx = make_effect(effect_type="ResEffect", et_modifies="Recovery", buffable=True)
        assert base_totals._enhance_aspect(fx, names) is None
