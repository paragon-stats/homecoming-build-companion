"""Enhancement placement legality (``Power.IsEnhancementValid`` + ``EnhancementTest``).

The placement verdict is anchored to Mids' own ``IsEnhancementValid`` for the named
golden pairs (``legality_probe.json``): a generic Recharge IO in One with the Shield
and Force Feedback in Soul Drain both reject; a Recharge IO in Hasten accepts. The
build-wide rules (global unique, superior/attuned/ATO/Winter version mutex, stealth
mutex, per-power set duplicate) port ``Build.EnhancementTest`` and are exercised with
real enhancement attributes from the dump.

These protect build *generation* (an optimizer or DTO forcing an illegal slot); Mids
strips class-illegal slots on load, so a Mids-round-tripped build never trips the
placement checks, and mutex/unique are interactive-only in Mids — the port enforces
them ahead of totals.
"""

import json
from pathlib import Path
from typing import Any

import pytest

from coh_engine.diagnostics import Diagnostic
from coh_engine.effect import Power, load_powers_effects
from coh_engine.enhancement import SlotRef, load_build_slots, load_enhancement_effects
from coh_engine.legality import (
    EnhancementLegality,
    check_build_legality,
    is_enhancement_valid,
    load_enhancement_legality,
)
from coh_engine.set_bonuses import SetBonusDb, load_set_bonus_db

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"


@pytest.fixture(scope="module")
def enh_leg() -> dict[int, EnhancementLegality]:
    return dict(load_enhancement_legality(MIDS / "enhancements.json"))


@pytest.fixture(scope="module")
def set_db() -> SetBonusDb:
    return load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json")


def _power(
    build_index: int,
    full_name: str,
    *,
    set_types: tuple[int, ...] = (),
    enhancements: tuple[int, ...] = (),
) -> Power:
    """A Power carrying only the fields legality reads; the rest are inert defaults."""
    return Power(
        build_index=build_index,
        nid_power=build_index,
        full_name=full_name,
        static_index=-1,
        power_type="Click",
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
        set_types=set_types,
        enhancements=enhancements,
    )


def _slot(enh: int, level: int = 1) -> SlotRef:
    return SlotRef(level=level, is_inherent=False, enh=enh, grade=0, io_level=49, relative_level=4)


def _probe() -> list[dict[str, Any]]:
    with open(MIDS / "legality_probe.json", encoding="utf-8") as fh:
        data: list[dict[str, Any]] = json.load(fh)
    return data


def test_load_enhancement_legality_parses_fields(enh_leg: dict[int, EnhancementLegality]) -> None:
    """The loader reads the class/uniqueness fields the legality checks need."""
    recharge = enh_leg[43]  # Crafted_Recharge
    assert recharge.uid == "Crafted_Recharge"
    assert recharge.type_name == "InventO"
    assert recharge.class_ids == (18,)
    assert recharge.nid_set == -1
    assert not recharge.unique


