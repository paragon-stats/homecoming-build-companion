"""The ``.mbd`` writer: a build written as the game grants it, one level-by-level walk."""

import json
from dataclasses import replace
from pathlib import Path

import pytest

from coh_engine.__main__ import run_validate
from coh_engine.buildfile.mbd import (
    BuildSpec,
    BuildSpecError,
    PickSpec,
    _fill_slots,
    load_mbd,
    parse_mbd,
    write_mbd,
)
from coh_engine.levels import LevelSchedule, load_level_schedule
from coh_engine.tests.wpss_tanker_spec import SPEC

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"
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
    written = write_mbd(_spec(tuple(reversed(picks))), schedule)
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
        write_mbd(_spec(picks), schedule)
    with pytest.raises(BuildSpecError, match=r"E46: 25 picks; the schedule grants 24"):
        write_mbd(_spec((*_full(), PickSpec("Pool.Speed.Hasten", 50))), schedule)


def test_explicit_slots_before_the_power_or_off_a_grant_level_are_refused(schedule: LevelSchedule) -> None:
    early = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 2, slot_levels=(3,))
    with pytest.raises(
        BuildSpecError, match=r"E47: .*Haymaker.* places a slot at level 3, before it is picked at level 4"
    ):
        write_mbd(_spec(_full(p3=early)), schedule)
    off = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 2, slot_levels=(6,))
    with pytest.raises(BuildSpecError, match=r"E48: .*Haymaker.* places a slot at level 6, which grants no slot"):
        write_mbd(_spec(_full(p3=off)), schedule)
    short = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 3, slot_levels=(5,))
    with pytest.raises(BuildSpecError, match=r"E48: .*Haymaker.* names 1 slot level for 2 added slots"):
        write_mbd(_spec(_full(p3=short)), schedule)


def test_explicit_slots_are_kept_and_the_rest_placed_around_them(schedule: LevelSchedule) -> None:
    fixed = PickSpec("Tanker_Melee.Super_Strength.Haymaker", 4, ("Crafted_Accuracy",) * 3, slot_levels=(50, 50))
    build = parse_mbd(write_mbd(_spec(_full({0: 1}, p3=fixed)), schedule))
    assert [s.level + 1 for s in build.power_entries[3].slot_entries] == [4, 50, 50]
    assert [s.level + 1 for s in build.power_entries[0].slot_entries] == [1, 3]


def test_demand_the_schedule_cannot_place_is_refused(schedule: LevelSchedule) -> None:
    # Level 49 grants no slot and level 50 grants three: four added slots on a level-49 pick cannot fit.
    late = PickSpec("Pool.Speed.Hasten", 49, ("Crafted_Recharge",) * 5)
    with pytest.raises(
        BuildSpecError, match=r"E49: .*Hasten.* picked at level 49 needs 4 added slots; 3 can be placed from level 49"
    ):
        write_mbd(_spec(_full(p23=late)), schedule)
    greedy = dict.fromkeys(range(14), 5)  # 70 added slots against a 67-slot budget
    with pytest.raises(BuildSpecError, match=r"E49: 70 added slots; the schedule grants 67"):
        write_mbd(_spec(_full(greedy)), schedule)
    # Each fits alone (six grants from 47, three from 49), but together they need seven.
    taunt = PickSpec("Tanker_Melee.Super_Strength.Taunt", 47, ("Crafted_Accuracy",) * 5)
    hasten = PickSpec("Pool.Speed.Hasten", 49, ("Crafted_Recharge",) * 4)
    with pytest.raises(BuildSpecError, match=r"E49: the powers picked late need more added slots"):
        write_mbd(_spec(_full(p22=taunt, p23=hasten)), schedule)


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
    build = parse_mbd(write_mbd(_spec(_full(hungry, p0=anchor, p4=toggle)), schedule))
    for entry in (build.power_entries[0], build.power_entries[4]):
        assert sum(1 for s in entry.slot_entries[1:] if s.level + 1 <= 15) >= 2, entry.power_name


TANKER_MBD = FIXTURES / "builds" / "wpss_tanker_leadership.mbd"
TANKER_DUMP = MIDS / "builds" / "wpss_tanker_leadership"
ATTACK_OR_TOGGLE = (
    "Super_Strength.Punch",
    "Super_Strength.Haymaker",
    "Indomitable_Will",
    "Rise_to_the_Challenge",
    "Combat_Jumping",
)


def test_the_writer_reproduces_the_committed_tanker_build(schedule: LevelSchedule) -> None:
    written = json.dumps(write_mbd(SPEC, schedule), indent=2) + "\n"
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


def test_every_early_attack_and_toggle_holds_two_added_slots_by_level_15() -> None:
    build = load_mbd(TANKER_MBD)
    early = [e for e in build.power_entries[:24] if e.level + 1 <= 14 and e.power_name.endswith(ATTACK_OR_TOGGLE)]
    assert len(early) == len(ATTACK_OR_TOGGLE)
    for entry in early:
        added = entry.slot_entries[1:]
        assert sum(1 for s in added if s.level + 1 <= 15) >= min(2, len(added)), entry.power_name


def test_inherents_and_incarnates_follow_the_picks(schedule: LevelSchedule) -> None:
    health = PickSpec(
        "Inherent.Fitness.Health", 1, ("Crafted_Numinas_Convalesence_F", "Crafted_Miracle_F"), stat_include=True
    )
    brawl = PickSpec("Inherent.Inherent.Brawl", 1, (None,))
    gauntlet = PickSpec("Inherent.Inherent.Gauntlet", 1, ())
    spec = replace(_spec(_full(), (brawl, gauntlet, health)), incarnates=("Incarnate.Alpha.Cardiac_Radial_Paragon",))
    build = parse_mbd(write_mbd(spec, schedule))
    names = [e.power_name for e in build.power_entries[24:]]
    assert names == [
        "Inherent.Inherent.Brawl",
        "Inherent.Inherent.Gauntlet",
        "Inherent.Fitness.Health",
        "Incarnate.Alpha.Cardiac_Radial_Paragon",
    ]
    assert build.power_entries[24].slot_entries[0].enhancement is None and build.power_entries[25].slot_entries == ()
    assert len(build.power_entries[26].slot_entries) == 2 and build.power_entries[27].level + 1 == 50
