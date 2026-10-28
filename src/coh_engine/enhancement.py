"""Per-slot, per-aspect enhancement value (``I9Slot`` value path).

Ports ``I9Slot.GetScheduleMult`` (schedule tables x relative-level x Superior),
``I9Slot.GetRelativeLevelMultiplier``, and ``I9Slot.GetEnhancementEffect``
(MidsReborn ``Core/I9Slot.cs:38-153``). These turn a single slotted enhancement
into its contribution to one aspect, *before* aggregation and ED — those live in
:mod:`coh_engine.enh_pipeline`.

Inputs come from the dump harness: ``enhancement_effects.json`` (per-enhancement
``Superior`` + the ``Enhancement``-mode effects with their aspect/schedule/
multiplier) and ``builds/<name>/slots.json`` (the resolved per-power slot layout,
with I9Slot's Grade/IOLevel/RelativeLevel already clamped by Mids).

Every arithmetic step is quantized to float32 at the same points the C# ``float``
math rounds, so results track Mids bit-for-bit.
Spec: docs/engine/mids-port-spec.md § enhancement-value-pipeline.
"""

import json
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from types import MappingProxyType

from coh_engine.ed import Schedule
from coh_engine.maths import MathTables, f32

# eEnhGrade ordinals (Enums.cs:645-651): the clamp range for a slot's Grade.
_GRADE_NONE = 0
_GRADE_TRAINING_O = 1
_GRADE_DUAL_O = 2
_GRADE_SINGLE_O = 3

# eEnhRelative ordinals (Enums.cs:665-677): None=0 .. Even=4 .. PlusFive=9.
_REL_NONE = 0
_REL_EVEN = 4
_REL_PLUS_FIVE = 9

# GetRelativeLevelMultiplier double literals (I9Slot.cs:150-151), kept verbatim
# so the (float) narrowing matches Mids.
_REL_STEP_UP = 0.0500000007450581
_REL_STEP_DOWN = 0.100000001490116

_SUPERIOR_MULT = f32(1.25)
# I9Slot.cs:56 gates the per-effect Multiplier on abs(Multiplier) > 0.01.
_MULTIPLIER_EPSILON = 0.01


@dataclass(frozen=True, slots=True)
class EnhEffect:
    """One ``Enhancement``-mode effect an enhancement grants (``Enums.sEffect``)."""

    enhance: str  # eEnhance name — the aspect this effect boosts.
    enhance_sub_id: int
    schedule: int  # eSchedule ordinal.
    multiplier: float
    buff_mode: str  # eBuffDebuff name: Any / BuffOnly / DeBuffOnly.


@dataclass(frozen=True, slots=True)
class EnhancementRecord:
    """A DB enhancement's value-relevant fields (``Enhancement``).

    ``effects`` holds only ``Mode == Enhancement`` effects — the harness drops
    FX/proc effects, which contribute no numeric aspect (``GetEnhancementEffect``).
    """

    nid: int
    static_index: int
    type_name: str  # eType name: None / Normal / InventO / SpecialO / SetO.
    superior: bool
    effects: tuple[EnhEffect, ...]
    # nIDSet: the enhancement set this piece belongs to (-1 for non-set IOs). The
    # set-bonus tally (coh_engine.set_bonuses) reads it for SetO pieces only.
    nid_set: int = -1
    # IsProc, and the index of the enhancement's own power (-1 if none): the effects assembly adds.
    is_proc: bool = False
    power_nid: int = -1


@dataclass(frozen=True, slots=True)
class SlotRef:
    """A resolved enhancement slot (``SlotEntry`` + its ``I9Slot``).

    ``level`` is Mids' internal 0-based slot-placement level (the exemplar gate
    compares it against ``ForceLevel``). ``enh`` is the enhancement nID, ``-1``
    for an empty slot. ``grade``/``relative_level`` are ``eEnhGrade``/
    ``eEnhRelative`` ordinals; ``io_level`` is 0-based (49 for a level-50 IO).
    """

    level: int
    is_inherent: bool
    enh: int
    grade: int
    io_level: int
    relative_level: int


