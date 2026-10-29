"""Power assembly: each build power's effect list as Mids builds it before computing totals.

Mids does not compute totals from the database power. It first assembles each build power
(``GBPA_SubPass0_AssemblePowerEntry``): a power override, the slotted enhancements' own effects,
pet absorption, GrantPower replacement, ExecutePower expansion and sub-power effects, in that order.
Once every power is assembled, a cross-power pass absorbs the GlobalBoost effects of the other active
powers into each power (``GBPA_ApplyIncarnateEnhancements`` with effect type GrantPower).

The output is plain :class:`Effect` records, re-indexed so ``Effect.index`` is the position in the list.
Their ``probability`` is what Mids' ``Probability`` reads after a recompute: the base probability plus
the build's chance modifier for the effect's id (``ModifyEffects``), from the cross-power pass and the
build powers' GlobalChanceMod effects, then the set-bonus virtual power's, each reward taken once.
Contract: ``.claude/plans/assembly-seams.md`` § Seam 2.

Three things are refused rather than guessed: an override redirect (``E88``, the export carries no
redirect target), an entity or powerset the export does not hold (``E89``) and a GrantPower gated on a
special case (``E91``, character state is not modelled). A pointer to any other record the inputs do not
hold refuses with ``E90``.
"""

import math
import re
from collections import ChainMap
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, replace
from types import MappingProxyType

from coh_engine.assembly_data import (
    AssemblyEntities,
    AssemblyEntity,
    AssemblyPower,
    BuildPowerAssembly,
)
from coh_engine.effect import Effect, Power
from coh_engine.enhancement import EnhancementRecord, SlotRef
from coh_engine.maths import f32
from coh_engine.set_bonuses import SetBonusDb

Conditions = Callable[[Effect], bool]

_GRANT_ROUNDS = 100
_EXECUTE_DEPTH = 5
_SLOTTABLE_SET_TYPES = frozenset({"Primary", "Secondary", "Ancillary", "Inherent", "Pool"})
_BOOST_TABLE = re.compile(r"^(Melee|Ranged)_Boosts_")
# The build's chance modifiers are empty while each power is assembled; the cross-power pass fills them.
_NO_MODS: Mapping[str, float] = MappingProxyType({})


@dataclass(frozen=True, slots=True)
class AssembledPower:
    """One build power after assembly: its effects, and the aspects it ignores once pet absorption copied them."""

    effects: tuple[Effect, ...]
    ignore_enh: frozenset[str]


@dataclass(frozen=True, slots=True)
class _Inputs:
    powers: Mapping[int, AssemblyPower]
    entities: AssemblyEntities
    archetype_index: int


def _record[T](table: Mapping[int, T], key: int, what: str, where: str) -> T:
    if key in table:
        return table[key]
    raise ValueError(
        f"E90: {where} points at {what} {key}, which the assembly inputs do not hold; expected every record "
        "assembly reaches; re-run the Mids dump harness so the export carries it"
    )


def _computed(fx: Effect) -> bool:
    """Whether Mids computes ``Probability`` from PPM or an expression, so the stored value stands."""
    return fx.procs_per_minute > 0 or (fx.attrib_type == "Expression" and fx.expr_probability.strip() != "")


def _chance_mod(fx: Effect, mods: Mapping[str, float]) -> float:
    return mods.get(fx.effect_id, 0.0) if fx.effect_id else 0.0


def _probability(fx: Effect, mods: Mapping[str, float]) -> float:
    """``Effect.Probability``: the base probability plus the build's chance modifier for the effect's id."""
    return fx.probability if _computed(fx) else max(0.0, min(1.0, fx.base_probability + _chance_mod(fx, mods)))


def _with_probability(fx: Effect, value: float, mods: Mapping[str, float]) -> Effect:
    """``Effect.EffectiveProbability``: the base probability that makes ``Probability`` read ``value``."""
    fx = replace(fx, base_probability=f32(value - _chance_mod(fx, mods)))
    return replace(fx, probability=_probability(fx, mods))


def _set_ticks(duration: float, interval: float) -> int:
    return 1 + math.floor(duration / interval) if interval > 0 else 0


def _pet_powers(nid: int, where: str, inputs: _Inputs) -> list[AssemblyPower]:
    """A pet powerset's powers in order, its empty slots left out."""
    if nid not in inputs.entities.powersets:
        raise NotImplementedError(
            f"E89: {where} needs powerset {nid}, which assembly_entities.json does not hold; expected every "
            "powerset pet absorption reads; re-run the Mids dump harness so the export carries it"
        )
    powerset = inputs.entities.powersets[nid]
    return [_record(inputs.powers, p, "pet power", powerset.full_name) for p in powerset.power_nids if p > -1]