def test_is_enhancement_valid_matches_mids_probe(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Each golden pair's verdict reproduces Mids' IsEnhancementValid exactly."""
    for pair in _probe():
        power = _power(
            0,
            pair["PowerFullName"],
            set_types=tuple(pair["SetTypes"]),
            enhancements=tuple(pair["PowerEnhancements"]),
        )
        enh = enh_leg[pair["EnhNid"]]
        assert is_enhancement_valid(power, enh, set_db) is pair["IsValid"], pair["PowerFullName"]


def test_attuned_set_io_validated_like_its_set_type(
    enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb
) -> None:
    """An Attuned set IO validates by its set type — accepted iff the power lists it."""
    attuned = enh_leg[892]  # Attuned_Scrappers_Strike_A, SetO, set 155 (Scrapper set type)
    set_type = set_db.sets[attuned.nid_set].set_type
    accepts = _power(0, "acc", set_types=(set_type,))
    rejects = _power(1, "rej", set_types=())
    assert is_enhancement_valid(accepts, attuned, set_db) is True
    assert is_enhancement_valid(rejects, attuned, set_db) is False


def test_legal_build_has_no_error_diagnostics(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """A real Mids build (shield_scrapper_set_bonuses) passes with zero errors."""
    powers = load_powers_effects(MIDS / "builds" / "shield_scrapper_set_bonuses" / "powers_effects.json")
    slots = load_build_slots(MIDS / "builds" / "shield_scrapper_set_bonuses" / "slots.json")
    diags = check_build_legality(powers, slots, enh_leg, set_db)
    assert [d for d in diags if d.severity == "error"] == []


def test_recharge_io_in_one_with_the_shield_is_rejected(
    enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb
) -> None:
    """A class-illegal standard IO yields H-ENH-003 (the resolved golden negative)."""
    pair = next(p for p in _probe() if "One_with_the_Shield" in p["PowerFullName"])
    power = _power(0, pair["PowerFullName"], enhancements=tuple(pair["PowerEnhancements"]))
    diags = check_build_legality([power], {0: (_slot(pair["EnhNid"]),)}, enh_leg, set_db)
    assert any(d.rule_id == "H-ENH-003" and d.severity == "error" for d in diags)


def test_force_feedback_in_soul_drain_is_rejected(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """A set-type-illegal set IO yields H-ENH-001 (the second golden negative)."""
    pair = next(p for p in _probe() if "Soul_Drain" in p["PowerFullName"])
    power = _power(0, pair["PowerFullName"], set_types=tuple(pair["SetTypes"]))
    diags = check_build_legality([power], {0: (_slot(pair["EnhNid"]),)}, enh_leg, set_db)
    assert any(d.rule_id == "H-ENH-001" and d.severity == "error" for d in diags)


def test_unknown_enhancement_is_rejected(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """A slotted enhancement absent from the DB yields H-ENH-007 (distinct from unknown-set)."""
    power = _power(0, "p", enhancements=(18,))
    diags = check_build_legality([power], {0: (_slot(9_999_999),)}, enh_leg, set_db)
    assert any(d.rule_id == "H-ENH-007" and d.severity == "error" for d in diags)


def test_global_unique_slotted_twice_is_rejected(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """A unique IO (Kismet: Accuracy) in two powers yields H-ENH-004."""
    kismet = 253  # Crafted_Kismet_E, Unique
    st = set_db.sets[enh_leg[kismet].nid_set].set_type
    p0 = _power(0, "a", set_types=(st,))
    p1 = _power(1, "b", set_types=(st,))
    diags = check_build_legality([p0, p1], {0: (_slot(kismet),), 1: (_slot(kismet),)}, enh_leg, set_db)
    assert any(d.rule_id == "H-ENH-004" and d.severity == "error" for d in diags)


def test_unique_slotted_once_is_legal(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """The same unique IO in a single slot raises no unique violation."""
    kismet = 253
    st = set_db.sets[enh_leg[kismet].nid_set].set_type
    p0 = _power(0, "a", set_types=(st,))
    diags = check_build_legality([p0], {0: (_slot(kismet),)}, enh_leg, set_db)
    assert not any(d.rule_id == "H-ENH-004" for d in diags)


def test_per_power_set_duplicate_is_rejected(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """The same set piece twice in one power yields H-ENH-006."""
    ff = 640  # Crafted_Force_Feedback_A, SetO
    st = set_db.sets[enh_leg[ff].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(ff), _slot(ff))}, enh_leg, set_db)
    assert any(d.rule_id == "H-ENH-006" and d.severity == "error" for d in diags)


def test_stealth_procs_are_mutually_exclusive(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Two different stealth procs (Celerity, Freebird) build-wide yield H-ENH-005."""
    celerity, freebird = 521, 509  # both mutex Stealth
    p0 = _power(0, "a", set_types=(set_db.sets[enh_leg[celerity].nid_set].set_type,))
    p1 = _power(1, "b", set_types=(set_db.sets[enh_leg[freebird].nid_set].set_type,))
    diags = check_build_legality([p0, p1], {0: (_slot(celerity),), 1: (_slot(freebird),)}, enh_leg, set_db)
    assert any(d.rule_id == "H-ENH-005" and d.severity == "error" for d in diags)


def test_superior_and_regular_ato_are_mutually_exclusive(
    enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb
) -> None:
    """The regular and Superior versions of the same ATO piece yield H-ENH-005.

    Attuned_Scrappers_Strike_A and Superior_Attuned_Superior_Scrappers_Strike_A both
    strip to the base 'Scrappers_Strike_A' — Mids' version mutex, which also covers
    Winter-O Superior variants (same SetO typing, same regex).
    """
    regular, superior = 892, 970
    p0 = _power(0, "a", set_types=(set_db.sets[enh_leg[regular].nid_set].set_type,))
    p1 = _power(1, "b", set_types=(set_db.sets[enh_leg[superior].nid_set].set_type,))
    diags = check_build_legality([p0, p1], {0: (_slot(regular),), 1: (_slot(superior),)}, enh_leg, set_db)
    assert any(d.rule_id == "H-ENH-005" and d.severity == "error" for d in diags)


def test_purple_superior_flag_does_not_trigger_version_mutex(
    enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb
) -> None:
    """Two different purples (Hecatomb, Apocalypse) do not mutex despite Superior=True.

    Purples carry Superior=True (the x1.25 value flag) but MutExID=None, so Mids'
    version mutex (gated on MutExID != None, not on the Superior flag) never fires.
    A port keying the mutex on the Superior flag would wrongly reject this legal pair.
    """
    hecatomb, apocalypse = 550, 556  # both Superior=True, mutex None, distinct sets
    p0 = _power(0, "a", set_types=(set_db.sets[enh_leg[hecatomb].nid_set].set_type,))
    p1 = _power(1, "b", set_types=(set_db.sets[enh_leg[apocalypse].nid_set].set_type,))
    diags = check_build_legality([p0, p1], {0: (_slot(hecatomb),), 1: (_slot(apocalypse),)}, enh_leg, set_db)
    assert not any(d.rule_id == "H-ENH-005" for d in diags)


def test_empty_slots_and_no_slots_are_legal(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Empty slots (enh < 0) and powers with no slot list contribute no diagnostics."""
    power = _power(0, "p", enhancements=(18,))
    diags = check_build_legality([power], {0: (_slot(-1),)}, enh_leg, set_db)
    assert diags == []
    assert check_build_legality([power], {}, enh_leg, set_db) == []


def test_set_io_whose_set_is_unknown_is_rejected(set_db: SetBonusDb) -> None:
    """A set IO whose set is absent from the set database yields H-ENH-002."""
    orphan = EnhancementLegality(
        nid=7,
        uid="Orphan_A",
        long_name="Orphan Set: Piece A",
        type_name="SetO",
        class_ids=(),
        nid_set=999_999,  # no such set in the set database
        unique=False,
        mutex_name="None",
        superior=False,
    )
    power = _power(0, "p", set_types=(1, 2, 3))
    diags = check_build_legality([power], {0: (_slot(7),)}, {7: orphan}, set_db)
    assert any(d.rule_id == "H-ENH-002" and d.severity == "error" for d in diags)


def test_valid_standard_io_in_build_has_no_diagnostics(
    enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb
) -> None:
    """A class-legal standard IO (Recharge in Hasten) produces no diagnostics."""
    power = _power(0, "Pool.Speed.Hasten", enhancements=(18,))
    assert check_build_legality([power], {0: (_slot(43),)}, enh_leg, set_db) == []


def test_diagnostics_are_diagnostic_instances(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """check_build_legality yields Diagnostic objects (rendered by diagnostics.py)."""
    power = _power(0, "p", enhancements=())
    diags = check_build_legality([power], {0: (_slot(43),)}, enh_leg, set_db)  # Recharge, class not accepted
    assert diags and all(isinstance(d, Diagnostic) for d in diags)


def test_lone_set_piece_earns_no_bonus(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """One plain piece of a set in a power yields P-SET-001 (warning, not error)."""
    ff = 640  # Crafted_Force_Feedback_A: plain SetO piece, enhances numerically.
    st = set_db.sets[enh_leg[ff].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(ff),)}, enh_leg, set_db)
    assert any(d.rule_id == "P-SET-001" and d.severity == "warning" for d in diags)


def test_two_pieces_of_one_set_in_a_power_is_clean(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Two pieces of the same set in one power reach the tier threshold: no P-SET-001."""
    a, b = 640, 641  # Crafted_Force_Feedback_A / _B
    st = set_db.sets[enh_leg[a].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(a), _slot(b))}, enh_leg, set_db)
    assert not any(d.rule_id == "P-SET-001" for d in diags)


