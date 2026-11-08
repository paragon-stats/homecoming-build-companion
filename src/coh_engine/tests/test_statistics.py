"""Derived per-power statistics parity (Core/Statistics.cs + the Pass-3 global fold).

The port computes each power's *buffed* recharge / endurance / cast cadence from the
base scalars (``powers_effects.json``), the slotted enhancement, and the global
``_selfEnhance`` terms (``BaseTotals.global_enhance``), then derives end/sec. Every
value is validated against the Mids reference dump (``enhanced_powers.json``): the
buffed scalars directly, and end/sec via the documented Statistics.cs formula. DPS is
deferred (it needs the FXGetDamageValue critical/conditional port), so
``compute_power_dps`` is asserted to fail loud rather than return a wrong number.

Golden: unslotted Hasten's buffed RechargeTime is 315.789 = base 450 / (1 + global
recharge 0.425) — the global BuffHaste folded into a per-power scalar, which CP5's
character Totals never exposed.
"""

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
from coh_engine.effect import Power, load_powers_effects
from coh_engine.enhancement import EnhancementRecord, SlotRef, load_build_slots, load_enhancement_effects
from coh_engine.maths import MathTables, f32, load_maths
from coh_engine.set_bonuses import SetBonusDb, load_set_bonus_db
from coh_engine.statistics import (
    PowerStats,
    StatsContext,
    compute_build_stats,
    compute_power_dps,
    compute_power_stats,
)

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES.parents[3] / "tests" / "fixtures" / "mids"
BUILD = "shield_scrapper_set_bonuses"


@pytest.fixture(scope="module")
def mods() -> AttribMods:
    return load_attribmod(FIXTURES.parents[3] / "tests" / "fixtures" / "attribmod" / "AttribMod.json")


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
    return load_maths(FIXTURES.parents[3] / "tests" / "fixtures" / "Maths.mhd")


@pytest.fixture(scope="module")
def enh_db() -> dict[int, EnhancementRecord]:
    return dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))


@pytest.fixture(scope="module")
def set_db() -> SetBonusDb:
    return load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json")


@pytest.fixture(scope="module")
def powers() -> tuple[Power, ...]:
    return load_powers_effects(MIDS / "builds" / BUILD / "powers_effects.json")


@pytest.fixture(scope="module")
def slots() -> dict[int, tuple[SlotRef, ...]]:
    return dict(load_build_slots(MIDS / "builds" / BUILD / "slots.json"))


@pytest.fixture(scope="module")
def enhanced() -> dict[str, dict[str, Any]]:
    import json

    with open(MIDS / "builds" / BUILD / "enhanced_powers.json", encoding="utf-8") as fh:
        return {p["FullName"]: p for p in json.load(fh)}


