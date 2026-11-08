"""Tests for coh_engine.enhancement — per-slot, per-aspect enhancement value.

Ports ``I9Slot.GetScheduleMult`` / ``GetRelativeLevelMultiplier`` /
``GetEnhancementEffect`` (MidsReborn ``Core/I9Slot.cs:38-153``). Golden numbers
come from the Homecoming ``Maths.mhd`` tables and the two generic Invention IOs
the slotted parity fixtures use: ``Crafted_Recharge`` (RechargeTime, schedule A) and
``Crafted_Defense_Buff`` (Defense, schedule B).
"""

from pathlib import Path

import pytest

from coh_engine.ed import Schedule
from coh_engine.enhancement import (
    EnhancementRecord,
    EnhEffect,
    SlotRef,
    enhancement_effect,
    load_build_slots,
    load_enhancement_effects,
    schedule_mult,
)
from coh_engine.maths import MathTables

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES.parents[3] / "tests" / "fixtures" / "mids"

# eEnhRelative ordinals (Enums.cs:665-677).
REL_NONE = 0
REL_MINUS_THREE = 1
REL_EVEN = 4
REL_PLUS_ONE = 5
REL_PLUS_FIVE = 9

# eEnhGrade ordinals (Enums.cs:645-651).
GRADE_NONE = 0
GRADE_TRAINING_O = 1
GRADE_DUAL_O = 2
GRADE_SINGLE_O = 3

CRAFTED_RECHARGE_NID = 43
CRAFTED_DEFENSE_NID = 30


@pytest.fixture(scope="module")
def enh_db() -> dict[int, EnhancementRecord]:
    return dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))


def _slot(
    *,
    level: int = 9,
    is_inherent: bool = False,
    enh: int = CRAFTED_RECHARGE_NID,
    grade: int = GRADE_NONE,
    io_level: int = 49,
    relative_level: int = REL_EVEN,
) -> SlotRef:
    return SlotRef(
        level=level,
        is_inherent=is_inherent,
        enh=enh,
        grade=grade,
        io_level=io_level,
        relative_level=relative_level,
    )


class TestLoaders:
    def test_recharge_record_shape(self, enh_db: dict[int, EnhancementRecord]) -> None:
        rec = enh_db[CRAFTED_RECHARGE_NID]
        assert rec.type_name == "InventO"
        assert rec.superior is False
        assert len(rec.effects) == 1
        fx = rec.effects[0]
        assert fx.enhance == "RechargeTime"
        assert fx.schedule == Schedule.A
        assert fx.multiplier == pytest.approx(1.0)

    def test_defense_record_is_buff_only(self, enh_db: dict[int, EnhancementRecord]) -> None:
        fx = enh_db[CRAFTED_DEFENSE_NID].effects[0]
        assert fx.enhance == "Defense"
        assert fx.schedule == Schedule.B
        assert fx.buff_mode == "BuffOnly"

    def test_build_slots_load(self) -> None:
        slots = load_build_slots(MIDS / "builds" / "shield_scrapper_slotted" / "slots.json")
        # Hasten is build index 6 with three level-50 recharge IOs.
        hasten = slots[6]
        filled = [s for s in hasten if s.enh > -1]
        assert len(filled) == 3
        assert all(s.io_level == 49 and s.relative_level == REL_EVEN for s in filled)

    def test_empty_slot_has_enh_minus_one(self) -> None:
        slots = load_build_slots(MIDS / "builds" / "shield_scrapper_slotted" / "slots.json")
        deflection = slots[2]  # three defense IOs + one empty slot
        assert sum(1 for s in deflection if s.enh == -1) == 1


