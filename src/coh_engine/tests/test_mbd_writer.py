"""The ``.mbd`` writer: a build written as the game grants it, one level-by-level walk."""

import json
from dataclasses import replace
from pathlib import Path

import pytest

from coh_engine.__main__ import run_validate
from coh_engine.buildfile.mbd import (
    BuildSpec,
    BuildSpecError,
    EnhInfo,
    PickSpec,
    _fill_slots,
    check_mbd_invariants,
    dump_mbd,
    io_level_bounds,
    level_text_problem,
    load_mbd,
    load_mbd_db,
    missing_inherents,
    parse_mbd,
    write_mbd,
)
from coh_engine.levels import LevelSchedule, load_level_schedule
from coh_engine.tests.wpss_tanker_spec import SPEC

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"
DB = load_mbd_db(MIDS)
PICK_LEVELS = (1, 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 35, 38, 41, 44, 47, 49)


@pytest.fixture(scope="module")
def schedule() -> LevelSchedule:
    return load_level_schedule(MIDS / "levels.json")


def _spec(picks: tuple[PickSpec, ...], inherents: tuple[PickSpec, ...] = ()) -> BuildSpec:
    return BuildSpec(
        class_name="Class_Tanker",
        origin="Science",
        alignment="Hero",
        name="test",
        comment="",
        power_sets=("Tanker_Defense.Willpower", "Tanker_Melee.Super_Strength", "", "", "", "", "", ""),
        picks=picks,
        inherents=inherents,
    )


def _full(added: dict[int, int] | None = None, **overrides: PickSpec) -> tuple[PickSpec, ...]:
    """24 picks on the schedule's own levels; ``added`` maps a pick position to its added slots."""
    added = added or {}
    picks = [
        PickSpec(f"Tanker_Melee.Super_Strength.P{i}", level, ("Crafted_Accuracy",) * (1 + added.get(i, 0)))
        for i, level in enumerate(PICK_LEVELS)
    ]
    for key, pick in overrides.items():
        picks[int(key[1:])] = pick
    return tuple(picks)


def test_picks_are_written_in_schedule_order_whatever_order_the_spec_lists_them(schedule: LevelSchedule) -> None:
    picks = _full({0: 2, 3: 1})
    written = write_mbd(_spec(tuple(reversed(picks))), schedule, DB)
    build = parse_mbd(written)
    assert [e.level + 1 for e in build.power_entries[:24]] == list(PICK_LEVELS)
    # The two level-1 picks keep the order the spec lists them in; every other pick has its own level.
    assert [e.power_name for e in build.power_entries[:24]] == [picks[1].power, picks[0].power] + [
        p.power for p in picks[2:]
    ]
    first, fourth = build.power_entries[1], build.power_entries[3]
    assert [s.level + 1 for s in first.slot_entries] == [1, 3, 3]  # (A) at the pick, then the first grants
    assert [s.level + 1 for s in fourth.slot_entries] == [4, 5]
    assert build.last_power == 24 and build.meta.database_version == "2026.1.1242"


def test_a_pick_off_its_schedule_level_is_refused(schedule: LevelSchedule) -> None:
    picks = _full(p22=PickSpec("Tanker_Melee.Super_Strength.Taunt", 48, ("Crafted_Accuracy",)))
    with pytest.raises(
        BuildSpecError, match=r"E46: pick 23 of 24 .*Taunt.* is at level 48; the schedule grants it at level 47"
    ):
        write_mbd(_spec(picks), schedule, DB)
    with pytest.raises(BuildSpecError, match=r"E46: 25 picks; the schedule grants 24"):
        write_mbd(_spec((*_full(), PickSpec("Pool.Speed.Hasten", 50))), schedule, DB)


def test_explicit_slots_before_the_power_or_off_a_grant_level_are_refused(schedule: LevelSchedule) -> None:
    early = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 2, slot_levels=(3,))
    with pytest.raises(
        BuildSpecError, match=r"E47: .*Haymaker.* places a slot at level 3, before it is picked at level 4"
    ):
        write_mbd(_spec(_full(p3=early)), schedule, DB)
    off = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 2, slot_levels=(6,))
    with pytest.raises(BuildSpecError, match=r"E48: .*Haymaker.* places a slot at level 6, which grants no slot"):
        write_mbd(_spec(_full(p3=off)), schedule, DB)
    short = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 3, slot_levels=(5,))
    with pytest.raises(BuildSpecError, match=r"E48: .*Haymaker.* names 1 slot level for 2 added slots"):
        write_mbd(_spec(_full(p3=short)), schedule, DB)


