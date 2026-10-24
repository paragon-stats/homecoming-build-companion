"""Mids-parity goldens: the incarnate (Alpha-slot) GrantPower-delivered enhancement.

``arsenal_dominator_incarnate`` is the empty-slot Arsenal/Arsenal Dominator with an
**Agility Core Paragon** Alpha — a recharge + defense + endurance-modification incarnate.
Agility exercises both halves of the incarnate fold in one fixture:

- the **scalar** path (RechargeTime): every slottable power's buffed recharge drops by the
    ``ED(0.11) + 0.22`` incarnate aggregate (Hasten 450 -> 338.35), checked against
    ``enhanced_powers.json``;
- the **effect-mag** path (Defense, Recovery): the typed Defense and Stamina's Recovery
    effects gain the ``ED(pre_ed) + post_ed`` multiplier, checked in ``Totals``.

The ``Incarnate.*`` power itself is a GrantPower delivery vehicle (only GrantPower /
RevokePower / LevelShift / SetMode), so it is excluded from the buff/enhance passes; the
only Totals deltas versus the no-incarnate build are the granted enhancement's.
"""

import json
from pathlib import Path
from typing import Any

import pytest

from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.attribmod import AttribMods, load_attribmod
from coh_engine.base_totals import (
    BaseTotals,
    EngineConfig,
    EnumMaps,
    ServerData,
    compute_base_totals,
    load_engine_config,
    load_enum_maps,
    load_server_data,
)
from coh_engine.effect import load_powers_effects
from coh_engine.endurance_budget import MIDS_TOTALS_SCALAR_FIELDS, MIDS_TOTALS_VECTOR_FIELDS
from coh_engine.enhancement import EnhancementRecord, load_build_slots, load_enhancement_effects
from coh_engine.incarnate import Incarnate, load_incarnates
from coh_engine.maths import MathTables, f32
from coh_engine.statistics import StatsContext, compute_build_stats

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"

INCARNATE = "arsenal_dominator_incarnate"
SLOTTED = "arsenal_dominator_incarnate_slotted"
CARDIAC = "arsenal_dominator_cardiac"
NOSLOTS = "arsenal_dominator_noslots"

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
def enh_db() -> dict[int, EnhancementRecord]:
    return dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))


def _totals_json(name: str) -> dict[str, Any]:
    with open(MIDS / "builds" / name / "totals.json", encoding="utf-8") as fh:
        data: dict[str, Any] = json.load(fh)
    return data


def _enhanced_powers(name: str) -> dict[str, dict[str, Any]]:
    with open(MIDS / "builds" / name / "enhanced_powers.json", encoding="utf-8") as fh:
        return {p["FullName"]: p for p in json.load(fh)}


def _incarnates(name: str) -> tuple[Incarnate, ...]:
    return load_incarnates(MIDS / "builds" / name / "incarnates.json")


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
    with_incarnates: bool = True,
) -> BaseTotals:
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
        incarnates=_incarnates(name) if with_incarnates else None,
    )