def load_enhancement_effects(path: Path | str) -> Mapping[int, EnhancementRecord]:
    """Parse an ``enhancement_effects.json`` dump, keyed by enhancement nID.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        records = json.load(stream)
    out = {
        r["Nid"]: EnhancementRecord(
            nid=r["Nid"],
            static_index=r["StaticIndex"],
            type_name=r["TypeName"],
            superior=r["Superior"],
            nid_set=r["nIDSet"],
            is_proc=r.get("IsProc", False),
            power_nid=r.get("PowerNid", -1),
            effects=tuple(
                EnhEffect(
                    enhance=fx["EnhanceName"],
                    enhance_sub_id=fx["EnhanceSubId"],
                    schedule=fx["Schedule"],
                    multiplier=f32(fx["Multiplier"]),
                    buff_mode=fx["BuffMode"],
                )
                for fx in r["Effects"]
            ),
        )
        for r in records
    }
    return MappingProxyType(out)


def load_build_slots(path: Path | str) -> Mapping[int, tuple[SlotRef, ...]]:
    """Parse a build's ``slots.json`` dump, keyed by the power's ``BuildIndex``.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        entries = json.load(stream)
    out = {
        e["BuildIndex"]: tuple(
            SlotRef(
                level=s["Level"],
                is_inherent=s["IsInherent"],
                enh=s["Enh"],
                grade=s["Grade"],
                io_level=s["IOLevel"],
                relative_level=s["RelativeLevel"],
            )
            for s in e["Slots"]
        )
        for e in entries
    }
    return MappingProxyType(out)


def _relative_level_mult(relative_level: int) -> float:
    """``GetRelativeLevelMultiplier`` (I9Slot.cs:138-153).

    None → 0.0 (kills the slot); each level above Even adds 5%, each below
    subtracts 10%. The bracket is computed in double then narrowed to float.
    """
    if relative_level == _REL_NONE:
        return 0.0
    offset = relative_level - _REL_EVEN
    if offset >= 0:
        return f32(offset * _REL_STEP_UP + 1.0)
    return f32(1.0 + offset * _REL_STEP_DOWN)


def _base_table_value(type_name: str, grade: int, io_level: int, schedule: int, tables: MathTables) -> float:
    """The ``num1`` schedule-table cell selected by enhancement type (I9Slot.cs:104-128)."""
    if type_name == "Normal":
        if grade == _GRADE_TRAINING_O:
            return tables.mult_to[schedule]
        if grade == _GRADE_DUAL_O:
            return tables.mult_do[schedule]
        if grade == _GRADE_SINGLE_O:
            return tables.mult_so[schedule]
        return 0.0  # Grade None (or unreachable) contributes nothing.
    if type_name in ("InventO", "SetO"):
        return tables.mult_io[io_level][schedule]
    if type_name == "SpecialO":
        return tables.mult_so[schedule]
    return 0.0  # eType.None: no switch case, stays 0.


def schedule_mult(
    *,
    type_name: str,
    superior: bool,
    io_level: int,
    grade: int,
    relative_level: int,
    schedule: int,
    tables: MathTables,
) -> float:
    """``I9Slot.GetScheduleMult`` (I9Slot.cs:66-136): one effect's pre-ED value.

    Clamps Grade/RelativeLevel/IOLevel to their valid ranges (Mids silently
    clamps rather than rejecting), reads the schedule-table cell for the
    enhancement type, then applies the relative-level multiplier and the
    Superior x1.25 bonus.
    """
    grade = min(max(grade, _GRADE_NONE), _GRADE_SINGLE_O)
    relative_level = min(max(relative_level, _REL_NONE), _REL_PLUS_FIVE)
    io_level = min(max(io_level, 0), len(tables.mult_io) - 1)
    if schedule in (Schedule.NONE, Schedule.MULTIPLE):
        return 0.0
    num1 = _base_table_value(type_name, grade, io_level, schedule, tables)
    num2 = f32(num1 * _relative_level_mult(relative_level))
    if superior:
        num2 = f32(num2 * _SUPERIOR_MULT)
    return num2


def enhancement_effect(
    record: EnhancementRecord,
    slot: SlotRef,
    *,
    aspect: str,
    sub_enh: int,
    mag: float,
    tables: MathTables,
) -> float:
    """``I9Slot.GetEnhancementEffect`` (I9Slot.cs:38-64): a slot's value for one aspect.

    Sums the schedule multipliers of every ``Enhancement``-mode effect on this
    enhancement that matches ``aspect`` (and ``sub_enh`` for Mez), gated by the
    buff/debuff sign of ``mag``. ``mag`` only gates inclusion — it never scales
    the return. An empty slot (``enh < 0``) contributes 0.
    """
    if slot.enh < 0:
        return 0.0
    total = 0.0
    for fx in record.effects:
        if fx.buff_mode == "DeBuffOnly" and not (mag <= 0.0):
            continue
        if fx.buff_mode == "BuffOnly" and not (mag >= 0.0):
            continue
        if fx.schedule == Schedule.NONE:
            continue
        if fx.enhance != aspect:
            continue
        if sub_enh >= 0 and sub_enh != fx.enhance_sub_id:
            continue
        m = schedule_mult(
            type_name=record.type_name,
            superior=record.superior,
            io_level=slot.io_level,
            grade=slot.grade,
            relative_level=slot.relative_level,
            schedule=fx.schedule,
            tables=tables,
        )
        if abs(fx.multiplier) > _MULTIPLIER_EPSILON:
            m = f32(m * fx.multiplier)
        total = f32(total + m)
    return total