def test_explicit_slots_are_kept_and_the_rest_placed_around_them(schedule: LevelSchedule) -> None:
    fixed = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 3, slot_levels=(50, 50))
    build = parse_mbd(write_mbd(_spec(_full({0: 1}, p3=fixed)), schedule, DB))
    assert [s.level + 1 for s in build.power_entries[3].slot_entries] == [4, 50, 50]
    assert [s.level + 1 for s in build.power_entries[0].slot_entries] == [1, 3]


def test_demand_the_schedule_cannot_place_is_refused(schedule: LevelSchedule) -> None:
    # Level 49 grants no slot and level 50 grants three: four added slots on a level-49 pick cannot fit.
    late = PickSpec("Pool.Speed.Hasten", 49, ("Crafted_Recharge",) * 5)
    with pytest.raises(
        BuildSpecError, match=r"E49: .*Hasten.* picked at level 49 needs 4 added slots; 3 can be placed from level 49"
    ):
        write_mbd(_spec(_full(p23=late)), schedule, DB)
    greedy = dict.fromkeys(range(14), 5)  # 70 added slots against a 67-slot budget
    with pytest.raises(BuildSpecError, match=r"E49: 70 added slots; the schedule grants 67"):
        write_mbd(_spec(_full(greedy)), schedule, DB)
    # Each fits alone (six grants from 47, three from 49), but together they need seven.
    taunt = PickSpec("Tanker_Melee.Super_Strength.Taunt", 47, ("Crafted_Accuracy",) * 5)
    hasten = PickSpec("Pool.Speed.Hasten", 49, ("Crafted_Recharge",) * 4)
    with pytest.raises(BuildSpecError, match=r"E49: the powers picked late need more added slots"):
        write_mbd(_spec(_full(p22=taunt, p23=hasten)), schedule, DB)


def test_the_fill_never_gives_a_grant_that_strands_a_later_pick() -> None:
    """A grant is withheld when taking it would leave a later pick short (the fill's own guard)."""
    powers = [PickSpec("A", 1, (None, None)), PickSpec("B", 49, (None, None))]
    placed: dict[int, list[int]] = {0: [], 1: []}
    _fill_slots(powers, {0: 1, 1: 1}, [50], placed)
    assert placed == {0: [], 1: []}  # one grant, two needs: neither takes it and strands the other


def test_essential_powers_get_two_added_slots_by_level_15(schedule: LevelSchedule) -> None:
    """Exemplar-10: a defensive toggle and the anchor attack are slotted before the rest."""
    hungry = dict.fromkeys(range(1, 6), 5)  # five early non-essential picks that want every early grant
    toggle = PickSpec("Tanker_Defense.Willpower.Indomitable_Will", 6, ("Crafted_Accuracy",) * 3, essential=True)
    anchor = PickSpec("Tanker_Melee.Super_Strength.Punch", 1, ("Crafted_Accuracy",) * 3, essential=True)
    build = parse_mbd(write_mbd(_spec(_full(hungry, p0=anchor, p4=toggle)), schedule, DB))
    for entry in (build.power_entries[0], build.power_entries[4]):
        assert sum(1 for s in entry.slot_entries[1:] if s.level + 1 <= 15) >= 2, entry.power_name


TANKER_MBD = FIXTURES / "builds" / "wpss_tanker_leadership.mbd"
TANKER_DUMP = MIDS / "builds" / "wpss_tanker_leadership"


def test_the_writer_reproduces_the_committed_tanker_build(schedule: LevelSchedule) -> None:
    written = json.dumps(write_mbd(SPEC, schedule, DB), indent=2) + "\n"
    assert written == TANKER_MBD.read_text(encoding="utf-8")


