"""Set-bonus virtual power assembly (``Build.GenerateSetBonusData`` port).

Ports the four-stage MidsReborn pipeline that turns a build's slotted set-IO
enhancements into one synthetic "set bonus virtual power" whose effects are then
folded into character totals exactly like any other buff power
(``clsToonX.cs:2177-2178``):

1. **Tally** (``GenerateSetBonusData`` / ``I9SetData.Add``, ``Build.cs:1151-1176``,
    ``I9SetData.cs:31-52``) — per build power picked at ``Level <= ForceLevel``,
    count the SetO pieces per enhancement set. Non-SetO IOs never contribute. There
    is no per-slot level gate: if the power's pick level passes, all its slots count.
2. **BuildEffects** (``I9SetData.cs:73-128``) — tier bonuses activate at
    ``SlottedCount > 1`` (>=2 pieces) filtered by PvMode; per-enhancement specials
    / uniques activate at ``SlottedCount > 0`` (>=1 piece), matched by the specific
    slotted enhancement. Each activation contributes global ``set_bonus`` power ids.
3. **Rule of Five + MyPet skip** (``GetSetBonusVirtualPower``, ``Build.cs:1178-1226``)
    — a counter keyed on the exact ``set_bonus`` power id (the identity of the exact
    numeric bonus value) folds instances 1..5 and drops the 6th+; MyPet-only bonuses
    are skipped (but still consume a Rule-of-Five slot).
4. Assembly — the surviving bonus powers' effects are collected, in Mids' exact
    order (build-power order -> set first-encounter order -> tier-then-special id
    order -> effect-array order), into one :class:`~coh_engine.effect.Power`. The
    ordering is load-bearing: the buff/enh passes sum per bucket in float32, and
    float addition is not associative, so a different fold order would diverge.

The RESOLVED exemplar rule (spec § set-bonuses-ruleof5): Mids applies **no** -3
set-min retention. The one gate is power pick ``Level <= ForceLevel``.

Spec: docs/engine/mids-port-spec.md § set-bonuses-ruleof5.
"""

import json
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from types import MappingProxyType
from typing import Any

from coh_engine.effect import Effect, Power, _parse_effect
from coh_engine.enhancement import EnhancementRecord, SlotRef

# ePvX ordinals (Enums.cs:935-940): Any matches every mode.
PVX_ANY = 0
PVX_PVE = 1
PVX_PVP = 2

# Rule of Five: at most 5 instances of an exact bonus id fold (setCount < 6,
# incremented before the test — Build.cs:1213).
_RULE_OF_FIVE_LIMIT = 6

# The virtual power's build index sentinel — distinct from any real build power
# so its (absent) slots never collide in the enhancement-multiplier map.
SET_BONUS_BUILD_INDEX = -1


@dataclass(frozen=True, slots=True)
class BonusItem:
    """One tier or special bonus (``EnhancementSet.BonusItem``, EnhancementSet.cs:363)."""

    slotted: int  # 1-based piece threshold (tier); the special bonuses ignore it.
    pv_mode: int  # ePvX ordinal gating tier activation.
    index: tuple[int, ...]  # global set_bonus Power ids this bonus grants.


@dataclass(frozen=True, slots=True)
class EnhancementSetDef:
    """An enhancement set's bonus definition (``EnhancementSet``, EnhancementSet.cs:12)."""

    nid: int
    uid: str
    set_type: int  # eSetType ordinal.
    set_type_name: str  # eSetType name — matched against a power's ``set_types``.
    enhancements: tuple[int, ...]  # member enhancement nIDs; pairs 1:1 with special_bonus.
    bonus: tuple[BonusItem, ...]  # tiered count bonuses (>=2 pieces).
    special_bonus: tuple[BonusItem, ...]  # per-enhancement globals/uniques (>=1 piece).


