"""Unit tests for the set-bonus subsystem (:mod:`coh_engine.set_bonuses`).

The parity suite (``test_set_bonus_parity.py``) validates the assembled virtual
power against Mids on a real build. These tests drive the tally / BuildEffects /
Rule-of-Five / legality logic directly with synthetic sets, covering the edges a
single fixture cannot reach: PvMode filtering, the exemplar gate, MyPet skip,
missing bonus powers, and the fail-loud slotting guard.
"""

from collections.abc import Sequence
from pathlib import Path

import pytest

from coh_engine.effect import Effect, Power
from coh_engine.enhancement import EnhancementRecord, SlotRef
from coh_engine.set_bonuses import (
    PVX_ANY,
    PVX_PVP,
    BonusItem,
    EnhancementSetDef,
    SetBonusDb,
    SetBonusPower,
    build_set_bonus_power,
    load_set_bonus_powers,
    power_accepts_set,
    validate_set_slotting,
)

_DEFENSE_SET_TYPE = 7


def _effect(**over: object) -> Effect:
    base: dict[str, object] = {
        "index": 0,
        "effect_type": "Defense",
        "damage_type": "Smashing",
        "mez_type": "None",
        "et_modifies": "None",
        "scale": 1.0,
        "n_magnitude": 1.0,
        "n_duration": 0.0,
        "attrib_type": "Magnitude",
        "aspect": "Cur",
        "modifier_table": "Ones",
        "n_modifier_table": 0,
        "to_who": "Self",
        "pv_mode": "Any",
        "stacking": "No",
        "suppression": 0,
        "buffable": True,
        "resistible": False,
        "ignore_ed": False,
        "ignore_scaling": False,
        "variable_modified": False,
        "base_probability": 1.0,
        "probability": 1.0,
        "procs_per_minute": 0.0,
        "ticks": 0,
        "delayed_time": 0.0,
        "effect_class": "Primary",
        "special_case": "None",
        "n_id_class_name": -1,
        "absorbed_effect": False,
        "absorbed_power_type": "Auto_",
        "absorbed_class_n_id": -1,
        "active_conditionals_count": 0,
        "expr_magnitude": "",
        "expr_duration": "",
        "expr_probability": "",
        "display_percentage": False,
    }
    base.update(over)
    return Effect(**base)  # type: ignore[arg-type]


def _power(
    build_index: int,
    *,
    level: int = 1,
    pick_level: int | None = None,
    set_types: tuple[int, ...] = (_DEFENSE_SET_TYPE,),
) -> Power:
    return Power(
        build_index=build_index,
        nid_power=build_index,
        full_name=f"Test.Set.Power{build_index}",
        static_index=build_index,
        power_type="Toggle",
        forced_class="",
        click_buff=False,
        level=level,
        # The tally gates on pick_level; default it to level so existing gate tests
        # (a power picked above ForceLevel) keep their intent.
        pick_level=level if pick_level is None else pick_level,
        end_cost=0.0,
        activate_period=0.0,
        toggle_cost=0.0,
        variable_enabled=False,
        stat_include=True,
        variable_value=0,
        set_types=set_types,
        effects=(),
    )


def _seto(nid: int, nid_set: int) -> EnhancementRecord:
    return EnhancementRecord(nid=nid, static_index=nid, type_name="SetO", superior=False, effects=(), nid_set=nid_set)


def _slot(enh: int) -> SlotRef:
    return SlotRef(level=1, is_inherent=False, enh=enh, grade=0, io_level=49, relative_level=4)


# A set (nID 1) with a 2-piece tier (bonus power 100), a PvP-only 2-piece tier
# (power 101), and a per-enhancement special on its first member (power 102).
_SET = EnhancementSetDef(
    nid=1,
    uid="Test_Set",
    set_type=_DEFENSE_SET_TYPE,
    set_type_name="Defense",
    enhancements=(10, 11, 12),
    bonus=(
        BonusItem(slotted=2, pv_mode=PVX_ANY, index=(100,)),
        BonusItem(slotted=2, pv_mode=PVX_PVP, index=(101,)),
    ),
    special_bonus=(
        BonusItem(slotted=1, pv_mode=PVX_ANY, index=(102,)),
        BonusItem(slotted=1, pv_mode=PVX_ANY, index=()),
    ),
)