class TestScheduleMult:
    """``GetScheduleMult`` table lookup x relative-level x Superior."""

    def test_invento_recharge_level50_even(self, tables: MathTables) -> None:
        # Reference (spec § enhancement-value-pipeline): MultIO[49][A] = 0.424,
        # Even x 1.0, not Superior. 0.424 is the documented table value, not a
        # restatement of the implementation's own lookup.
        v = schedule_mult(
            type_name="InventO",
            superior=False,
            io_level=49,
            grade=GRADE_NONE,
            relative_level=REL_EVEN,
            schedule=Schedule.A,
            tables=tables,
        )
        assert v == pytest.approx(0.424, abs=1e-3)

    def test_invento_defense_level50_even(self, tables: MathTables) -> None:
        v = schedule_mult(
            type_name="InventO",
            superior=False,
            io_level=49,
            grade=GRADE_NONE,
            relative_level=REL_EVEN,
            schedule=Schedule.B,
            tables=tables,
        )
        assert v == pytest.approx(0.255, abs=1e-3)

    def test_none_and_multiple_schedule_zero(self, tables: MathTables) -> None:
        for sched in (Schedule.NONE, Schedule.MULTIPLE):
            assert (
                schedule_mult(
                    type_name="InventO",
                    superior=False,
                    io_level=49,
                    grade=GRADE_NONE,
                    relative_level=REL_EVEN,
                    schedule=sched,
                    tables=tables,
                )
                == 0.0
            )

    def test_relative_level_none_zeroes(self, tables: MathTables) -> None:
        assert (
            schedule_mult(
                type_name="InventO",
                superior=False,
                io_level=49,
                grade=GRADE_NONE,
                relative_level=REL_NONE,
                schedule=Schedule.A,
                tables=tables,
            )
            == 0.0
        )

    def test_relative_level_plus_and_minus(self, tables: MathTables) -> None:
        # Reference ratios: +5 -> x1.25 (0.424x1.25=0.530), -3 -> x0.70
        # (0.424x0.70=0.2968). Constants documented in spec § Step 1.
        plus5 = schedule_mult(
            type_name="InventO",
            superior=False,
            io_level=49,
            grade=GRADE_NONE,
            relative_level=REL_PLUS_FIVE,
            schedule=Schedule.A,
            tables=tables,
        )
        minus3 = schedule_mult(
            type_name="InventO",
            superior=False,
            io_level=49,
            grade=GRADE_NONE,
            relative_level=REL_MINUS_THREE,
            schedule=Schedule.A,
            tables=tables,
        )
        assert plus5 == pytest.approx(0.530, abs=1e-3)
        assert minus3 == pytest.approx(0.2968, abs=1e-3)

    def test_superior_multiplies_by_1_25(self, tables: MathTables) -> None:
        # Superior x1.25 on the level-50 recharge IO: 0.424 x 1.25 = 0.530.
        sup = schedule_mult(
            type_name="InventO",
            superior=True,
            io_level=49,
            grade=GRADE_NONE,
            relative_level=REL_EVEN,
            schedule=Schedule.A,
            tables=tables,
        )
        assert sup == pytest.approx(0.530, abs=1e-3)

    def test_normal_grade_single_o(self, tables: MathTables) -> None:
        # Reference MultSO[0][A] = 0.333 (spec grade-effectiveness table).
        v = schedule_mult(
            type_name="Normal",
            superior=False,
            io_level=0,
            grade=GRADE_SINGLE_O,
            relative_level=REL_EVEN,
            schedule=Schedule.A,
            tables=tables,
        )
        assert v == pytest.approx(0.333, abs=1e-3)

    def test_normal_grade_none_is_zero(self, tables: MathTables) -> None:
        assert (
            schedule_mult(
                type_name="Normal",
                superior=False,
                io_level=0,
                grade=GRADE_NONE,
                relative_level=REL_EVEN,
                schedule=Schedule.A,
                tables=tables,
            )
            == 0.0
        )

    def test_normal_grade_training_o(self, tables: MathTables) -> None:
        # Reference MultTO[0][A] = 0.083 (spec grade-effectiveness table).
        v = schedule_mult(
            type_name="Normal",
            superior=False,
            io_level=0,
            grade=GRADE_TRAINING_O,
            relative_level=REL_EVEN,
            schedule=Schedule.A,
            tables=tables,
        )
        assert v == pytest.approx(0.083, abs=1e-3)

    def test_normal_grade_dual_o(self, tables: MathTables) -> None:
        # Reference MultDO[0][A] = 0.167 (spec grade-effectiveness table).
        v = schedule_mult(
            type_name="Normal",
            superior=False,
            io_level=0,
            grade=GRADE_DUAL_O,
            relative_level=REL_EVEN,
            schedule=Schedule.A,
            tables=tables,
        )
        assert v == pytest.approx(0.167, abs=1e-3)

    def test_specialo_uses_so_table(self, tables: MathTables) -> None:
        # SpecialO reads the SO table (0.333), NOT the level-50 IO cell (0.424) —
        # the distinguishing reference behavior (spec § Step 1 switch).
        v = schedule_mult(
            type_name="SpecialO",
            superior=False,
            io_level=49,
            grade=GRADE_NONE,
            relative_level=REL_EVEN,
            schedule=Schedule.A,
            tables=tables,
        )
        assert v == pytest.approx(0.333, abs=1e-3)

    def test_type_none_is_zero(self, tables: MathTables) -> None:
        assert (
            schedule_mult(
                type_name="None",
                superior=False,
                io_level=49,
                grade=GRADE_NONE,
                relative_level=REL_EVEN,
                schedule=Schedule.A,
                tables=tables,
            )
            == 0.0
        )

    def test_io_level_clamps(self, tables: MathTables) -> None:
        # io_level 999 clamps to the last row (level 53) -> MultIO[52][A] = 0.435.
        v = schedule_mult(
            type_name="InventO",
            superior=False,
            io_level=999,
            grade=GRADE_NONE,
            relative_level=REL_EVEN,
            schedule=Schedule.A,
            tables=tables,
        )
        assert v == pytest.approx(0.435, abs=1e-3)