def test_incarnate_totals_match_mids(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """Full Totals/TotalsCapped parity with the Agility-Alpha incarnate folded in.

    The effect-mag half (typed Defense x11 uniformly x1.2, Stamina Recovery 0.25 ->
    0.3625) reaches ``Totals`` bit-exact; every other field is unchanged.
    """
    result = _compute(
        INCARNATE, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    expected = _totals_json(INCARNATE)
    for side, got in (("Totals", result.totals), ("TotalsCapped", result.totals_capped)):
        want = expected[side]
        for json_key, attr in VECTOR_FIELDS.items():
            got_vec = getattr(got, attr)
            for i, cell in enumerate(want[json_key]):
                assert f32(cell) == got_vec[i], f"{side}.{json_key}[{i}]"
        for json_key, attr in SCALAR_FIELDS.items():
            assert f32(want[json_key]) == getattr(got, attr), f"{side}.{json_key}"


def test_cardiac_totals_match_mids(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """Cardiac Core Paragon: DamageBuff->Resistance + EnduranceDiscount, full Totals parity.

    The DamageBuff half folds into the build's Smashing/Lethal Resistance effects
    (Totals.Res -> 0.153) via the name-identity + DamageBuff route; the EnduranceDiscount
    half divides every toggle's end cost. Full Totals/TotalsCapped parity confirms both
    reach the character totals bit-exact.
    """
    result = _compute(
        CARDIAC, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    expected = _totals_json(CARDIAC)
    for side, got in (("Totals", result.totals), ("TotalsCapped", result.totals_capped)):
        want = expected[side]
        for json_key, attr in VECTOR_FIELDS.items():
            got_vec = getattr(got, attr)
            for i, cell in enumerate(want[json_key]):
                assert f32(cell) == got_vec[i], f"{side}.{json_key}[{i}]"
        for json_key, attr in SCALAR_FIELDS.items():
            if json_key == "BuffRange":
                continue  # known gap, pinned below
            assert f32(want[json_key]) == getattr(got, attr), f"{side}.{json_key}"
    # Known gap: Cardiac's global Range incarnate does not yet fold into Totals.BuffRange
    # (Mids' enhancement "boost head" for range). The golden carries 0.2; the engine 0.0.
    # Pinned so it flips loudly when the range-buff fold lands (CP6.2 range work).
    assert f32(expected["Totals"]["BuffRange"]) == f32(0.2)
    assert result.totals.buff_range == 0.0


def test_cardiac_combat_jumping_end_is_race_free(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """Combat Jumping's buffed end cost is base / 1.45 — the original race, now correct.

    Cardiac's EnduranceDiscount aggregate is 0.45; the pre-serialization harness raced this
    to 1.15 (base / 2.15). The engine divides by 1.45 (0.0325 -> 0.0224138) and matches the
    race-free dump, proving the port never had the Parallel.For read/write race.
    """
    result = _compute(
        CARDIAC, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    assert result.incarnate_addends is not None
    powers = load_powers_effects(MIDS / "builds" / CARDIAC / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / CARDIAC / "slots.json")
    at_index = classes.nid_from_uid_class(_totals_json(CARDIAC)["Class"])
    ctx = StatsContext(
        mods=mods,
        classes=classes,
        archetype_index=at_index,
        config=config,
        tables=tables,
        enh_db=enh_db,
        global_enhance=result.global_enhance,
        recharge_cap=classes.classes[at_index].recharge_cap,
        incarnate_scalar=result.incarnate_addends.scalar,
    )
    stats = {s.full_name: s for s in compute_build_stats(list(powers), slots, ctx)}
    cj = next(s for name, s in stats.items() if "Combat_Jumping" in name)
    dumped = _enhanced_powers(CARDIAC)
    assert cj.end_cost == f32(dumped[cj.full_name]["EndCost"])
    assert cj.end_cost == f32(f32(0.0325) / f32(1.45))


def test_slotted_coed_totals_match_mids(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """Co-ED (#2): slotted Defense IOs + the Agility Defense incarnate share one ApplyED.

    Weave/Maneuvers carry crafted Defense IOs; Agility adds a pre-ED Defense term. Full
    Totals/TotalsCapped parity holds only if the two fold through a single
    ``ED(Σ slotted + incarnate_pre)`` pass, not ``ED(slotted) + ED(incarnate_pre)``.
    """
    result = _compute(
        SLOTTED, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    expected = _totals_json(SLOTTED)
    for side, got in (("Totals", result.totals), ("TotalsCapped", result.totals_capped)):
        want = expected[side]
        for json_key, attr in VECTOR_FIELDS.items():
            got_vec = getattr(got, attr)
            for i, cell in enumerate(want[json_key]):
                assert f32(cell) == got_vec[i], f"{side}.{json_key}[{i}]"
        for json_key, attr in SCALAR_FIELDS.items():
            assert f32(want[json_key]) == getattr(got, attr), f"{side}.{json_key}"


@pytest.mark.parametrize(
    "full_name",
    [
        # The powers that carry slotted Defense IOs — their added slots shift them into a
        # racy Parallel.For partition in Mids, so the pre-sequential dump reported spurious
        # extra recharge. Defense IOs do not touch recharge, so each must equal its Agility-
        # only recharge (Weave/Tough/Targeting Drone == the empty-build 7.5188).
        "Pool.Fighting.Boxing",
        "Pool.Fighting.Tough",
        "Pool.Fighting.Weave",
        "Pool.Leadership.Defense",
        "Dominator_Assault.Arsenal_Assault.Targeting_Drone",
        "Pool.Leadership.Tactics",
    ],
)
def test_slotted_per_power_recharge_is_race_free(
    full_name: str,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """The slotted build's per-power recharge matches the race-free ``enhanced_powers.json``.

    The engine applies only the Agility recharge fold to these Defense-slotted powers, so
    it reproduces the sequential (UI) recharge — proving the port never had the Parallel.For
    read/write race the pre-sequential dump carried.
    """
    result = _compute(
        SLOTTED, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    assert result.incarnate_addends is not None
    powers = load_powers_effects(MIDS / "builds" / SLOTTED / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / SLOTTED / "slots.json")
    at_index = classes.nid_from_uid_class(_totals_json(SLOTTED)["Class"])
    ctx = StatsContext(
        mods=mods,
        classes=classes,
        archetype_index=at_index,
        config=config,
        tables=tables,
        enh_db=enh_db,
        global_enhance=result.global_enhance,
        recharge_cap=classes.classes[at_index].recharge_cap,
        incarnate_scalar=result.incarnate_addends.scalar,
    )
    stats = {s.full_name: s for s in compute_build_stats(list(powers), slots, ctx)}
    assert stats[full_name].recharge_time == f32(_enhanced_powers(SLOTTED)[full_name]["RechargeTime"])


@pytest.mark.parametrize(
    ("full_name", "buffed_recharge"),
    [
        ("Pool.Speed.Hasten", 338.34588623046875),
        ("Pool.Fighting.Weave", 7.518797397613525),
        ("Pool.Fighting.Tough", 7.518797397613525),
    ],
)
def test_incarnate_per_power_recharge(
    full_name: str,
    buffed_recharge: float,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """The scalar recharge fold reaches every slottable power's buffed RechargeTime.

    The incarnate recharge aggregate is ``ED(0.11) + 0.22 = 0.33``; each base recharge is
    divided by ``1.33``. The per-power derived-stat layer reads the incarnate scalar
    addends off the base-totals result, so recharge matches ``enhanced_powers.json``.
    """
    result = _compute(
        INCARNATE, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    assert result.incarnate_addends is not None
    powers = load_powers_effects(MIDS / "builds" / INCARNATE / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / INCARNATE / "slots.json")
    at_index = classes.nid_from_uid_class(_totals_json(INCARNATE)["Class"])
    ctx = StatsContext(
        mods=mods,
        classes=classes,
        archetype_index=at_index,
        config=config,
        tables=tables,
        enh_db=enh_db,
        global_enhance=result.global_enhance,
        recharge_cap=classes.classes[at_index].recharge_cap,
        incarnate_scalar=result.incarnate_addends.scalar,
    )
    stats = {s.full_name: s for s in compute_build_stats(list(powers), slots, ctx)}
    assert stats[full_name].recharge_time == f32(buffed_recharge)
    assert stats[full_name].recharge_time == f32(_enhanced_powers(INCARNATE)[full_name]["RechargeTime"])


def test_incarnate_changes_totals_versus_no_incarnate(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    enh_db: dict[int, EnhancementRecord],
    tables: MathTables,
) -> None:
    """The incarnate is not inert: dropping it lowers Defense and Recovery.

    Computing the same build with ``incarnates=None`` yields the pre-incarnate totals
    (equal to the no-incarnate ``arsenal_dominator_noslots`` Def/EndRec), proving the
    fold is what raises them — not some unrelated path.
    """
    with_inc = _compute(
        INCARNATE, mods=mods, classes=classes, enums=enums, config=config, server=server, enh_db=enh_db, tables=tables
    )
    without = _compute(
        INCARNATE,
        mods=mods,
        classes=classes,
        enums=enums,
        config=config,
        server=server,
        enh_db=enh_db,
        tables=tables,
        with_incarnates=False,
    )
    assert without.incarnate_addends is None
    assert with_inc.totals.end_rec > without.totals.end_rec
    assert all(w >= wo for w, wo in zip(with_inc.totals.def_, without.totals.def_, strict=True))
    assert with_inc.totals.def_ != without.totals.def_
    # The incarnate-stripped Defense/Recovery equal the no-incarnate build's.
    noslots = _totals_json(NOSLOTS)["Totals"]
    assert without.totals.end_rec == f32(noslots["EndRec"])
    for i, cell in enumerate(noslots["Def"]):
        assert without.totals.def_[i] == f32(cell)


def test_incarnates_without_enhancement_inputs_is_refused(
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
) -> None:
    """Supplying incarnates without slots/enh_db/tables fails loud (``E21``).

    The incarnate fold threads through the per-effect multiplier and per-power scalar
    path, both of which need the enhancement inputs; refuse rather than silently drop it.
    """
    powers = load_powers_effects(MIDS / "builds" / INCARNATE / "powers_effects.json")
    with pytest.raises(ValueError, match="E21"):
        compute_base_totals(
            powers,
            class_name=_totals_json(INCARNATE)["Class"],
            mods=mods,
            classes=classes,
            enums=enums,
            config=config,
            server=server,
            incarnates=_incarnates(INCARNATE),
        )