def _bonus_power(pid: int, *, my_pet: bool = False) -> SetBonusPower:
    return SetBonusPower(
        power_id=pid,
        full_name=f"Set_Bonus.Set_Bonus.Bonus_{pid}",
        power_type="Auto_",
        my_pet_only=my_pet,
        effects=(_effect(effect_type="Resistance", et_modifies="None", damage_type="Fire", scale=0.05),),
    )


def _db(powers: Sequence[SetBonusPower], sets: Sequence[EnhancementSetDef] = (_SET,)) -> SetBonusDb:
    return SetBonusDb(sets={s.nid: s for s in sets}, powers={p.power_id: p for p in powers})


class TestPowerAcceptsSet:
    def test_accepts_when_set_type_listed(self) -> None:
        assert power_accepts_set(_power(0, set_types=(7,)), _SET) is True

    def test_rejects_when_set_type_absent(self) -> None:
        assert power_accepts_set(_power(0, set_types=()), _SET) is False


class TestValidateSetSlotting:
    def test_valid_slotting_passes(self) -> None:
        db = _db([_bonus_power(100), _bonus_power(102)])
        validate_set_slotting([_power(0)], {0: (_slot(10),)}, {10: _seto(10, 1)}, db)

    def test_illegal_set_in_power_raises(self) -> None:
        db = _db([_bonus_power(100)])
        with pytest.raises(ValueError, match="H-ENH-001"):
            validate_set_slotting([_power(0, set_types=())], {0: (_slot(10),)}, {10: _seto(10, 1)}, db)

    def test_unknown_set_raises(self) -> None:
        db = _db([_bonus_power(100)])
        with pytest.raises(ValueError, match="H-ENH-002"):
            validate_set_slotting([_power(0)], {0: (_slot(10),)}, {10: _seto(10, 99)}, db)

    def test_empty_and_non_set_slots_skipped(self) -> None:
        db = _db([_bonus_power(100)])
        enh_db = {10: _seto(10, 1), 20: EnhancementRecord(20, 20, "InventO", False, (), -1)}
        # empty slot (-1) and a generic InventO slot both skip the set-type check.
        validate_set_slotting([_power(0)], {0: (_slot(-1), _slot(20))}, enh_db, db)