class TestEnhancementEffect:
    def test_recharge_matches_aspect(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        rec = enh_db[CRAFTED_RECHARGE_NID]
        v = enhancement_effect(rec, _slot(), aspect="RechargeTime", sub_enh=-1, mag=1.0, tables=tables)
        assert v == pytest.approx(0.424, abs=1e-3)

    def test_wrong_aspect_returns_zero(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        rec = enh_db[CRAFTED_RECHARGE_NID]
        assert enhancement_effect(rec, _slot(), aspect="Defense", sub_enh=-1, mag=1.0, tables=tables) == 0.0

    def test_empty_slot_returns_zero(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        rec = enh_db[CRAFTED_RECHARGE_NID]
        v = enhancement_effect(rec, _slot(enh=-1), aspect="RechargeTime", sub_enh=-1, mag=1.0, tables=tables)
        assert v == 0.0

    def test_buff_only_skipped_for_debuff_mag(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        rec = enh_db[CRAFTED_DEFENSE_NID]
        slot = _slot(enh=CRAFTED_DEFENSE_NID)
        # BuffOnly effect requires mag >= 0; a negative mag excludes it.
        assert enhancement_effect(rec, slot, aspect="Defense", sub_enh=-1, mag=-1.0, tables=tables) == 0.0
        assert enhancement_effect(rec, slot, aspect="Defense", sub_enh=-1, mag=1.0, tables=tables) > 0.0


def _synth_record(
    *,
    enhance: str = "RechargeTime",
    enhance_sub_id: int = -1,
    schedule: int = Schedule.A,
    multiplier: float = 1.0,
    buff_mode: str = "Any",
) -> EnhancementRecord:
    """A one-effect InventO record for exercising GetEnhancementEffect's gates."""
    return EnhancementRecord(
        nid=9999,
        static_index=9999,
        type_name="InventO",
        superior=False,
        effects=(
            EnhEffect(
                enhance=enhance,
                enhance_sub_id=enhance_sub_id,
                schedule=schedule,
                multiplier=multiplier,
                buff_mode=buff_mode,
            ),
        ),
    )


class TestEnhancementEffectGates:
    """The per-effect inclusion gates of GetEnhancementEffect (I9Slot.cs:49-58)."""

    def test_debuff_only_skipped_for_buff_mag(self, tables: MathTables) -> None:
        rec = _synth_record(buff_mode="DeBuffOnly")
        slot = _slot(enh=9999)
        # DeBuffOnly requires mag <= 0; a positive mag excludes it, a negative includes.
        assert enhancement_effect(rec, slot, aspect="RechargeTime", sub_enh=-1, mag=1.0, tables=tables) == 0.0
        assert enhancement_effect(rec, slot, aspect="RechargeTime", sub_enh=-1, mag=-1.0, tables=tables) > 0.0

    def test_schedule_none_effect_skipped(self, tables: MathTables) -> None:
        rec = _synth_record(schedule=Schedule.NONE)
        slot = _slot(enh=9999)
        assert enhancement_effect(rec, slot, aspect="RechargeTime", sub_enh=-1, mag=1.0, tables=tables) == 0.0

    def test_sub_enh_mismatch_skipped(self, tables: MathTables) -> None:
        rec = _synth_record(enhance="Mez", enhance_sub_id=2)
        slot = _slot(enh=9999)
        assert enhancement_effect(rec, slot, aspect="Mez", sub_enh=3, mag=1.0, tables=tables) == 0.0
        assert enhancement_effect(rec, slot, aspect="Mez", sub_enh=2, mag=1.0, tables=tables) > 0.0

    def test_multiplier_below_epsilon_ignored(self, tables: MathTables) -> None:
        # |Multiplier| <= 0.01 means "no multiplier" — the base schedule value stands.
        rec = _synth_record(multiplier=0.0)
        slot = _slot(enh=9999)
        v = enhancement_effect(rec, slot, aspect="RechargeTime", sub_enh=-1, mag=1.0, tables=tables)
        assert v == pytest.approx(0.424, abs=1e-3)


class TestHastenGoldenPerSlot:
    """Each level-50 recharge IO contributes 0.424; the aggregate/ED is enh_pipeline's job."""

    def test_single_recharge_io(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        rec = enh_db[CRAFTED_RECHARGE_NID]
        per_slot = enhancement_effect(rec, _slot(), aspect="RechargeTime", sub_enh=-1, mag=1.0, tables=tables)
        assert round(3 * per_slot, 3) == 1.272
        assert round(2 * per_slot, 3) == 0.848