@dataclass(frozen=True, slots=True)
class SetBonusPower:
    """A referenced ``set_bonus`` power: the effect payload the virtual power clones."""

    power_id: int
    full_name: str
    power_type: str
    my_pet_only: bool  # ShouldSkipEffects: Target AND EntitiesAffected both MyPet.
    effects: tuple[Effect, ...]


@dataclass(frozen=True, slots=True)
class SetBonusDb:
    """The DB-level set-bonus inputs: set definitions + referenced bonus powers."""

    sets: Mapping[int, EnhancementSetDef]
    powers: Mapping[int, SetBonusPower]


def _load_bonus_items(raw: Sequence[dict[str, Any]]) -> tuple[BonusItem, ...]:
    return tuple(
        BonusItem(slotted=int(b["Slotted"]), pv_mode=int(b["PvMode"]), index=tuple(int(x) for x in b["Index"]))
        for b in raw
    )


def load_enhancement_sets(path: Path | str) -> Mapping[int, EnhancementSetDef]:
    """Parse an ``enhancement_sets.json`` dump, keyed by set nID.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        records = json.load(stream)
    out = {
        r["Nid"]: EnhancementSetDef(
            nid=r["Nid"],
            uid=r["Uid"],
            set_type=r["SetType"],
            set_type_name=r["SetTypeName"],
            enhancements=tuple(r["Enhancements"]),
            bonus=_load_bonus_items(r["Bonus"]),
            special_bonus=_load_bonus_items(r["SpecialBonus"]),
        )
        for r in records
    }
    return MappingProxyType(out)


def load_set_bonus_powers(path: Path | str) -> Mapping[int, SetBonusPower]:
    """Parse a ``set_bonus_powers.json`` dump, keyed by global Power id.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        records = json.load(stream)
    out = {
        r["PowerId"]: SetBonusPower(
            power_id=r["PowerId"],
            full_name=r["FullName"],
            power_type=r["PowerType"],
            my_pet_only=bool(r["MyPetTarget"]) and bool(r["MyPetEntities"]),
            effects=tuple(_parse_effect(fx) for fx in r["Effects"]),
        )
        for r in records
    }
    return MappingProxyType(out)


def load_set_bonus_db(sets_path: Path | str, powers_path: Path | str) -> SetBonusDb:
    """Load the set definitions and referenced bonus powers into one :class:`SetBonusDb`."""
    return SetBonusDb(sets=load_enhancement_sets(sets_path), powers=load_set_bonus_powers(powers_path))


def power_accepts_set(power: Power, set_def: EnhancementSetDef) -> bool:
    """Whether ``power`` can slot IOs from ``set_def`` — its set type is in the power's list.

    Mirrors Mids' slot-time validity (``Power.SetTypes`` vs the set's ``SetType``,
    the check that strips an out-of-category set IO on load), matched on the
    eSetType ordinal. The set-bonus tally itself trusts the resolved slots Mids
    already validated; this predicate is the legality primitive for pre-slot checks
    and for asserting that assumption holds.
    """
    return set_def.set_type in power.set_types


def validate_set_slotting(
    powers: Sequence[Power],
    slots: Mapping[int, Sequence[SlotRef]],
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
) -> None:
    """Fail loud if any SetO enhancement is slotted in a power that rejects its set type.

    The critical legality guard for build generation: an AI- or operator-authored
    build must not force an IO set into a power that cannot accept it. Mids strips
    such slots on load, so a build round-tripped through Mids never trips this; a
    build assembled directly (optimizer, JSON DTO, hand-authored) can, and is
    refused here — fail fast, before any totals are computed.

    Only set IOs (``SetO``) are checked: their legality is the set-type map
    (``Power.SetTypes`` vs the set's ``SetType``). Generic-IO aspect legality
    (``BoostsAllowed``) is a separate, broader check not yet ported.

    Raises:
        ValueError: ``H-ENH-001`` if a SetO piece's set is not accepted by its
            power, or ``H-ENH-002`` if a slotted SetO piece maps to no known set.
    """
    for power in powers:
        for slot in slots.get(power.build_index, ()):
            if slot.enh < 0:
                continue
            record = enh_db.get(slot.enh)
            if record is None or record.type_name != "SetO":
                continue
            set_def = set_db.sets.get(record.nid_set)
            if set_def is None:
                raise ValueError(
                    f"H-ENH-002: enhancement nID {slot.enh} in {power.full_name!r} is a set IO whose set "
                    f"(nIDSet {record.nid_set}) is not in the set database; remove the piece or re-dump the "
                    "set database so it includes this set"
                )
            if not power_accepts_set(power, set_def):
                raise ValueError(
                    f"H-ENH-001: set {set_def.uid!r} (set type {set_def.set_type}) is not slottable in "
                    f"{power.full_name!r} (accepts set types {sorted(power.set_types)}); remove the piece or "
                    "move it to a power that accepts this set"
                )