def test_midsreborn_loads_every_pick_and_slot_where_the_writer_put_them() -> None:
    """The committed dump is MidsReborn's own load of the written file (tools/mids-dump)."""
    build = load_mbd(TANKER_MBD)
    uid_of = {e["Nid"]: e["UID"] for e in json.loads((MIDS / "enhancements.json").read_text(encoding="utf-8"))}
    powers = json.loads((TANKER_DUMP / "powers_effects.json").read_text(encoding="utf-8"))
    slots = {s["BuildIndex"]: s["Slots"] for s in json.loads((TANKER_DUMP / "slots.json").read_text(encoding="utf-8"))}
    loaded = {p["FullName"]: p for p in powers}
    for position, entry in enumerate(build.power_entries):
        mids = loaded[entry.power_name]
        if position < 24:  # a pick's place is its position: Mids must keep it there
            assert mids["BuildIndex"] == position, entry.power_name
            assert mids["PickLevel"] == entry.level, entry.power_name
        mids_slots = [(s["Level"], uid_of.get(s["Enh"])) for s in slots[mids["BuildIndex"]]]
        ours = [(s.level, s.enhancement.uid if s.enhancement else None) for s in entry.slot_entries]
        assert mids_slots == ours, entry.power_name


def test_the_loaded_tanker_build_validates_without_errors() -> None:
    errors = [d for d in run_validate(TANKER_DUMP, MIDS) if d.severity == "error"]
    assert errors == []


def _early_slots(kind: str, effects: set[str], last_level: int) -> dict[str, int]:
    """Added slots placed by ``last_level`` in each early pick Mids loaded as ``kind`` with one of ``effects``."""
    powers = json.loads((TANKER_DUMP / "powers_effects.json").read_text(encoding="utf-8"))
    slots = {s["BuildIndex"]: s["Slots"] for s in json.loads((TANKER_DUMP / "slots.json").read_text(encoding="utf-8"))}
    return {
        p["FullName"]: sum(1 for s in slots[p["BuildIndex"]][1:] if s["Level"] + 1 <= last_level)
        for p in powers[:24]
        if p["PickLevel"] + 1 <= last_level
        and p["PowerType"] == kind
        and effects & {e["EffectType"] for e in p["Effects"]}
    }


def test_every_early_defensive_toggle_holds_two_added_slots_by_level_15() -> None:
    """Exemplar-10: the toggles come from Mids' own load, so a toggle missing from the spec's essentials is caught."""
    toggles = _early_slots("Toggle", {"Defense", "Resistance"}, 15)
    assert len(toggles) >= 3
    assert {name: n for name, n in toggles.items() if n < 2} == {}


def test_two_early_attacks_hold_two_added_slots_by_level_14() -> None:
    attacks = _early_slots("Click", {"Damage"}, 14)
    assert sum(1 for n in attacks.values() if n >= 2) >= 2, attacks


def test_inherents_and_incarnates_follow_the_picks(schedule: LevelSchedule) -> None:
    health = PickSpec(
        "Inherent.Fitness.Health", 1, ("Crafted_Numinas_Convalesence_F", "Crafted_Miracle_F"), stat_include=True
    )
    brawl = PickSpec("Inherent.Inherent.Brawl", 1, (None,))
    gauntlet = PickSpec("Inherent.Inherent.Gauntlet", 1, ())
    spec = replace(_spec(_full(), (brawl, gauntlet, health)), incarnates=("Incarnate.Alpha.Cardiac_Radial_Paragon",))
    build = parse_mbd(write_mbd(spec, schedule, DB))
    names = [e.power_name for e in build.power_entries[24:]]
    # The spec's own inherents first, then the class inherents it omitted, then the incarnates.
    assert names == [
        "Inherent.Inherent.Brawl",
        "Inherent.Inherent.Gauntlet",
        "Inherent.Fitness.Health",
        "Inherent.Inherent.Sprint",
        "Inherent.Inherent.Rest",
        "Inherent.Fitness.Swift",
        "Inherent.Fitness.Hurdle",
        "Inherent.Fitness.Stamina",
        "Incarnate.Alpha.Cardiac_Radial_Paragon",
    ]
    assert build.power_entries[24].slot_entries[0].enhancement is None and build.power_entries[25].slot_entries == ()
    assert len(build.power_entries[26].slot_entries) == 2 and build.power_entries[-1].level + 1 == 50


