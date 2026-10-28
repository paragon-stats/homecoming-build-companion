"""End to end: every committed sample ``.mbd`` is read, rewritten, resolved, validated and computed.

One sample build per archetype is committed under ``samples/builds`` (Mids-saved files), and
its Mids dump sits beside the other fixtures in ``fixtures/mids/builds/<file stem>``. Each is
parsed, round-tripped through the writer's inverse, checked against the format invariants,
resolved to powers and slots through ``powers.json``, run through both registries, and its
totals computed from the dump with its slots, set bonuses and incarnates, and held against the
archetype's caps. Every failure is collected and listed; a dropped sample is a failure, not a
smaller pass.
"""

import json
from collections.abc import Callable
from pathlib import Path

import pytest

from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.attribmod import load_attribmod
from coh_engine.base_totals import (
    BaseTotals,
    compute_base_totals,
    load_engine_config,
    load_enum_maps,
    load_server_data,
)
from coh_engine.buildfile.mbd import (
    check_mbd_invariants,
    dump_mbd,
    io_bounds_by_nid,
    load_mbd,
    load_mbd_db,
    parse_mbd,
    powers_from_build,
    slots_from_build,
)
from coh_engine.effect import load_powers_effects
from coh_engine.endurance_budget import MIDS_TOTALS_SCALAR_FIELDS, MIDS_TOTALS_VECTOR_FIELDS
from coh_engine.enhancement import load_build_slots, load_enhancement_effects
from coh_engine.hard_limits import MbdFormat, check_hard_limits
from coh_engine.incarnate import load_incarnates
from coh_engine.legality import check_build_legality, load_enhancement_legality
from coh_engine.levels import load_level_schedule
from coh_engine.maths import f32, load_maths
from coh_engine.set_bonuses import load_set_bonus_db

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"
SAMPLES = Path(__file__).resolve().parents[3] / "samples" / "builds"

# The classes with a committed sample; Defender, Peacebringer and Warshade have only .mxd files to take.
EXPECTED_CLASSES = {
    "Class_Arachnos_Soldier",
    "Class_Arachnos_Widow",
    "Class_Blaster",
    "Class_Brute",
    "Class_Controller",
    "Class_Corruptor",
    "Class_Dominator",
    "Class_Mastermind",
    "Class_Scrapper",
    "Class_Sentinel",
    "Class_Stalker",
    "Class_Tanker",
}
# The samples that are one per archetype; the community captures beside them are the .mxd parity builds.
CURATED_FOLDERS = ("operator-builds", "community/Mids Archives")

DB = load_mbd_db(MIDS)
ARCHETYPES: ArchetypeDb = load_archetypes(MIDS / "archetypes.json")
MODS = load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")
TABLES = load_maths(FIXTURES / "Maths.mhd")
# Speed caps, stealth and threat are left out, as in the other parity tests.
_NOT_COMPARED = {"MaxFlySpd", "MaxJumpSpd", "StealthPvE", "StealthPvP", "ThreatLevel", "MaxRunSpd"}
SCALAR_FIELDS = {k: v for k, v in MIDS_TOTALS_SCALAR_FIELDS.items() if k not in _NOT_COMPARED}


def _curated() -> list[Path]:
    return sorted(p for folder in CURATED_FOLDERS for p in (SAMPLES / folder).rglob("*.mbd"))


def _round_trip_and_invariants(path: Path) -> list[str]:
    build = load_mbd(path)
    doc = dump_mbd(build)
    check_mbd_invariants(doc, DB)
    return [] if parse_mbd(doc) == build else ["the rewritten build differs from the file"]


def _errors_from_the_registries(path: Path) -> list[str]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    build = parse_mbd(raw)
    powers = powers_from_build(build, DB)
    slots = slots_from_build(build, DB)
    fmt = MbdFormat(build.power_sets, raw["Level"], io_bounds_by_nid(DB))
    schedule = load_level_schedule(MIDS / "levels.json")
    server = load_server_data(MIDS / "server_data.json")
    diagnostics = [
        *check_hard_limits(powers, slots, schedule, server.max_slots, format=fmt),
        *check_build_legality(
            powers,
            slots,
            load_enhancement_legality(MIDS / "enhancements.json"),
            load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json"),
            dict(load_enhancement_effects(MIDS / "enhancement_effects.json")),
        ),
    ]
    return [f"{d.rule_id}: {d.message}" for d in diagnostics if d.severity == "error"]


