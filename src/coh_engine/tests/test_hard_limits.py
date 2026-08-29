"""Hard game-limit validation: the registry and each ``H-<DOMAIN>-<NN>`` rule.

Every rule is exercised against a passing and a violating synthetic build over the real
Homecoming level schedule (``levels.json``). The validator is a game-rule checker, so it
is validated against authored builds, not a Mids totals dump.
"""

from collections.abc import Sequence
from pathlib import Path

import pytest

from coh_engine.effect import Power
from coh_engine.enhancement import SlotRef
from coh_engine.hard_limits import (
    build_context,
    check_hard_limits,
    registered_hard_limit_rules,
)
from coh_engine.levels import LevelSchedule, load_level_schedule

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"

MAX_SLOTS = 67


@pytest.fixture(scope="module")
def schedule() -> LevelSchedule:
    return load_level_schedule(MIDS / "levels.json")


def _power(
    build_index: int,
    full_name: str,
    *,
    level: int = 1,
    pick_level: int = 0,
    requires_powers: tuple[tuple[str, ...], ...] = (),
    requires_powers_not: tuple[tuple[str, ...], ...] = (),
    chosen: bool = True,
) -> Power:
    """A minimal build power (only the fields the hard-limits rules read)."""
    return Power(
        build_index=build_index,
        nid_power=build_index,
        full_name=full_name,
        static_index=build_index,
        power_type="Click",
        forced_class="",
        click_buff=False,
        level=level,
        end_cost=0.0,
        activate_period=0.0,
        toggle_cost=0.0,
        variable_enabled=False,
        stat_include=True,
        variable_value=0,
        effects=(),
        pick_level=pick_level,
        requires_powers=requires_powers,
        requires_powers_not=requires_powers_not,
        chosen=chosen,
    )


def _slot(level: int, *, inherent: bool = False, enh: int = 1) -> SlotRef:
    return SlotRef(level=level, is_inherent=inherent, enh=enh, grade=0, io_level=49, relative_level=4)


def _rule_ids(diags: Sequence) -> set[str]:  # type: ignore[type-arg]
    return {d.rule_id for d in diags}


def test_registry_lists_every_rule(schedule: LevelSchedule) -> None:
    """The registry introspects to the registered rule function names."""
    names = registered_hard_limit_rules()
    assert "_rule_total_added_slots" in names
    assert "_rule_pool_count" in names
    assert len(names) == len(set(names))  # no duplicate registration


def test_clean_build_has_no_violations(schedule: LevelSchedule) -> None:
    """A small legal build produces no diagnostics."""
    powers = [
        _power(0, "Scrapper_Melee.Dark_Melee.Smite", level=1, pick_level=0),
        _power(1, "Pool.Speed.Hasten", level=4, pick_level=3),
    ]
    slots = {0: (_slot(0, inherent=True), _slot(2), _slot(4)), 1: (_slot(3, inherent=True),)}
    assert check_hard_limits(powers, slots, schedule, MAX_SLOTS) == []


def test_total_added_slots_over_cap_errors(schedule: LevelSchedule) -> None:
    """H-SLOT-001: more than 67 added slots across the build is an error."""
    # 14 powers x 5 added = 70 added slots (> 67).
    powers = [_power(i, f"Scrapper_Melee.Dark_Melee.P{i}", pick_level=0) for i in range(14)]
    grant = sorted(schedule.slot_grant_levels)[:5]
    slots = {i: (_slot(0, inherent=True), *(_slot(g) for g in grant)) for i in range(14)}
    diags = check_hard_limits(powers, slots, schedule, MAX_SLOTS)
    assert "H-SLOT-001" in _rule_ids(diags)


def test_per_power_over_five_added_slots_errors(schedule: LevelSchedule) -> None:
    """H-SLOT-002: a sixth added slot on one power is an error."""
    powers = [_power(0, "Scrapper_Melee.Dark_Melee.Smite", pick_level=0)]
    grant = sorted(schedule.slot_grant_levels)[:6]  # 6 added slots
    slots = {0: (_slot(0, inherent=True), *(_slot(g) for g in grant))}
    diags = check_hard_limits(powers, slots, schedule, MAX_SLOTS)
    assert "H-SLOT-002" in _rule_ids(diags)