def test_inherents_the_spec_omits_are_added_as_mids_adds_them(schedule: LevelSchedule) -> None:
    build = parse_mbd(write_mbd(_spec(_full()), schedule, DB))
    added = {e.power_name: e for e in build.power_entries[24:]}
    assert set(added) == {
        "Inherent.Inherent.Brawl",
        "Inherent.Inherent.Gauntlet",
        "Inherent.Inherent.Sprint",
        "Inherent.Inherent.Rest",
        "Inherent.Fitness.Swift",
        "Inherent.Fitness.Health",
        "Inherent.Fitness.Hurdle",
        "Inherent.Fitness.Stamina",
    }
    assert added["Inherent.Inherent.Rest"].level + 1 == 2  # the power's own database level
    assert len(added["Inherent.Inherent.Brawl"].slot_entries) == 1  # takes enhancements: one empty free slot
    assert added["Inherent.Inherent.Gauntlet"].slot_entries == ()  # takes none
    assert added["Inherent.Fitness.Swift"].stat_include and not added["Inherent.Inherent.Brawl"].stat_include


@pytest.mark.parametrize(
    ("uid", "ceiling"),
    [
        ("Crafted_Achilles_Heel_A", 19),
        ("Crafted_Kismet_A", 29),
        ("Crafted_Steadfast_Protection_A", 29),
        ("Crafted_Miracle_A", 39),
        ("Crafted_Touch_of_Death_A", 39),
        ("Crafted_Shield_Wall_A", 49),
        ("Crafted_Panacea_A", 49),
        ("Crafted_Accuracy", 49),
    ],
)
def test_io_level_follows_the_set_ceiling(schedule: LevelSchedule, uid: str, ceiling: int) -> None:
    picks = _full(p0=PickSpec("Tanker_Melee.Super_Strength.P0", 1, (uid,)))
    written = write_mbd(_spec(picks), schedule, DB)
    levels = {
        s["Enhancement"]["IoLevel"]
        for e in written["PowerEntries"]
        for s in e["SlotEntries"]
        if s["Enhancement"] and s["Enhancement"]["Uid"] == uid
    }
    assert levels == {ceiling}


def test_the_character_level_is_written_zero_based(schedule: LevelSchedule) -> None:
    assert write_mbd(_spec(_full()), schedule, DB)["Level"] == "49"
    assert write_mbd(replace(_spec(_full()), level=30), schedule, DB)["Level"] == "29"
    with pytest.raises(BuildSpecError, match=r"E75: .*'50'.*0 to 49"):
        write_mbd(replace(_spec(_full()), level=51), schedule, DB)


def test_seven_powersets_are_refused(schedule: LevelSchedule) -> None:
    spec = replace(_spec(_full()), power_sets=("Tanker_Defense.Willpower",) * 7)
    with pytest.raises(BuildSpecError, match=r"E73: PowerSets has 7 entries; MidsReborn needs 8"):
        write_mbd(spec, schedule, DB)


def test_an_unknown_enhancement_or_inherent_is_refused(schedule: LevelSchedule) -> None:
    picks = _full(p0=PickSpec("Tanker_Melee.Super_Strength.P0", 1, ("Crafted_No_Such_Thing",)))
    with pytest.raises(BuildSpecError, match=r"E78: .*Crafted_No_Such_Thing.*enhancements.json"):
        write_mbd(_spec(picks), schedule, DB)
    odd = replace(DB, powers={k: v for k, v in DB.powers.items() if k != "Inherent.Inherent.Rest"})
    with pytest.raises(BuildSpecError, match=r"E77: .*Inherent.Inherent.Rest.*powers.json"):
        write_mbd(_spec(_full()), schedule, odd)


def _tanker_doc(schedule: LevelSchedule) -> dict[str, object]:
    return write_mbd(_spec(_full()), schedule, DB)


def test_an_io_level_above_its_set_is_refused(schedule: LevelSchedule) -> None:
    doc = _tanker_doc(schedule)
    first = doc["PowerEntries"][0]["SlotEntries"][0]  # type: ignore[index]
    first["Enhancement"] = {**first["Enhancement"], "Uid": "Crafted_Kismet_A", "IoLevel": 50}
    with pytest.raises(
        BuildSpecError, match=r"E74: Tanker_Melee.Super_Strength.P0 slot .*Crafted_Kismet_A.*50.*9 to 29"
    ):
        check_mbd_invariants(doc, DB)
    first["FlippedEnhancement"] = first["Enhancement"]
    first["Enhancement"] = None
    with pytest.raises(BuildSpecError, match=r"E74: "):
        check_mbd_invariants(doc, DB)


