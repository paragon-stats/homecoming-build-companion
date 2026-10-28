"""Aggregate per-aspect enhancement value across a power's slots, then ED once.

This is Pass 1 (``GBPA_Pass1_EnhancePreED``, sum per aspect across slots gated by
the ForceLevel exemplar cutoff) followed by Pass 2 (``GBPA_Pass2_ApplyED``, apply
Enhancement Diversification exactly once on the aggregate). The per-slot values
come from :mod:`coh_engine.enhancement`; ED comes from :mod:`coh_engine.ed`.

The ED schedule is chosen by ``Enhancement.GetSchedule`` keyed on the aspect's
``eEnhance`` name (``Enhancement.cs:424-448``) — distinct from :func:`ed.schedule_for`,
which keys on the curated ``ed.Aspect`` enum.
Spec: docs/engine/mids-port-spec.md § enhancement-value-pipeline (Steps 3-4).
"""

from collections.abc import Iterable, Mapping

from coh_engine.ed import Schedule, apply_ed
from coh_engine.enhancement import EnhancementRecord, SlotRef, enhancement_effect
from coh_engine.maths import MathTables, f32

# eEnhance names whose ED schedule is B (Enhancement.GetSchedule, Enhancement.cs:427-444).
_SCHEDULE_B_ENHANCE = frozenset({"Defense", "Range", "Resistance", "ToHit"})
# eMez sub-types that push a Mez enhancement onto schedule D.
_MEZ_SCHEDULE_D_SUBS = frozenset({4, 5})


def schedule_for_enhance(aspect: str, sub: int = -1) -> Schedule:
    """``Enhancement.GetSchedule(eEnhance, tSub)`` (Enhancement.cs:424-448)."""
    if aspect in _SCHEDULE_B_ENHANCE:
        return Schedule.B
    if aspect == "Interrupt":
        return Schedule.C
    if aspect == "Mez":
        return Schedule.D if sub in _MEZ_SCHEDULE_D_SUBS else Schedule.A
    return Schedule.A


def aggregate_and_ed(
    slots: Iterable[SlotRef],
    *,
    aspect: str,
    enh_db: Mapping[int, EnhancementRecord],
    force_level: int,
    tables: MathTables,
    sub_enh: int = -1,
    mag: float = 1.0,
    pre_ed_addend: float = 0.0,
    skip_ed: bool = False,
) -> float:
    """Sum one aspect's per-slot values across ``slots``, then apply ED once.

    A slot contributes only if it is filled (``enh > -1``) and placed below
    ``force_level`` (``Slots[i].Level < ForceLevel`` — the exemplar/build-level
    cutoff). ``mag`` is the buff/debuff sign the per-slot value gate reads; for a
    scalar aspect it is the constant 1 Mids passes, for an effect-borne aspect it
    is the effect's current magnitude. ``pre_ed_addend`` is an extra pre-ED term
    summed after the slotted values and before ED — the incarnate ``IgnoreED`` =
    false contribution Pass 1 adds alongside the slotted aggregate
    (:mod:`coh_engine.incarnate`), so it is co-ED'd with the slots exactly as
    ``GBPA_ApplyIncarnateEnhancements`` (pre-ED) then ``GBPA_Pass2_ApplyED`` do. The
    pre-ED sum accumulates in float32, and ED is applied a single time on the
    schedule for ``aspect`` unless ``skip_ed`` is set, for an effect ED never reaches.
    """
    pre_ed = 0.0
    for slot in slots:
        if slot.enh < 0 or slot.level >= force_level:
            continue
        record = enh_db[slot.enh]
        pre_ed = f32(pre_ed + enhancement_effect(record, slot, aspect=aspect, sub_enh=sub_enh, mag=mag, tables=tables))
    pre_ed = f32(pre_ed + pre_ed_addend)
    return pre_ed if skip_ed else apply_ed(schedule_for_enhance(aspect, sub_enh), pre_ed, tables)