def test_five_added_slots_is_legal(schedule: LevelSchedule) -> None:
    """The inherent slot plus five added slots (six total) is the legal maximum."""
    powers = [_power(0, "Scrapper_Melee.Dark_Melee.Smite", pick_level=0)]
    grant = sorted(schedule.slot_grant_levels)[:5]
    slots = {0: (_slot(0, inherent=True), *(_slot(g) for g in grant))}
    assert check_hard_limits(powers, slots, schedule, MAX_SLOTS) == []


def test_slot_before_pick_level_errors(schedule: LevelSchedule) -> None:
    """H-SLOT-003: a slot placed before the power was picked is an error."""
    powers = [_power(0, "Scrapper_Melee.Dark_Melee.Shadow_Maul", level=2, pick_level=14)]
    # A grant level (2) below the power's pick level (14).
    slots = {0: (_slot(14, inherent=True), _slot(2))}
    diags = check_hard_limits(powers, slots, schedule, MAX_SLOTS)
    assert "H-SLOT-003" in _rule_ids(diags)


def test_slot_off_a_grant_level_errors(schedule: LevelSchedule) -> None:
    """H-SLOT-004: a slot at a level that grants no slot is an error."""
    powers = [_power(0, "Scrapper_Melee.Dark_Melee.Smite", pick_level=0)]
    # Level index 1 (game level 2) grants a power, not a slot.
    slots = {0: (_slot(0, inherent=True), _slot(1))}
    diags = check_hard_limits(powers, slots, schedule, MAX_SLOTS)
    assert "H-SLOT-004" in _rule_ids(diags)


def test_slots_placed_before_earned_errors(schedule: LevelSchedule) -> None:
    """H-SLOT-005: placing more slots by a level than have been granted is an error."""
    # Level index 2 grants 2 slots; place 3 there (one per power) with none earned earlier.
    powers = [_power(i, f"Scrapper_Melee.Dark_Melee.P{i}", pick_level=0) for i in range(3)]
    slots = {i: (_slot(0), _slot(2)) for i in range(3)}
    diags = check_hard_limits(powers, slots, schedule, MAX_SLOTS)
    assert "H-SLOT-005" in _rule_ids(diags)


def test_deferred_slotting_is_legal(schedule: LevelSchedule) -> None:
    """Under-placing early then over-placing later (deferred slotting) is legal — no H-SLOT-005.

    Level index 2 grants 2 but takes 1 (surplus +1); level index 4 grants 2 but takes 3,
    drawing the surplus. Cumulatively 4 placed ≤ 4 granted.
    """
    powers = [_power(0, "Scrapper_Melee.Dark_Melee.Smite", pick_level=0)]
    # (A) at 0, then 1 added at level 2 and 3 added at level 4 (4 added, cumulative-legal).
    slots = {0: (_slot(0), _slot(2), _slot(4), _slot(4), _slot(4))}
    diags = check_hard_limits(powers, slots, schedule, MAX_SLOTS)
    assert "H-SLOT-005" not in _rule_ids(diags)