def _entity(entities: AssemblyEntities, nid: int, where: str) -> AssemblyEntity:
    if nid in entities.entities:
        return entities.entities[nid]
    raise NotImplementedError(
        f"E89: {where} creates entity {nid}, which assembly_entities.json does not hold; expected every "
        "entity pet absorption reads; re-run the Mids dump harness so the export carries it"
    )


def _pet_duration(source: AssemblyPower, inputs: _Inputs) -> float:
    """The delay of the last self SilentKill in a Pet source's powerset: the duration its effects are cut to."""
    if source.power_set_type != "Pet":
        return 0.0
    owner = next((s for s in inputs.entities.powersets.values() if source.nid in s.power_nids), None)
    if owner is None:
        raise NotImplementedError(
            f"E89: {source.full_name} is a Pet power whose powerset assembly_entities.json does not hold; "
            "expected its powerset for the pet duration rule; re-run the Mids dump harness so the export carries it"
        )
    duration = 0.0
    for power in _pet_powers(owner.nid, source.full_name, inputs):
        for fx in power.effects:
            if fx.effect_type == "SilentKill" and fx.to_who == "Self" and fx.delayed_time > 0:
                duration = fx.delayed_time
    return duration


def _absorb(
    source: AssemblyPower,
    duration: float,
    class_id: int,
    inputs: _Inputs,
    *,
    delay: float = 0.0,
    grant: bool = True,
    chosen: Effect | None = None,
) -> list[Effect]:
    """``Power.AbsorbEffects``: all of ``source``'s effects, or only ``chosen``, as absorbed copies."""
    pet_duration = _pet_duration(source, inputs)
    if pet_duration > 0 and (duration < 0.01 or duration > pet_duration):
        duration = pet_duration
    if chosen is None:
        picked = [
            fx
            for fx in source.effects
            if grant or source.entities_affected != {"Caster"} or fx.effect_type == "EntCreate"
        ]
    else:
        picked = [chosen]
    auto_hit = source.entities_auto_hit
    to_target = ("Friend" in auto_hit or "MyPet" in auto_hit) and (chosen is not None or "Caster" not in auto_hit)
    out = []
    for fx in picked:
        absorbed = replace(
            fx,
            absorbed_effect=True,
            absorbed_power_type=source.power_type,
            absorbed_class_n_id=class_id,
            absorbed_power_nid=source.nid,
            ticks=_set_ticks(duration, source.activate_period)
            if source.power_type in ("Auto_", "Toggle")
            else fx.ticks,
            # Stacking Yes multiplies scale by the stack count, which assembly always passes as 1.
            to_who="Target" if to_target else fx.to_who,
            delayed_time=delay if delay > 0 else fx.delayed_time,
            n_duration=duration if duration > 0 and pet_duration > 0 else fx.n_duration,
        )
        out.append(absorbed)
    return out


def _check_override(power: AssemblyPower) -> None:
    if not power.has_power_override_effect:
        return
    for fx in power.effects:
        if fx.effect_type == "PowerRedirect" and abs(fx.probability - 1) < 0.01 and fx.special_case == "None":
            raise ValueError(
                f"E88: {power.full_name} effect {fx.index} redirects the power, but the export carries no "
                "redirect target; expected the target power in the assembly inputs, found none; export the "
                "effect's Override power before assembling this build"
            )


def _pets_only_copies(fx: Effect, current: Sequence[Effect], entities: AssemblyEntities, where: str) -> int:
    """How many times a pets-only effect is copied onto a summon power.

    Once per power of the summoned entity's first powerset that has an effect of the same type, and once
    more when the last of those powers has one, as Mids' loop leaves its flag set for the common add.
    """
    uid = next(e.summon for e in current if e.effect_type == "EntCreate")
    entity = next((e for e in entities.entities.values() if e.uid == uid), None)
    if entity is None:
        raise NotImplementedError(
            f"E89: {where} summons entity {uid!r}, which assembly_entities.json does not hold; expected every "
            "entity a pets-only enhancement effect reads; re-run the Mids dump harness so the export carries it"
        )
    matches = [fx.effect_type in types for types in entity.first_powerset_effect_types]
    return sum(matches) + (1 if matches and matches[-1] else 0)