def test_a_one_based_level_is_refused(schedule: LevelSchedule) -> None:
    doc = _tanker_doc(schedule)
    for bad in ("50", "-1", "high", 49):
        doc["Level"] = bad
        with pytest.raises(BuildSpecError, match=r"E75: Level "):
            check_mbd_invariants(doc, DB)
    assert level_text_problem("0") is None and level_text_problem("49") is None


def test_a_missing_inherent_is_refused(schedule: LevelSchedule) -> None:
    doc = _tanker_doc(schedule)
    doc["PowerEntries"] = [e for e in doc["PowerEntries"] if e["PowerName"] != "Inherent.Inherent.Rest"]  # type: ignore[attr-defined]
    with pytest.raises(BuildSpecError, match=r"E76: .*missing .*Inherent.Inherent.Rest"):
        check_mbd_invariants(doc, DB)
    names = [e["PowerName"] for e in doc["PowerEntries"]]  # type: ignore[attr-defined]
    assert missing_inherents(names, "Class_Tanker", 50, DB) == ["Inherent.Inherent.Rest"]
    assert "Inherent.Inherent.Special_Set_Bonuses" not in missing_inherents([], "Class_Tanker", 50, DB)
    assert "Inherent.Inherent.Combat_Flight" not in missing_inherents([], "Class_Peacebringer", 5, DB)
    assert "Inherent.Inherent.Combat_Flight" in missing_inherents([], "Class_Peacebringer", 10, DB)
    with pytest.raises(BuildSpecError, match=r"E76: class Class_Nope is not in"):
        missing_inherents([], "Class_Nope", 50, DB)


def test_the_invariants_accept_an_unknown_uid_and_a_null_slot(schedule: LevelSchedule) -> None:
    doc = _tanker_doc(schedule)
    first = doc["PowerEntries"][0]["SlotEntries"][0]  # type: ignore[index]
    first["Enhancement"] = {"Enhancement": "Legacy_Gone", "IoLevel": 50}  # an old uid is judged on load, not here
    check_mbd_invariants(doc, DB)


def test_io_level_bounds_by_enhancement_type() -> None:
    inv = EnhInfo(nid=1, type_name="InventO", set_nid=-1, level_min=9, level_max=49)
    so = EnhInfo(nid=2, type_name="SingleO", set_nid=-1, level_min=0, level_max=52)
    orphan = EnhInfo(nid=3, type_name="SetO", set_nid=-1, level_min=0, level_max=0)
    db = replace(DB, enhancements={"inv": inv, "so": so, "orphan": orphan})
    assert io_level_bounds("inv", db) == (9, 49)
    assert io_level_bounds("so", db) == (1, 1)  # no IO level: Mids keeps 1
    assert io_level_bounds("orphan", db) == (9, 52)  # a set piece with no set: CheckAndFixIOLevel's own defaults
    assert io_level_bounds("missing", db) is None


def test_the_builds_on_disk_round_trip_through_the_model() -> None:
    builds = [*FIXTURES.joinpath("builds").glob("*.mbd"), *(FIXTURES.parents[3] / "samples" / "builds").rglob("*.mbd")]
    assert len(builds) >= 24
    failures = []
    for path in builds:
        build = load_mbd(path)
        try:
            check_mbd_invariants(dump_mbd(build), DB)
            if parse_mbd(dump_mbd(build)) != build:
                failures.append((path.name, "round trip differs"))
        except BuildSpecError as error:
            failures.append((path.name, str(error)))
    assert failures == []


def test_a_placeholder_entry_and_a_null_slot_survive_the_round_trip() -> None:
    doc = {
        "BuiltWith": {"App": "a", "Version": "1", "Database": "Homecoming", "DatabaseVersion": "2"},
        "Level": "49",
        "Class": "Class_Tanker",
        "PowerSets": [],
        "LastPower": 1,
        "PowerEntries": [
            {"PowerName": "", "Level": 7, "SlotEntries": []},
            {
                "PowerName": "A.B.C",
                "Level": 3,
                "SubPowerEntries": [{"PowerName": "A.B.D", "StatInclude": True}],
                "SlotEntries": [{"Level": 3, "IsInherent": True, "Enhancement": None, "FlippedEnhancement": None}],
            },
        ],
    }
    build = parse_mbd(doc)
    assert parse_mbd(dump_mbd(build)) == build
    assert dump_mbd(build)["PowerEntries"][0]["Level"] == 7