def test_same_set_split_across_powers_still_warns(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Two pieces of one set in DIFFERENT powers do not combine: both warn.

    The failure the rule exists to catch -- a build-wide piece tally looks fine while
    neither power reaches the 2-piece threshold.
    """
    a, b = 640, 641
    st = set_db.sets[enh_leg[a].nid_set].set_type
    p0 = _power(0, "a", set_types=(st,))
    p1 = _power(1, "b", set_types=(st,))
    diags = check_build_legality([p0, p1], {0: (_slot(a),), 1: (_slot(b),)}, enh_leg, set_db)
    assert len([d for d in diags if d.rule_id == "P-SET-001"]) == 2


def _nid(enh_leg: dict[int, EnhancementLegality], uid: str) -> int:
    return next(nid for nid, e in enh_leg.items() if e.uid == uid)


def test_lone_special_piece_is_exempt(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Luck of the Gambler's global is not Unique but carries a SpecialBonus: exempt."""
    lotg = _nid(enh_leg, "Crafted_Luck_of_the_Gambler_F")
    assert not enh_leg[lotg].unique  # the special test, not the unique test, must catch it
    st = set_db.sets[enh_leg[lotg].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(lotg),)}, enh_leg, set_db)
    assert not any(d.rule_id == "P-SET-001" for d in diags)