def _enhancement_effects(
    build_power: BuildPowerAssembly,
    slots: Sequence[SlotRef],
    current: Sequence[Effect],
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    inputs: _Inputs,
) -> tuple[list[Effect], bool]:
    """``GBPA_AddEnhFX``: the slotted enhancements' own power effects, and whether one is a GrantPower."""
    where = f"build power {build_power.build_index} ({build_power.power.full_name})"
    summons = any(fx.effect_type == "EntCreate" for fx in current)
    out: list[Effect] = []
    grants = False
    for slot in slots:
        if slot.enh < 0:
            continue
        enh = _record(enh_db, slot.enh, "enhancement", where)
        if enh.power_nid < 0 or (build_power.proc_include and enh.is_proc) or enh.nid_set < 0:
            continue
        set_def = _record(set_db.sets, enh.nid_set, "enhancement set", f"enhancement {enh.nid}")
        position = set_def.enhancements.index(slot.enh) if slot.enh in set_def.enhancements else -1
        for fx in _record(inputs.powers, enh.power_nid, "power", f"enhancement {enh.nid}").effects:
            if summons and "Set_Bonus" in fx.reward and "Pet" in set_def.set_type_name:
                copies = _pets_only_copies(fx, current, inputs.entities, where)
            elif (
                position < 0
                or set_def.special_bonus[position].index
                or (enh.effects and _BOOST_TABLE.match(fx.modifier_table))
            ):
                continue
            else:
                copies = 1
            copy = replace(
                fx,
                is_enhancement_effect=True,
                ignore_scaling=enh.is_proc,
                absorbed_effect=True,
                buffable=False,
                absorbed_power_nid=-1,
            )
            out.extend([copy] * copies)
            grants = grants or (copies > 0 and fx.effect_type == "GrantPower")
    return out, grants


def _absorb_pets(power: AssemblyPower, effects: list[Effect], inputs: _Inputs) -> AssemblyPower:
    """``Power.AbsorbPetEffects`` with a stack count of 1: the power, with any attributes it took, back."""
    if not (power.absorb_summon_effects or power.absorb_summon_attributes):
        return power
    creates = [
        fx for fx in effects if fx.effect_type == "EntCreate" and fx.n_summon > -1 and abs(fx.probability - 1) < 0.01
    ]
    # The attribute loop stops at the first pet power unless the database power hits only its caster or no one.
    first_only = power.entities_auto_hit not in (frozenset(), frozenset({"Caster"}))
    for create in creates:
        entity = _entity(inputs.entities, create.n_summon, power.full_name)
        if not entity.powerset_nids:
            continue
        if power.absorb_summon_attributes and entity.powerset_nids[0] > -1:
            power = _take_attributes(power, entity.powerset_nids[0], first_only, inputs)
        if not power.absorb_summon_effects:
            continue
        for set_nid in entity.powerset_nids:
            if set_nid < 0:
                continue
            for pet in _pet_powers(set_nid, power.full_name, inputs):
                effects.extend(_absorb_pet_power(pet, create, entity, inputs))
    return power


def _take_attributes(power: AssemblyPower, powerset_nid: int, first_only: bool, inputs: _Inputs) -> AssemblyPower:
    """The pet powers' targeting and ignored aspects copied onto the absorbing power, the last copy winning."""
    for pet in _pet_powers(powerset_nid, power.full_name, inputs):
        power = replace(
            power,
            entities_affected=pet.entities_affected,
            entities_auto_hit=pet.entities_auto_hit if power.entities_auto_hit else power.entities_auto_hit,
            ignore_enh=pet.ignore_enh,
        )
        if first_only:
            break
    return power


def _absorb_pet_power(pet: AssemblyPower, create: Effect, entity: AssemblyEntity, inputs: _Inputs) -> list[Effect]:
    """One pet power's effects as the absorbing power takes them, then those of the entities it creates."""

    def take(source: AssemblyPower) -> list[Effect]:
        return _absorb(source, create.duration, entity.class_nid, inputs, delay=create.delayed_time, grant=False)

    out = take(pet)
    for spawn in pet.effects:
        if spawn.effect_type != "EntCreate" or spawn.n_summon <= -1:
            continue
        child = _entity(inputs.entities, spawn.n_summon, pet.full_name)
        if not child.powerset_nids or child.powerset_nids[0] < 0:
            continue
        for power in _pet_powers(child.powerset_nids[0], pet.full_name, inputs):
            out.extend(take(power))
    return out


