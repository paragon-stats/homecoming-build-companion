"""Tests for coh_engine.enh_pipeline — aggregate-per-aspect + ED once.

The decisive goldens are reference values: the Hasten recharge aggregate
(99.08% / 83.32%) is the spec's worked assertion, reproduced here from the real
slotted fixtures + the real enhancement DB, not from the implementation. The
per-slot-ED trap is caught structurally — applying ED to each 0.424 slot then
summing yields 1.272, never 0.9908.
"""

from pathlib import Path

import pytest

from coh_engine.ed import Schedule
from coh_engine.enh_pipeline import aggregate_and_ed, schedule_for_enhance
from coh_engine.enhancement import (
    EnhancementRecord,
    SlotRef,
    load_build_slots,
    load_enhancement_effects,
)
from coh_engine.maths import MathTables

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES.parents[3] / "tests" / "fixtures" / "mids"

FORCE_LEVEL = 50  # config.json ForceLevel; every level-50 slot passes the gate.
HASTEN_INDEX = 6
DEFLECTION_INDEX = 2
CRAFTED_RECHARGE_NID = 43


@pytest.fixture(scope="module")
def enh_db() -> dict[int, EnhancementRecord]:
    return dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))


def _hasten_slots(build: str) -> tuple[SlotRef, ...]:
    return load_build_slots(MIDS / "builds" / build / "slots.json")[HASTEN_INDEX]


class TestHastenGoldens:
    """End-to-end aggregation over real slots -> ED once -> the spec goldens."""

    def test_three_recharge_ios_9908(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        slots = _hasten_slots("shield_scrapper_slotted")
        result = aggregate_and_ed(slots, aspect="RechargeTime", enh_db=enh_db, force_level=FORCE_LEVEL, tables=tables)
        assert round(result, 4) == 0.9908

    def test_two_recharge_ios_8332(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        slots = _hasten_slots("hasten_2rech")
        result = aggregate_and_ed(slots, aspect="RechargeTime", enh_db=enh_db, force_level=FORCE_LEVEL, tables=tables)
        assert round(result, 4) == 0.8332

    def test_ed_applied_once_not_per_slot(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        # Per-slot ED then sum would give 3x0.424 = 1.272 (each 0.424 is below
        # the 0.70 threshold, so unreduced). The correct once-on-aggregate value
        # is 0.9908 — well under 1.272.
        slots = _hasten_slots("shield_scrapper_slotted")
        result = aggregate_and_ed(slots, aspect="RechargeTime", enh_db=enh_db, force_level=FORCE_LEVEL, tables=tables)
        assert result < 1.0


class TestScheduleBAggregation:
    """Defense (schedule B) over Deflection's three generic Defense IOs."""

    def test_deflection_defense_uses_schedule_b(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        slots = load_build_slots(MIDS / "builds" / "shield_scrapper_slotted" / "slots.json")[DEFLECTION_INDEX]
        result = aggregate_and_ed(slots, aspect="Defense", enh_db=enh_db, force_level=FORCE_LEVEL, tables=tables)
        # Reference composition: three level-50 Defense IOs (MultIO[49][B]) summed,
        # then Enhancement Diversification on schedule B applied once.
        from coh_engine.ed import apply_ed

        pre_ed = 3 * tables.mult_io[49][Schedule.B]
        assert result == pytest.approx(apply_ed(Schedule.B, pre_ed, tables), abs=1e-6)
        # Not the schedule-A curve (the wrong-schedule bug would over-reduce).
        assert result != pytest.approx(apply_ed(Schedule.A, pre_ed, tables), abs=1e-6)


class TestForceLevelGate:
    def test_slot_at_or_above_force_level_excluded(
        self, enh_db: dict[int, EnhancementRecord], tables: MathTables
    ) -> None:
        rio = SlotRef(level=9, is_inherent=False, enh=CRAFTED_RECHARGE_NID, grade=0, io_level=49, relative_level=4)
        gated = SlotRef(level=60, is_inherent=False, enh=CRAFTED_RECHARGE_NID, grade=0, io_level=49, relative_level=4)
        one = aggregate_and_ed(
            (rio, gated), aspect="RechargeTime", enh_db=enh_db, force_level=FORCE_LEVEL, tables=tables
        )
        # Only the sub-ForceLevel slot counts: a single 0.424, below ED's first
        # threshold, so unreduced.
        assert one == pytest.approx(0.424, abs=1e-3)

    def test_slot_exactly_at_force_level_excluded(
        self, enh_db: dict[int, EnhancementRecord], tables: MathTables
    ) -> None:
        at_gate = SlotRef(
            level=FORCE_LEVEL, is_inherent=False, enh=CRAFTED_RECHARGE_NID, grade=0, io_level=49, relative_level=4
        )
        result = aggregate_and_ed(
            (at_gate,), aspect="RechargeTime", enh_db=enh_db, force_level=FORCE_LEVEL, tables=tables
        )
        assert result == 0.0


class TestEmptyAndUnmatched:
    def test_no_matching_slots_returns_zero(self, enh_db: dict[int, EnhancementRecord], tables: MathTables) -> None:
        empty = SlotRef(level=9, is_inherent=False, enh=-1, grade=0, io_level=1, relative_level=4)
        assert (
            aggregate_and_ed((empty,), aspect="RechargeTime", enh_db=enh_db, force_level=FORCE_LEVEL, tables=tables)
            == 0.0
        )


class TestScheduleForEnhance:
    """Enhancement.GetSchedule keyed by eEnhance name (Enhancement.cs:424-448)."""

    @pytest.mark.parametrize("aspect", ["Defense", "Range", "Resistance", "ToHit"])
    def test_schedule_b(self, aspect: str) -> None:
        assert schedule_for_enhance(aspect) == Schedule.B

    def test_interrupt_c(self) -> None:
        assert schedule_for_enhance("Interrupt") == Schedule.C

    @pytest.mark.parametrize("aspect", ["RechargeTime", "Accuracy", "Damage", "Heal", "EnduranceDiscount"])
    def test_default_a(self, aspect: str) -> None:
        assert schedule_for_enhance(aspect) == Schedule.A

    def test_mez_default_a(self) -> None:
        assert schedule_for_enhance("Mez") == Schedule.A
        assert schedule_for_enhance("Mez", sub=1) == Schedule.A

    @pytest.mark.parametrize("sub", [4, 5])
    def test_mez_sub_4_5_is_d(self, sub: int) -> None:
        assert schedule_for_enhance("Mez", sub=sub) == Schedule.D