def _slotted_totals(path: Path) -> tuple[BaseTotals, str]:
    """Compute the build's totals from its Mids dump with the slots, set bonuses and incarnates folded in."""
    dump = MIDS / "builds" / path.stem
    class_name = json.loads((dump / "totals.json").read_text(encoding="utf-8"))["Class"]
    result = compute_base_totals(
        load_powers_effects(dump / "powers_effects.json"),
        class_name=class_name,
        mods=MODS,
        classes=ARCHETYPES,
        enums=load_enum_maps(MIDS / "enums.json"),
        config=load_engine_config(MIDS / "config.json"),
        server=load_server_data(MIDS / "server_data.json"),
        slots=load_build_slots(dump / "slots.json"),
        enh_db=dict(load_enhancement_effects(MIDS / "enhancement_effects.json")),
        tables=TABLES,
        set_db=load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json"),
        incarnates=load_incarnates(dump / "incarnates.json"),
    )
    return result, class_name


def _totals_outside_caps(path: Path) -> list[str]:
    """Name every capped total above its archetype cap."""
    result, class_name = _slotted_totals(path)
    at = ARCHETYPES.classes[ARCHETYPES.nid_from_uid_class(class_name)]
    capped = result.totals_capped
    limits = [
        ("BuffDam", capped.buff_dam, at.damage_cap - 1),
        ("BuffHaste", capped.buff_haste, at.recharge_cap - 1),
        ("HPRegen", capped.hp_regen, at.regen_cap - 1),
        ("EndRec", capped.end_rec, at.recovery_cap - 1),
        *((f"Res[{i}]", value, at.res_cap) for i, value in enumerate(capped.res)),
        ("HPMax", capped.hp_max, at.hp_cap),
    ]
    return [f"{label} {value} above the cap {cap}" for label, value, cap in limits if value > cap + 1e-6]


def _totals_differing_from_mids(path: Path) -> list[str]:
    """Name every compared field whose computed total differs from Mids' own, at single precision."""
    result, _ = _slotted_totals(path)
    want = json.loads((MIDS / "builds" / path.stem / "totals.json").read_text(encoding="utf-8"))
    diffs = []
    for side, got in (("Totals", result.totals), ("TotalsCapped", result.totals_capped)):
        for key, attr in MIDS_TOTALS_VECTOR_FIELDS.items():
            ours = getattr(got, attr)
            diffs += [f"{side}.{key}[{i}] {ours[i]} != {v}" for i, v in enumerate(want[side][key]) if f32(v) != ours[i]]
        for key, attr in SCALAR_FIELDS.items():
            if f32(want[side][key]) != getattr(got, attr):
                diffs.append(f"{side}.{key} {getattr(got, attr)} != {want[side][key]}")
    return diffs


def test_one_sample_per_available_archetype_is_committed() -> None:
    classes = {load_mbd(p).class_name for p in _curated()}
    assert classes == EXPECTED_CLASSES, f"missing {EXPECTED_CLASSES - classes}, unexpected {classes - EXPECTED_CLASSES}"
    assert len(_curated()) == len(EXPECTED_CLASSES)


@pytest.mark.parametrize("path", _curated(), ids=lambda p: p.stem)
@pytest.mark.parametrize(
    "check",
    [_round_trip_and_invariants, _errors_from_the_registries, _totals_outside_caps],
    ids=lambda f: f.__name__,
)
def test_every_curated_sample_passes(check: Callable[[Path], list[str]], path: Path) -> None:
    """One case per sample and check, so every failure is listed and none is dropped."""
    assert check(path) == []


def test_unslotted_sample_matches_mids_totals() -> None:
    """The one sample that slots no enhancement matches Mids' totals on every compared field.

    The other eleven slot enhancements whose own effects (Numina's Convalescence, procs) Mids adds
    to the slotted power; the committed enhancement dump omits those effects, so their totals
    cannot match until it carries them.
    """
    (path,) = [p for p in _curated() if p.stem == "Laser - Fallout - Sentinel (Fire Blast - Radiation Armor)"]
    assert _totals_differing_from_mids(path) == []