def _can_grant(fx: Effect, conditions: Conditions) -> bool:
    """``Effect.CanGrantPower``."""
    if fx.special_case != "None":
        raise NotImplementedError(
            f"E91: GrantPower effect {fx.index} ({fx.summon}) is gated on special case {fx.special_case}; "
            "expected special case None, found one; the character state it reads is not modelled"
        )
    return not fx.conditionals or conditions(fx)


def _apply_grants(effects: list[Effect], power: AssemblyPower, inputs: _Inputs, conditions: Conditions) -> None:
    """``Power.ApplyGrantPowerEffects``, editing ``effects`` in place."""
    for _ in range(_GRANT_ROUNDS):
        found = [
            i
            for i, fx in enumerate(effects)
            if fx.effect_type == "GrantPower"
            and fx.effect_class != "Ignored"
            and fx.n_summon > -1
            and _can_grant(fx, conditions)
        ]
        if not found:
            return
        for i in found:
            grant = effects[i] = replace(effects[i], effect_class="Ignored")
            where = f"{power.full_name} effect {grant.index}"
            granted = _record(inputs.powers, grant.n_summon, "granted power", where)
            effects.extend(_absorb(granted, grant.duration, inputs.archetype_index, inputs))
            last = effects[-1]
            if grant.absorbed_power_nid > -1:
                last = replace(last, absorbed_power_type=grant.absorbed_power_type)
            if last.effect_type != "GrantPower":
                last = replace(last, to_who=grant.to_who)
            if last.to_who == "All" and not {"Caster", "Friend", "Foe"} <= power.entities_affected:
                last = replace(last, to_who="Target")
            last = replace(last, is_enhancement_effect=grant.is_enhancement_effect)
            if grant.base_probability < 1:
                last = _with_probability(
                    last, f32(_probability(last, _NO_MODS) * _probability(grant, _NO_MODS)), _NO_MODS
                )
            effects[-1] = last


def _inherit(fx: Effect, caller: Effect) -> Effect:
    """An executed power's effect as it takes the place of the ExecutePower effect that ran it."""
    own, called = _probability(fx, _NO_MODS), _probability(caller, _NO_MODS)
    fx = replace(
        fx,
        base_probability=own if called in (0.0, 1.0) else min(own, called),
        n_duration=caller.n_duration if caller.n_duration > 0 else fx.n_duration,
        delayed_time=f32(fx.delayed_time + caller.delayed_time),
        procs_per_minute=caller.procs_per_minute,
        conditionals=fx.conditionals + caller.conditionals,
        active_conditionals_count=fx.active_conditionals_count + caller.active_conditionals_count,
        ticks=caller.ticks if caller.ticks > 0 and fx.ticks == 0 else fx.ticks,
    )
    return replace(fx, probability=_probability(fx, _NO_MODS))


def _expand_executes(effects: Sequence[Effect], by_name: Mapping[str, AssemblyPower], depth: int = 0) -> list[Effect]:
    """``Power.ProcessExecutesInner``: each ExecutePower replaced in place by the executed power's effects."""
    if depth > _EXECUTE_DEPTH:
        return []
    out: list[Effect] = []
    for fx in effects:
        if fx.effect_type != "ExecutePower":
            out.append(fx)
            continue
        executed = by_name.get(fx.summon) if fx.summon else None
        if executed is not None:
            out.extend(_inherit(sub, fx) for sub in _expand_executes(executed.effects, by_name, depth + 1))
    return out


def _reindex(effects: Sequence[Effect]) -> tuple[Effect, ...]:
    return tuple(replace(fx, index=i) for i, fx in enumerate(effects))


def apply_assembly(
    powers: Sequence[Power], build: Mapping[int, BuildPowerAssembly], assembled: Mapping[int, AssembledPower]
) -> list[Power]:
    """The build's powers with their assembled effects and ignored aspects, and the scalars expressions read."""
    out = []
    for power in powers:
        result = assembled[power.build_index]
        own = build[power.build_index].power
        ignore_enh = (
            power.ignore_enh if frozenset(power.ignore_enh) == result.ignore_enh else tuple(sorted(result.ignore_enh))
        )
        out.append(
            replace(
                power,
                effects=result.effects,
                ignore_enh=ignore_enh,
                aoe_modifier=own.aoe_modifier,
                base_recharge_time=own.base_recharge_time,
            )
        )
    return out