def test_lone_unique_piece_is_exempt(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Kismet: Accuracy is Unique with no SpecialBonus: the unique test must catch it."""
    kismet = _nid(enh_leg, "Crafted_Kismet_E")
    assert enh_leg[kismet].unique
    st = set_db.sets[enh_leg[kismet].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(kismet),)}, enh_leg, set_db)
    assert not any(d.rule_id == "P-SET-001" for d in diags)


def test_lone_proc_is_exempt_with_effects_db(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """A pure proc is neither unique nor special; zero enhancement effects exempts it."""
    ff_proc = _nid(enh_leg, "Crafted_Force_Feedback_F")
    assert not enh_leg[ff_proc].unique
    effects = load_enhancement_effects(MIDS / "enhancement_effects.json")
    assert not effects[ff_proc].effects  # the signal the exemption relies on
    st = set_db.sets[enh_leg[ff_proc].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(ff_proc),)}, enh_leg, set_db, effects)
    assert not any(d.rule_id == "P-SET-001" for d in diags)


def test_lone_proc_warns_without_effects_db(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """Without the effects dump a pure proc cannot be identified, so it is reported."""
    ff_proc = _nid(enh_leg, "Crafted_Force_Feedback_F")
    st = set_db.sets[enh_leg[ff_proc].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(ff_proc),)}, enh_leg, set_db)
    assert any(d.rule_id == "P-SET-001" for d in diags)


def test_plain_piece_still_warns_with_effects_db(enh_leg: dict[int, EnhancementLegality], set_db: SetBonusDb) -> None:
    """A piece that DOES enhance numerically is not a proc, so the effects dump must not
    exempt it -- the exemption keys on having no enhancement effects, not on merely
    being present in the dump."""
    ff = _nid(enh_leg, "Crafted_Force_Feedback_A")
    effects = load_enhancement_effects(MIDS / "enhancement_effects.json")
    assert effects[ff].effects  # plain piece: has numeric effects
    st = set_db.sets[enh_leg[ff].nid_set].set_type
    power = _power(0, "p", set_types=(st,))
    diags = check_build_legality([power], {0: (_slot(ff),)}, enh_leg, set_db, effects)
    assert any(d.rule_id == "P-SET-001" for d in diags)