@dataclass(slots=True)
class _SetInfo:
    """One set slotted in one power (``I9SetData.sSetInfo``) — mutable during tally."""

    set_idx: int
    slotted_count: int
    enh_indexes: list[int] = field(default_factory=list)
    power_ids: list[int] = field(default_factory=list)


def _tally_power(power_slots: Sequence[SlotRef], enh_db: Mapping[int, EnhancementRecord]) -> list[_SetInfo]:
    """``I9SetData.Add`` over a power's slots: SetO pieces tallied per set, in first-seen order."""
    infos: list[_SetInfo] = []
    by_set: dict[int, _SetInfo] = {}
    for slot in power_slots:
        if slot.enh < 0:
            continue
        # .get, not [], to match validate_set_slotting: an enh id absent from enh_db
        # (inconsistent slots/enh dumps) is skipped like a non-SetO piece, not a crash.
        record = enh_db.get(slot.enh)
        if record is None or record.type_name != "SetO":
            continue
        info = by_set.get(record.nid_set)
        if info is None:
            info = _SetInfo(set_idx=record.nid_set, slotted_count=0)
            by_set[record.nid_set] = info
            infos.append(info)
        info.slotted_count += 1
        info.enh_indexes.append(slot.enh)
    return infos


def _resolve_bonus_powers(set_def: EnhancementSetDef, info: _SetInfo, pv_mode: int) -> list[int]:
    """``I9SetData.BuildEffects`` for one set: the flat list of activated set_bonus ids.

    Tier bonuses (``Bonus[]``) need >=2 slotted pieces and a matching (or ``Any``)
    PvMode; per-enhancement specials (``SpecialBonus[]``) need >=1 piece and the
    specific member enhancement slotted. Duplicates are preserved — each independently
    advances the Rule-of-Five counter.
    """
    out: list[int] = []
    if info.slotted_count > 1:
        for bonus in set_def.bonus:
            if bonus.slotted <= info.slotted_count and bonus.pv_mode in (pv_mode, PVX_ANY):
                out.extend(bonus.index)
    # Specials need >=1 piece. C# guards on ``SlottedCount > 0`` (I9SetData.cs:100),
    # but a _SetInfo only exists after Add incremented its count, so the count is
    # always >=1 — the guard is a no-op here and is omitted.
    for i, member in enumerate(set_def.enhancements):
        if i >= len(set_def.special_bonus):
            continue
        special = set_def.special_bonus[i]
        for enh in info.enh_indexes:
            if enh == member:
                out.extend(special.index)
    return out


def piece_grants_special(set_def: EnhancementSetDef, enh_nid: int) -> bool:
    """Whether this member piece carries a per-enhancement special (global/proc).

    A special activates on its own (``SpecialBonus[]``, >=1 piece), so a piece that has
    one is useful slotted alone. Every other member only contributes through the tier
    bonuses, which need >=2 pieces of the set *in the same power* — a lone piece of those
    buys nothing beyond its raw enhancement value.

    Single-sources the ">=1 vs >=2" split that :func:`_resolve_bonus_powers` applies, so
    the legality warning and the bonus math cannot disagree.
    """
    for i, member in enumerate(set_def.enhancements):
        if member != enh_nid:
            continue
        if i < len(set_def.special_bonus) and set_def.special_bonus[i].index:
            return True
    return False