def _assemble(
    build_power: BuildPowerAssembly,
    slots: Sequence[SlotRef],
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    inputs: _Inputs,
    conditions: Conditions,
) -> tuple[AssemblyPower, list[Effect]]:
    power = build_power.power
    _check_override(power)
    effects = list(power.effects)
    enh_effects, enh_grants = _enhancement_effects(build_power, slots, effects, enh_db, set_db, inputs)
    effects.extend(enh_effects)
    power = _absorb_pets(power, effects, inputs)
    if power.has_grant_power_effect or enh_grants:
        _apply_grants(effects, power, inputs, conditions)
    effects = _expand_executes(effects, {p.full_name: p for p in inputs.powers.values()})
    for nid, stat_include in build_power.sub_powers:
        if nid > -1 and stat_include:
            where = f"build power {build_power.build_index} ({power.full_name})"
            sub = _record(inputs.powers, nid, "sub-power", where)
            effects.extend(
                replace(fx, absorbed_effect=True, absorbed_power_nid=nid, absorbed_power_type=sub.power_type)
                for fx in sub.effects
            )
    return power, effects


def assemble_power(
    build_power: BuildPowerAssembly,
    slots: Sequence[SlotRef],
    *,
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    powers: Mapping[int, AssemblyPower],
    entities: AssemblyEntities,
    archetype_index: int,
    conditions: Conditions,
) -> AssembledPower:
    """One build power's assembled effects, before the cross-power pass.

    Raises:
        ValueError: ``E88`` for an override redirect the power takes; ``E90`` for a pointer to a record
            the inputs do not hold.
        NotImplementedError: ``E89`` for an entity or powerset the export does not hold; ``E91`` for a
            GrantPower gated on a special case.
    """
    inputs = _Inputs(powers, entities, archetype_index)
    power, effects = _assemble(build_power, slots, enh_db, set_db, inputs, conditions)
    return AssembledPower(effects=_reindex(effects), ignore_enh=power.ignore_enh)


def _grant_root(source: AssemblyPower, source_fx: Sequence[Effect], inputs: _Inputs) -> Callable[[int], Effect | None]:
    """``PowerGrantsMap.GetGrantRoot``: the source grant an absorbed position falls under, if any.

    Positions start after the source's database effects and step through each grant's granted effects.
    """
    ranges = []
    start = len(source.effects)
    for fx in source_fx:
        if fx.effect_type != "GrantPower":
            continue
        granted = _record(inputs.powers, fx.n_summon, "granted power", source.full_name) if fx.n_summon >= 0 else None
        count = len(granted.effects) if granted else 0
        ranges.append((start, start + count, fx))
        start += count

    def lookup(offset: int) -> Effect | None:
        position = offset + len(source.effects)
        return next((fx for lo, hi, fx in ranges if lo <= position < hi), None)

    return lookup


def _absorb_global(
    target_fx: list[Effect],
    target: AssemblyPower,
    source_fx: Sequence[Effect],
    source: AssemblyPower,
    inputs: _Inputs,
    mods: dict[str, float],
) -> None:
    """``GBPA_ApplyIncarnateEnhancements`` with effect type GrantPower and no buffed power, editing in place.

    It also enters, in ``mods``, the chance modifier of each id the absorbed effects and their grants carry.
    """
    if not source_fx or not target.enhancements or target.power_set_type not in _SLOTTABLE_SET_TYPES:
        return
    grant_root = _grant_root(source, source_fx, inputs)
    start = len(target_fx)
    for fx in source_fx:
        if (
            fx.effect_class == "Ignored"
            or fx.effect_type in ("Enhancement", "DamageBuff")
            or fx.ignore_ed
            or (
                source.power_type != "GlobalBoost"
                and not (fx.absorbed_effect and fx.absorbed_power_type == "GlobalBoost")
            )
            or (fx.effect_type == "GrantPower" and fx.absorbed_effect)
        ):
            continue
        where = f"{source.full_name} effect {fx.index}"
        owner = (
            _record(inputs.powers, fx.absorbed_power_nid, "power", where)
            if fx.absorbed_effect and fx.absorbed_power_nid > -1
            else source
        )
        if not target.enhancements & owner.enhancements:
            continue
        if fx.effect_type == "GrantPower":
            granted = _record(inputs.powers, fx.n_summon, "granted power", where)
            target_fx.extend(_absorb(granted, fx.duration, inputs.archetype_index, inputs))
            # Mids rewrites every effect of the target here, not only the ones just absorbed.
            for k, t in enumerate(target_fx):
                t = replace(
                    t,
                    to_who="Target",
                    absorbed_effect=True,
                    is_enhancement_effect=fx.is_enhancement_effect,
                    base_probability=f32(t.base_probability * fx.base_probability),
                    ticks=fx.ticks,
                )
                target_fx[k] = _with_probability(t, f32(_probability(t, mods) * _probability(fx, mods)), mods)
        else:
            (absorbed,) = _absorb(source, fx.duration, inputs.archetype_index, inputs, chosen=fx)
            target_fx.append(
                replace(
                    absorbed,
                    to_who="Target",
                    absorbed_effect=True,
                    is_enhancement_effect=fx.is_enhancement_effect,
                    ticks=fx.ticks,
                )
            )
    _settle_chances(target_fx, start, grant_root, source_fx, mods)