class TestBuildSetBonusPower:
    def test_tier_and_special_activate(self) -> None:
        db = _db([_bonus_power(100), _bonus_power(102)])
        enh_db = {10: _seto(10, 1), 11: _seto(11, 1)}
        virtual = build_set_bonus_power(
            [_power(0)], {0: (_slot(10), _slot(11))}, enh_db, db, force_level=50, disable_pve=False
        )
        # 2 pieces -> tier 100 fires; member 10 slotted -> special 102 fires. Each
        # bonus power contributes its one Resistance effect.
        assert len(virtual.effects) == 2

    def test_single_piece_skips_tier_keeps_special(self) -> None:
        db = _db([_bonus_power(100), _bonus_power(102)])
        virtual = build_set_bonus_power(
            [_power(0)], {0: (_slot(10),)}, {10: _seto(10, 1)}, db, force_level=50, disable_pve=False
        )
        assert len(virtual.effects) == 1  # only the >=1-piece special (102).

    def test_pvp_tier_gated_out_in_pve(self) -> None:
        db = _db([_bonus_power(100), _bonus_power(101)])
        enh_db = {12: _seto(12, 1), 13: _seto(13, 1)}
        # member 12/13 are not set member 10, so no special; only tiers can fire.
        virtual = build_set_bonus_power(
            [_power(0)], {0: (_slot(12), _slot(13))}, enh_db, db, force_level=50, disable_pve=False
        )
        assert len(virtual.effects) == 1  # PvE tier 100 only; PvP tier 101 filtered.

    def test_pvp_mode_includes_pvp_tier(self) -> None:
        db = _db([_bonus_power(100), _bonus_power(101)])
        enh_db = {12: _seto(12, 1), 13: _seto(13, 1)}
        virtual = build_set_bonus_power(
            [_power(0)], {0: (_slot(12), _slot(13))}, enh_db, db, force_level=50, disable_pve=True
        )
        assert len(virtual.effects) == 2  # Any tier 100 + PvP tier 101.

    def test_exemplar_gate_excludes_high_pick_level(self) -> None:
        db = _db([_bonus_power(100), _bonus_power(102)])
        enh_db = {10: _seto(10, 1), 11: _seto(11, 1)}
        virtual = build_set_bonus_power(
            [_power(0, level=51)], {0: (_slot(10), _slot(11))}, enh_db, db, force_level=50, disable_pve=False
        )
        assert virtual.effects == ()  # power picked above ForceLevel contributes nothing.

    def test_non_set_io_does_not_tally(self) -> None:
        db = _db([_bonus_power(100)])
        enh_db = {20: EnhancementRecord(20, 20, "InventO", False, (), -1)}
        virtual = build_set_bonus_power(
            [_power(0)], {0: (_slot(20), _slot(-1))}, enh_db, db, force_level=50, disable_pve=False
        )
        assert virtual.effects == ()

    def test_rule_of_five_caps_at_five(self) -> None:
        # Six powers each slotting the same set's member 10 -> six copies of the
        # special bonus 102; the counter folds five and drops the sixth.
        db = _db([_bonus_power(102)])
        enh_db = {10: _seto(10, 1)}
        powers = [_power(i) for i in range(6)]
        slots = {i: (_slot(10),) for i in range(6)}
        virtual = build_set_bonus_power(powers, slots, enh_db, db, force_level=50, disable_pve=False)
        assert len(virtual.effects) == 5

    def test_my_pet_bonus_skipped_but_counts(self) -> None:
        # Bonus 102 is MyPet-only: its effects are skipped, yet it still consumes a
        # Rule-of-Five slot. Two sources -> both skipped -> no effects.
        db = _db([_bonus_power(102, my_pet=True)])
        enh_db = {10: _seto(10, 1)}
        virtual = build_set_bonus_power(
            [_power(0), _power(1)], {0: (_slot(10),), 1: (_slot(10),)}, enh_db, db, force_level=50, disable_pve=False
        )
        assert virtual.effects == ()

    def test_missing_bonus_power_counts_without_effects(self) -> None:
        # The special references power 102, absent from the db (models
        # Database.Power[id] == null): it counts but contributes no effects.
        db = _db([])
        virtual = build_set_bonus_power(
            [_power(0)], {0: (_slot(10),)}, {10: _seto(10, 1)}, db, force_level=50, disable_pve=False
        )
        assert virtual.effects == ()

    def test_negative_bonus_index_skipped(self) -> None:
        sset = EnhancementSetDef(
            nid=1,
            uid="Neg",
            set_type=_DEFENSE_SET_TYPE,
            set_type_name="Defense",
            enhancements=(10,),
            bonus=(BonusItem(slotted=2, pv_mode=PVX_ANY, index=(-1,)),),
            special_bonus=(BonusItem(slotted=1, pv_mode=PVX_ANY, index=(-1,)),),
        )
        db = SetBonusDb(sets={1: sset}, powers={})
        enh_db = {10: _seto(10, 1), 11: _seto(11, 1)}
        virtual = build_set_bonus_power(
            [_power(0)], {0: (_slot(10), _slot(11))}, enh_db, db, force_level=50, disable_pve=False
        )
        assert virtual.effects == ()


def test_slow_set_bonus_effects_are_debuffs() -> None:
    """A set-bonus power named for Slow carries debuff-mode effects; every other bonus is normal."""
    powers = load_set_bonus_powers(Path(__file__).parent / "fixtures" / "mids" / "set_bonus_powers.json")
    modes = {p.full_name: {fx.buff_mode for fx in p.effects} for p in powers.values() if p.effects}
    assert modes["Set_Bonus.Set_Bonus.Improved_Slow_2"] == {"Debuff"}
    assert modes["Set_Bonus.PVP_Set_Bonus.Energy_Neg_Mez_Res_2"] == {"Normal"}