def _tally_build(
    powers: Sequence[Power],
    slots: Mapping[int, Sequence[SlotRef]],
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    *,
    force_level: int,
    pv_mode: int,
) -> list[_SetInfo]:
    """Stages 1-2 over the whole build: SetInfos with resolved bonus ids, in fold order.

    Build-power order is preserved (``GenerateSetBonusData`` iterates ``Powers`` in
    index order); within a power, sets keep first-encounter order. Only powers picked
    at ``Level <= ForceLevel`` contribute (the exemplar gate; no -3, no per-slot gate).
    """
    ordered: list[_SetInfo] = []
    for power in sorted(powers, key=lambda p: p.build_index):
        # The exemplar gate is the build's PICK level (``PowerEntry.Level``,
        # Build.cs:1160), not the DB minimum ``level``. They coincide at ForceLevel
        # 50; under exemplar a power whose DB minimum passes but whose pick level does
        # not must drop its set bonuses.
        if power.pick_level > force_level:
            continue
        infos = _tally_power(slots.get(power.build_index, ()), enh_db)
        for info in infos:
            info.power_ids = _resolve_bonus_powers(set_db.sets[info.set_idx], info, pv_mode)
        ordered.extend(infos)
    return ordered


def _fold_rule_of_five(ordered: Sequence[_SetInfo], set_db: SetBonusDb) -> list[Effect]:
    """``GetSetBonusVirtualPower`` fold: Rule of Five + MyPet skip -> cloned effects.

    The counter keys on the exact set_bonus power id, incremented before the ``< 6``
    test and before the MyPet skip (both consume a slot). A bonus power absent from
    the referenced dump models ``Database.Power[id] == null`` — it counts but adds no
    effects.
    """
    counter: dict[int, int] = {}
    effects: list[Effect] = []
    for info in ordered:
        for pid in info.power_ids:
            if pid < 0:
                continue
            counter[pid] = counter.get(pid, 0) + 1
            bonus_power = set_db.powers.get(pid)
            if bonus_power is not None and bonus_power.my_pet_only:
                continue
            if counter[pid] < _RULE_OF_FIVE_LIMIT and bonus_power is not None:
                effects.extend(bonus_power.effects)
    return effects


def build_set_bonus_power(
    powers: Sequence[Power],
    slots: Mapping[int, Sequence[SlotRef]],
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    *,
    force_level: int,
    disable_pve: bool,
) -> Power:
    """Assemble the set-bonus virtual power for a build (the full four-stage pipeline).

    Returns a synthetic :class:`~coh_engine.effect.Power` carrying the surviving bonus
    effects (empty when the build slots no set IOs). ``stat_include`` is True and the
    power type is neither ``Toggle`` nor ``GlobalBoost`` so the buff/enh passes fold it
    like any ordinary source; its (absent) slots leave every enhancement multiplier at 1.
    """
    pv_mode = PVX_PVP if disable_pve else PVX_PVE
    ordered = _tally_build(powers, slots, enh_db, set_db, force_level=force_level, pv_mode=pv_mode)
    effects = _fold_rule_of_five(ordered, set_db)
    return Power(
        build_index=SET_BONUS_BUILD_INDEX,
        nid_power=-1,
        full_name="Inherent.Inherent.Special_Set_Bonuses",
        static_index=-1,
        power_type="",
        forced_class="",
        click_buff=False,
        level=0,
        end_cost=0.0,
        activate_period=0.0,
        toggle_cost=0.0,
        variable_enabled=False,
        stat_include=True,
        variable_value=0,
        effects=tuple(effects),
    )