def _settle_chances(
    target_fx: list[Effect],
    start: int,
    grant_root: Callable[[int], Effect | None],
    source_fx: Sequence[Effect],
    mods: dict[str, float],
) -> None:
    """Enter the absorbed effects' chance ids in ``mods``, then scale each by the probability of its grant."""
    flags: dict[str, None] = {}
    for k in range(start, len(target_fx)):
        root = grant_root(k - start)
        for fx in (root, target_fx[k]):
            if fx is not None and fx.effect_id.strip():
                flags[fx.effect_id] = None
    for flag in flags:
        if flag not in mods:
            mods[flag] = f32(sum(fx.scale for fx in source_fx if fx.reward == flag))
    for k in range(start, len(target_fx)):
        root = grant_root(k - start)
        # A position no grant covers reads 0, as the float FirstOrDefault in Mids does.
        grant_probability = _probability(root, mods) if root is not None else 0.0
        target_fx[k] = _with_probability(target_fx[k], f32(_probability(target_fx[k], mods) * grant_probability), mods)


def _global_chance_mods(
    order: Sequence[int],
    assembled: Mapping[int, tuple[AssemblyPower, list[Effect]]],
    stat_include: Mapping[int, bool],
    set_bonus_effects: Sequence[Effect],
    mods: dict[str, float],
) -> None:
    """``GenerateModifyEffectsArray``: each reward's first GlobalChanceMod scale, build powers then set bonuses."""
    sources = [fx for i in order if stat_include[i] for fx in assembled[i][1]]
    for fx in (*sources, *set_bonus_effects):
        if fx.effect_type == "GlobalChanceMod" and fx.reward and fx.reward not in mods:
            mods[fx.reward] = fx.scale


def assemble_build(
    build: Mapping[int, BuildPowerAssembly],
    slots: Mapping[int, Sequence[SlotRef]],
    *,
    stat_include: Mapping[int, bool],
    enh_db: Mapping[int, EnhancementRecord],
    set_db: SetBonusDb,
    powers: Mapping[int, AssemblyPower],
    entities: AssemblyEntities,
    archetype_index: int,
    conditions: Conditions,
    set_bonus_effects: Sequence[Effect] = (),
) -> dict[int, AssembledPower]:
    """Every build power's assembled effects, by build index, after the cross-power GrantPower pass.

    Each power, StatInclude or not, absorbs from every other StatInclude power in build order; a source
    already processed as a target carries what it absorbed. ``set_bonus_effects`` are the set-bonus virtual
    power's effects, whose chance modifiers follow the build powers'.

    Raises:
        ValueError: as :func:`assemble_power`.
        NotImplementedError: as :func:`assemble_power`.
    """
    # An effect absorbed from a build power points at it, so the build's own powers are looked up too.
    every_power = ChainMap(dict(powers), {entry.power.nid: entry.power for entry in build.values()})
    inputs = _Inputs(every_power, entities, archetype_index)
    order = sorted(build)
    assembled = {i: _assemble(build[i], slots.get(i, ()), enh_db, set_db, inputs, conditions) for i in order}
    mods: dict[str, float] = {}
    for target in order:
        for source in order:
            if source != target and stat_include[source]:
                (target_power, target_fx), (source_power, source_fx) = assembled[target], assembled[source]
                _absorb_global(target_fx, target_power, source_fx, source_power, inputs, mods)
    _global_chance_mods(order, assembled, stat_include, set_bonus_effects, mods)
    return {
        i: AssembledPower(
            effects=_reindex([replace(fx, probability=_probability(fx, mods)) for fx in effects]),
            ignore_enh=power.ignore_enh,
        )
        for i, (power, effects) in assembled.items()
    }