@pytest.fixture(scope="module")
def ctx(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
    set_db: SetBonusDb,
    powers: tuple[Power, ...],
    slots: dict[int, tuple[SlotRef, ...]],
) -> StatsContext:
    """A StatsContext carrying the build's globals (from a real compute_base_totals run)."""
    at_index = classes.nid_from_uid_class("Class_Scrapper")
    base = compute_base_totals(
        powers,
        class_name="Class_Scrapper",
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
    return StatsContext(
        mods=mods,
        classes=classes,
        archetype_index=at_index,
        config=config,
        tables=tables,
        enh_db=enh_db,
        global_enhance=base.global_enhance,
        recharge_cap=classes.classes[at_index].recharge_cap,
    )


def _stats_by_name(
    powers: tuple[Power, ...], slots: dict[int, tuple[SlotRef, ...]], ctx: StatsContext
) -> dict[str, PowerStats]:
    return {s.full_name: s for s in compute_build_stats(powers, slots, ctx)}


def test_global_recharge_is_folded_into_every_power(ctx: StatsContext) -> None:
    """The build's global recharge (BuffHaste 0.425) is exposed for the per-power fold."""
    assert ctx.global_enhance.recharge == f32(0.425)


def test_hasten_buffed_recharge_folds_the_global(
    powers: tuple[Power, ...],
    slots: dict[int, tuple[SlotRef, ...]],
    ctx: StatsContext,
    enhanced: dict[str, dict[str, Any]],
) -> None:
    """Unslotted Hasten: base 450 / (1 + 0.425) = 315.789, matching the dump."""
    hasten = next(p for p in powers if p.full_name.endswith(".Hasten"))
    stats = compute_power_stats(hasten, slots.get(hasten.build_index, ()), ctx)
    assert stats.recharge_time == f32(enhanced["Pool.Speed.Hasten"]["RechargeTime"])
    assert stats.recharge_time == f32(315.7895)


def test_every_power_buffed_recharge_matches_mids(
    powers: tuple[Power, ...],
    slots: dict[int, tuple[SlotRef, ...]],
    ctx: StatsContext,
    enhanced: dict[str, dict[str, Any]],
) -> None:
    """Every power's computed buffed recharge reproduces enhanced_powers.json."""
    stats = _stats_by_name(powers, slots, ctx)
    for name, dumped in enhanced.items():
        assert stats[name].recharge_time == f32(dumped["RechargeTime"]), name


def test_every_power_buffed_end_cost_matches_mids(
    powers: tuple[Power, ...],
    slots: dict[int, tuple[SlotRef, ...]],
    ctx: StatsContext,
    enhanced: dict[str, dict[str, Any]],
) -> None:
    """Every power's computed buffed EndCost (EndRdx + global fold) reproduces the dump."""
    stats = _stats_by_name(powers, slots, ctx)
    for name, dumped in enhanced.items():
        assert stats[name].end_cost == f32(dumped["EndCost"]), name


def test_click_end_per_sec_uses_recharge_plus_cast_cadence(
    powers: tuple[Power, ...],
    slots: dict[int, tuple[SlotRef, ...]],
    ctx: StatsContext,
    enhanced: dict[str, dict[str, Any]],
) -> None:
    """A click's end/sec is buffed EndCost / (recharge + cast + interrupt)."""
    smite = next(p for p in powers if p.full_name.endswith(".Smite"))
    stats = compute_power_stats(smite, slots.get(smite.build_index, ()), ctx)
    d = enhanced["Scrapper_Melee.Dark_Melee.Smite"]
    expected = f32(f32(d["EndCost"]) / f32(f32(d["RechargeTime"]) + f32(f32(d["CastTime"]) + f32(d["InterruptTime"]))))
    assert stats.end_per_sec == expected


def test_toggle_end_per_sec_uses_activate_period(
    powers: tuple[Power, ...],
    slots: dict[int, tuple[SlotRef, ...]],
    ctx: StatsContext,
    enhanced: dict[str, dict[str, Any]],
) -> None:
    """A toggle's end/sec is buffed EndCost / ActivatePeriod (its per-tick drain)."""
    weave = next(p for p in powers if p.full_name.endswith(".Weave"))
    stats = compute_power_stats(weave, slots.get(weave.build_index, ()), ctx)
    d = enhanced["Pool.Fighting.Weave"]
    expected = f32(f32(d["EndCost"]) / weave.activate_period)
    assert stats.end_per_sec == expected


def test_one_with_the_shield_recharge_is_not_reduced(
    powers: tuple[Power, ...],
    slots: dict[int, tuple[SlotRef, ...]],
    ctx: StatsContext,
    enhanced: dict[str, dict[str, Any]],
) -> None:
    """A power that ignores RechargeTime enhancement keeps its base recharge (360)."""
    owts = next(p for p in powers if p.full_name.endswith(".One_with_the_Shield"))
    assert "RechargeTime" in owts.ignore_enh  # the gate precondition
    stats = compute_power_stats(owts, slots.get(owts.build_index, ()), ctx)
    assert stats.recharge_time == f32(enhanced["Scrapper_Defense.Shield_Defense.One_with_the_Shield"]["RechargeTime"])
    assert stats.recharge_time == f32(360.0)


def test_dps_is_deferred_and_fails_loud(
    powers: tuple[Power, ...], slots: dict[int, tuple[SlotRef, ...]], ctx: StatsContext
) -> None:
    """compute_power_dps refuses (E15) — faithful damage needs the FXGetDamageValue port."""
    smite = next(p for p in powers if p.full_name.endswith(".Smite"))
    with pytest.raises(NotImplementedError, match="E15"):
        compute_power_dps(smite, slots.get(smite.build_index, ()), ctx)


def test_zero_cadence_power_reports_zero_rates(ctx: StatsContext) -> None:
    """A power with zero recharge/cast/activate reports 0 end/sec (no divide-by-zero)."""
    inert = Power(
        build_index=0,
        nid_power=0,
        full_name="Inert",
        static_index=0,
        power_type="Auto",
        forced_class="",
        click_buff=False,
        level=1,
        end_cost=0.0,
        activate_period=0.0,
        toggle_cost=0.0,
        variable_enabled=False,
        stat_include=True,
        variable_value=0,
        effects=(),
    )
    stats = compute_power_stats(inert, (), ctx)
    assert stats.end_per_sec == 0.0
    assert stats.recharge_time == 0.0