def test_twentyfive_picks_errors(schedule: LevelSchedule) -> None:
    """H-POWER-001: more than 24 real picks is an error."""
    powers = [_power(i, f"Scrapper_Melee.Dark_Melee.P{i}", pick_level=0) for i in range(25)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-001" in _rule_ids(diags)


def test_inherents_do_not_count_toward_picks(schedule: LevelSchedule) -> None:
    """Inherent powers are auto-granted and excluded from the 24-pick count."""
    powers = [_power(i, f"Scrapper_Melee.Dark_Melee.P{i}", pick_level=0) for i in range(24)]
    powers += [_power(24, "Inherent.Fitness.Stamina", pick_level=0)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-001" not in _rule_ids(diags)


def test_incarnates_do_not_count_toward_picks_or_grant_levels(schedule: LevelSchedule) -> None:
    """An Incarnate slot power is a separate crafting system — not a 24-pick, not a grant level.

    A full 24-pick build plus an equipped Alpha (picked at level 50, which is not a
    power-pick level) must raise neither H-POWER-001 (>24 picks) nor H-POWER-003 (pick at a
    non-grant level): the incarnate is excluded from the pick set entirely, like an inherent.
    """
    powers = [_power(i, f"Dominator_Control.Arsenal_Control.P{i}", pick_level=0) for i in range(24)]
    powers += [_power(24, "Incarnate.Alpha.Agility_Core_Paragon", level=50, pick_level=49)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-001" not in _rule_ids(diags)
    assert "H-POWER-003" not in _rule_ids(diags)


def test_auto_granted_subpower_does_not_count_toward_picks(schedule: LevelSchedule) -> None:
    """An auto-granted sub-power (``chosen=false``) is not one of the 24 picks.

    Mystic Flight grants Translocation, and Mids adds it to the build as an unchosen
    ``PowerEntry`` in the same ``Pool`` group as a real pick. Counting it would report
    H-POWER-001 on a legal 24-pick Sorcery build.
    """
    powers = [_power(i, f"Blaster_Ranged.Electrical_Blast.P{i}", pick_level=0) for i in range(24)]
    powers += [_power(24, "Pool.Sorcery.Translocation", pick_level=5, chosen=False)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-001" not in _rule_ids(diags)


def test_power_picked_before_available_errors(schedule: LevelSchedule) -> None:
    """H-POWER-002: picking a power below its DB minimum level is an error."""
    # A tier-9 power (DB level 32) picked at level 20 (pick index 19).
    powers = [_power(0, "Scrapper_Defense.Shield_Defense.One_with_the_Shield", level=32, pick_level=19)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-002" in _rule_ids(diags)


def test_ancillary_at_level_35_is_available(schedule: LevelSchedule) -> None:
    """A DB-level-35 ancillary power picked at level 35 (index 34) is available."""
    powers = [_power(0, "Epic.Stalker_Soul_Mastery.Moonbeam", level=35, pick_level=34)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-002" not in _rule_ids(diags)


def test_pick_at_non_pick_level_errors(schedule: LevelSchedule) -> None:
    """H-POWER-003: a power picked at a level that grants no power is an error."""
    # Level index 2 (game level 3) grants a slot, not a power.
    powers = [_power(0, "Scrapper_Melee.Dark_Melee.Smite", level=1, pick_level=2)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-003" in _rule_ids(diags)


def test_five_pools_errors(schedule: LevelSchedule) -> None:
    """H-POOL-001: drawing from five power pools is an error."""
    pools = ["Speed", "Leaping", "Fighting", "Leadership", "Flight"]
    powers = [_power(i, f"Pool.{name}.T1", level=1, pick_level=0) for i, name in enumerate(pools)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POOL-001" in _rule_ids(diags)


def test_four_pools_is_legal(schedule: LevelSchedule) -> None:
    """Four power pools is the legal maximum; ancillary (Epic) is not a pool."""
    pools = ["Speed", "Leaping", "Fighting", "Leadership"]
    powers = [_power(i, f"Pool.{name}.T1", level=1, pick_level=0) for i, name in enumerate(pools)]
    powers += [_power(9, "Epic.Stalker_Soul_Mastery.Moonbeam", level=35, pick_level=34)]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POOL-001" not in _rule_ids(diags)


def test_missing_prerequisite_errors(schedule: LevelSchedule) -> None:
    """H-POWER-004: a power whose required prerequisite is absent is an error."""
    # An ancillary T2 requiring its T1 ("Epic...Gloom"), which is not in the build.
    powers = [
        _power(
            0,
            "Epic.Stalker_Soul_Mastery.Dark_Blast",
            level=41,
            pick_level=40,
            requires_powers=(("Epic.Stalker_Soul_Mastery.Gloom",),),
        )
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-004" in _rule_ids(diags)


def test_satisfied_prerequisite_passes(schedule: LevelSchedule) -> None:
    """A prerequisite picked at an earlier level satisfies the requirement."""
    powers = [
        _power(0, "Epic.Stalker_Soul_Mastery.Gloom", level=35, pick_level=34),
        _power(
            1,
            "Epic.Stalker_Soul_Mastery.Dark_Blast",
            level=41,
            pick_level=40,
            requires_powers=(("Epic.Stalker_Soul_Mastery.Gloom",),),
        ),
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-004" not in _rule_ids(diags)


def test_prerequisite_taken_later_does_not_satisfy(schedule: LevelSchedule) -> None:
    """A prerequisite present but picked *after* the candidate does not satisfy it."""
    powers = [
        _power(
            0,
            "Epic.Stalker_Soul_Mastery.Dark_Blast",
            level=41,
            pick_level=40,
            requires_powers=(("Epic.Stalker_Soul_Mastery.Gloom",),),
        ),
        _power(1, "Epic.Stalker_Soul_Mastery.Gloom", level=35, pick_level=43),  # picked later
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-004" in _rule_ids(diags)


def test_or_of_and_pairs_two_of_three(schedule: LevelSchedule) -> None:
    """ "Take 2 of the first 3" — an OR of AND-pairs — is satisfied by any pair present."""
    a, b, c = ("Pool.Leadership.Maneuvers", "Pool.Leadership.Assault", "Pool.Leadership.Tactics")
    # T4 requires two of {a,b,c}: (a&b) | (a&c) | (b&c). Build has a and c.
    powers = [
        _power(0, a, level=4, pick_level=3),
        _power(2, c, level=4, pick_level=7),
        _power(
            3,
            "Pool.Leadership.Vengeance",
            level=14,
            pick_level=13,
            requires_powers=((a, b), (a, c), (b, c)),
        ),
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-004" not in _rule_ids(diags)


def test_forbidden_prerequisite_present_errors(schedule: LevelSchedule) -> None:
    """H-POWER-005: a power whose forbidden (mutex) power is also in the build is an error."""
    powers = [
        _power(0, "Pool.Speed.Whirlwind", level=14, pick_level=13),
        _power(
            1,
            "Pool.Speed.Burnout",
            level=14,
            pick_level=15,
            requires_powers_not=(("Pool.Speed.Whirlwind",),),
        ),
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-005" in _rule_ids(diags)


def test_forbidden_prerequisite_absent_is_legal(schedule: LevelSchedule) -> None:
    """A forbidden (mutex) power that is not in the build raises no conflict."""
    powers = [
        _power(
            0,
            "Pool.Speed.Burnout",
            level=14,
            pick_level=13,
            requires_powers_not=(("Pool.Speed.Whirlwind",),),  # not present in the build
        )
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POWER-005" not in _rule_ids(diags)


def test_two_ancillary_pools_errors(schedule: LevelSchedule) -> None:
    """H-POOL-002: drawing from two Ancillary/Patron (Epic) pools is an error."""
    powers = [
        _power(0, "Epic.Stalker_Soul_Mastery.Moonbeam", level=35, pick_level=34),
        _power(1, "Epic.Mu_Mastery.Mu_Lightning", level=35, pick_level=37),
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POOL-002" in _rule_ids(diags)


def test_single_ancillary_pool_is_legal(schedule: LevelSchedule) -> None:
    """Two powers from one Ancillary pool is legal (only distinct pools count)."""
    powers = [
        _power(0, "Epic.Stalker_Soul_Mastery.Moonbeam", level=35, pick_level=34),
        _power(1, "Epic.Stalker_Soul_Mastery.Shadow_Meld", level=38, pick_level=37),
    ]
    diags = check_hard_limits(powers, {}, schedule, MAX_SLOTS)
    assert "H-POOL-002" not in _rule_ids(diags)


def test_virtual_power_excluded_from_picks(schedule: LevelSchedule) -> None:
    """A build-index < 0 power (the set-bonus virtual power) is not a real pick."""
    powers = [_power(-1, "", pick_level=0), _power(0, "Scrapper_Melee.Dark_Melee.Smite", pick_level=0)]
    ctx = build_context(powers, {}, schedule, MAX_SLOTS)
    assert len(ctx.picks) == 1
    assert ctx.picks[0].full_name == "Scrapper_Melee.Dark_Melee.Smite"
