"""Pet damage: summon powers -> :class:`~coh_engine.damage_model.PetSummon` -> the ``pet`` damage source.

Issue #68. **Novel to the port — no Mids parity reference**; pet AI is not modelled, so pet DPS is
approximate: a pet is assumed to run its own best attack chain on the target for as long as it lives.

Where the canonical data keeps each figure:

- Entities: a summon's ``Create_Entity`` templates (found recursively under ``effects``) name the
    entity in ``params.entity_def`` (file ``entities/<entity_def lower-cased>.json``). The number of
    templates naming an entity is its pet count (Spiderlings: three delayed templates). Effect groups
    marked PVP-only are skipped.
- Duration: the template's ``duration`` text, ``"240 seconds"`` for a timed pet and ``"UntilKilled"``
    for a permanent one (duration ``inf``).
- Pet level: the template's ``table``. A ``*_LevelminusN`` table maps the caster's level row to the pet
    level (row ``L-1`` holds ``L-N``, floored at 1); a ``*_Ones`` table holds no level, so the pet is
    taken at the caster's level (approximate; verify in MidsReborn).
- Pet class: the entity's ``defaults.character_class_name``. ``minion_pets`` maps to ``Class_Minion_Pets``;
    ``henchman_minion`` has no such class and maps to the token-reversed ``Class_Minion_Henchman``,
    whose Mids table reads melee 361 at level 50 against 56 for pets (henchman DPS unverified).
- Attacks: the entity's ``defaults.power_full_names`` that are clicks with foe damage. Effect groups with a
    ``requires_expression`` (conditional variants) or a procs-per-minute are skipped; the unconditional
    ``Fallback`` group carries the base damage.
- Damage per activation: ``|table value| * scale * magnitude * chance`` per damage template, with the
    table read for the pet's class at row ``pet_level - 1`` (``attribmod.get_modifier``), times the number of
    damage-over-time ticks, ``floor(duration / application_period) + 1`` (first tick on application;
    approximate; verify in MidsReborn), times ``1 + enhancement``.

The pet damage enhancement is not computed (the engine has no Damage aspect handler yet); it is the
``damage_enhancement`` mapping per summon, else :data:`DEFAULT_PET_DAMAGE_ENHANCEMENT`.
"""

import re
from collections.abc import Iterator, Mapping, Sequence
from dataclasses import dataclass
from math import floor, inf
from pathlib import Path
from typing import Any

from coh_engine.archetypes import ArchetypeDb
from coh_engine.attack_chain import arcana_cast_time, plan_attack_chain
from coh_engine.attack_profile import applies_in_pve, classify_attack, load_canonical_powers, read_json
from coh_engine.attribmod import AttribMods, get_modifier
from coh_engine.damage_model import (
    SOURCE_PET,
    AttackProfile,
    ChainAttack,
    DamageContext,
    EnduranceBudget,
    PetSummon,
    Role,
    SourceResult,
    Stance,
    damage_source,
)
from coh_engine.maths import f32

# approximate; verify in MidsReborn: three slotted pet-damage enhancements at the ED ceiling (+95%).
DEFAULT_PET_DAMAGE_ENHANCEMENT = 0.95

_UNLIMITED = EnduranceBudget(max_end=1.0e9, recovery_per_sec=0.0, toggle_drain_per_sec=0.0)
_APPROXIMATION = "pet AI not modelled: each pet runs its best chain; enhancement approximate"


@dataclass(frozen=True, slots=True)
class _Entity:
    name: str
    count: int
    duration: float
    table: str


def _effects(effects: Sequence[Mapping[str, Any]]) -> Iterator[tuple[Mapping[str, Any], Mapping[str, Any]]]:
    """Every (effect group, template) pair, recursing through child effects; PVP-only groups are skipped."""
    for effect in effects:
        if effect.get("is_pvp") != "PVP_ONLY":
            for template in effect.get("templates", ()):
                yield effect, template
        yield from _effects(effect.get("child_effects", ()))


def _duration(text: str, power: str) -> float:
    if text == "UntilKilled":
        return inf
    number, _, unit = text.partition(" ")
    try:
        if unit == "seconds":
            return float(number)
    except ValueError:
        pass
    raise ValueError(
        f"E62: {power} Create_Entity duration is {text!r}, expected 'N seconds' or 'UntilKilled'; extend pet_damage._duration"
    )


_LEVEL_GATE = re.compile(r"source>combatlevel\s*>\s*(\d+)")


def _spawn_key(condition: str, caster_level: int) -> str | None:
    """How a Create_Entity condition spawns for this caster.

    ``""``: always (no condition, or a level gate the caster meets); ``None``: never (an unmet level
    gate); otherwise the condition itself, an alternate group (e.g. a costume chosen by @CustomFX).
    """
    text = condition.strip()
    gate = _LEVEL_GATE.fullmatch(text)
    if gate is not None:
        return "" if caster_level > int(gate.group(1)) else None
    return text


def _summoned(power: Mapping[str, Any], caster_level: int) -> list[_Entity]:
    found: dict[str, list[Any]] = {}
    # Unconditional and met level-gated templates all spawn (Mercenaries' 2nd Soldier at level 6).
    # Other conditions (costume variants chosen by @CustomFX) are alternates: only the largest group spawns.
    groups: dict[str, dict[str, int]] = {}
    for effect, template in _effects(power["effects"]):
        if "Create_Entity" not in template["attribs"]:
            continue
        name = (template.get("params") or {}).get("entity_def")
        if not name:
            raise ValueError(
                f"E62: {power['full_name']} has a Create_Entity template with no params.entity_def; "
                "check the canonical power file"
            )
        found.setdefault(name, [0, _duration(template["duration"], power["full_name"]), template["table"]])
        by_condition = groups.setdefault(name, {})
        key = _spawn_key(effect.get("requires_expression", ""), caster_level)
        if key is not None:
            by_condition[key] = by_condition.get(key, 0) + 1
    if not found:
        raise ValueError(f"E62: {power['full_name']} has no Create_Entity template; pass only pet summon powers")
    for name, row in found.items():
        alternates = [n for c, n in groups[name].items() if c]
        row[0] = groups[name].get("", 0) + max(alternates, default=0)
    # An entity whose every template is gated above the caster's level never spawns.
    return [_Entity(name, *row) for name, row in found.items() if row[0] > 0]


def _class_index(entity: Mapping[str, Any], classes: ArchetypeDb) -> int:
    raw = entity["defaults"]["character_class_name"]
    tokens = raw.split("_")
    for candidate in (f"Class_{raw}", "Class_" + "_".join(reversed(tokens))):
        index = classes.nid_from_uid_class(candidate)
        if index >= 0:
            return index
    raise ValueError(
        f"E61: entity {entity['name']} has class {raw!r} with no Class_{raw} or reversed-token class in the "
        "archetype table; add the class to the archetypes dump"
    )


def modifier_table(mods: AttribMods, name: str, power: str) -> int:
    index = mods.nid_from_uid_attribmod(name)
    if index < 0:
        raise ValueError(
            f"E61: {power} uses modifier table {name!r}, which the AttribMod table lacks; reload AttribMod.json"
        )
    return index


def _pet_level(
    table: str, caster_level: int, mods: AttribMods, classes: ArchetypeDb, pet_class: int, power: str
) -> int:
    if table.casefold().endswith("ones"):
        return caster_level
    level = int(get_modifier(mods, classes.classes, pet_class, modifier_table(mods, table, power), caster_level - 1))
    if level < 1:
        raise ValueError(
            f"E61: table {table} gives pet level {level} at caster level {caster_level}; pass a caster_level in 1-50"
        )
    return level


def _damage_per_activation(
    power: Mapping[str, Any], mods: AttribMods, classes: ArchetypeDb, pet_class: int, level: int
) -> float:
    total = 0.0
    for effect, template in _effects(power["effects"]):
        foe_damage = (
            template["aspect"] == "Absolute"
            and template["target"] != "Self"
            and any(a.endswith("_Dmg") and not a.startswith("Heal") for a in template["attribs"])
        )
        if not foe_damage or not applies_in_pve(effect) or effect["ppm"] > 0:
            continue
        value = abs(
            get_modifier(
                mods, classes.classes, pet_class, modifier_table(mods, template["table"], power["full_name"]), level - 1
            )
        )
        period, span = template["application_period"], _seconds(template["duration"], power["full_name"])
        ticks = floor(span / period) + 1 if period > 0 and span > 0 else 1
        total += value * template["scale"] * template["magnitude"] * effect["chance"] * ticks
    return f32(total)


def _seconds(text: str, power: str) -> float:
    if text in ("", "UntilKilled"):  # no finite span: a single application
        return 0.0
    number, _, unit = text.partition(" ")
    try:
        if unit == "seconds":
            return float(number)
    except ValueError:
        pass
    raise ValueError(
        f"E62: {power} damage template duration is {text!r}, expected 'N seconds'; extend pet_damage._seconds"
    )


def _attack_profile(power: Mapping[str, Any]) -> AttackProfile | None:
    """The pet power's profile when it is a chain attack; the classifier is the one decision (E37 means none)."""
    if power["type"] != "Click":
        return None
    try:
        profile = classify_attack(power)
    except ValueError as error:
        if str(error).startswith("E37"):
            return None
        raise
    return profile if profile.role is Role.CHAIN else None


def build_pet_summons(
    summon_powers: Sequence[ChainAttack],
    *,
    canonical_root: Path,
    attribmod: AttribMods,
    classes: ArchetypeDb,
    caster_level: int = 50,
    damage_enhancement: Mapping[str, float] | None = None,
) -> tuple[PetSummon, ...]:
    """Resolve each pet summon power into one :class:`PetSummon` per summoned entity.

    ``canonical_root`` is ``data/canonical`` (holding ``powers/`` and ``entities/``). When a summon
    creates several entities (Mercenaries: Soldiers and a Medic) the first carries the summon's cast and
    end cost and later ones carry none, so the cast time is not charged twice; every one keeps the recharge.

    Raises:
        ValueError: ``E60`` for a missing entity or pet power file; ``E61`` for an unknown modifier table
            or class, or a pet level below 1; ``E62`` for a summon with no usable Create_Entity data;
            ``E63`` for a corrupt JSON file; ``E64`` for a power whose role is not ``PET_SUMMON``.
    """
    overrides = damage_enhancement or {}
    result: list[PetSummon] = []
    for summon in summon_powers:
        name = summon.full_name
        if summon.profile.role is not Role.PET_SUMMON:
            raise ValueError(
                f"E64: {name} has role {summon.profile.role.value}, expected pet_summon; pass summon powers only"
            )
        enhancement = overrides.get(name, DEFAULT_PET_DAMAGE_ENHANCEMENT)
        power = load_canonical_powers(canonical_root / "powers", [name])[name]
        for position, entity in enumerate(_summoned(power, caster_level)):
            path = canonical_root / "entities" / f"{entity.name.lower()}.json"
            if not path.is_file() or path.stat().st_size == 0:
                raise ValueError(
                    f"E60: entity {entity.name} (summoned by {name}) has no file {path}; restore the canonical entity"
                )
            data = read_json(path, "E63")
            pet_class = _class_index(data, classes)
            level = _pet_level(entity.table, caster_level, attribmod, classes, pet_class, name)
            names = data["defaults"]["power_full_names"]
            try:
                powers = load_canonical_powers(canonical_root / "powers", names)
            except ValueError as error:
                raise ValueError(
                    f"E60: pet powers of {entity.name} missing ({error}); restore the canonical files"
                ) from error
            attacks = tuple(
                ChainAttack(
                    full_name=p["full_name"],
                    damage=f32(_damage_per_activation(p, attribmod, classes, pet_class, level) * (1.0 + enhancement)),
                    cast_time=p["activation_time"],
                    recharge_time=p["recharge_time"],
                    end_cost=0.0,
                    profile=profile,
                    activate_period=p["activate_period"],
                )
                for p in powers.values()
                if (profile := _attack_profile(p)) is not None
            )
            pet_name = ((data.get("levels") or [{}])[0].get("display_names") or [entity.name])[0]
            result.append(
                PetSummon(
                    summon_name=name,
                    pet_name=pet_name,
                    pet_attacks=attacks,
                    pet_count=entity.count,
                    duration=entity.duration,
                    summon_cast_time=summon.cast_time if position == 0 else 0.0,
                    summon_recharge=summon.recharge_time,
                    summon_end_cost=summon.end_cost if position == 0 else 0.0,
                    damage_buff_baseline=f32(1.0 + enhancement),
                    label=f"{entity.name} level {level}" + ("" if attacks else " (no damaging attacks found)"),
                )
            )
    return tuple(result)


@damage_source()
def pet_source(ctx: DamageContext) -> SourceResult | None:
    """Pets' own best chains, scaled by count and by how much of the time the pets are alive."""
    if not ctx.pets:
        return SourceResult(source=SOURCE_PET, dps=0.0, label="no pets summoned")
    detail: list[tuple[str, float]] = []
    weights: list[tuple[float, float]] = []
    uptimes: list[str] = []
    share = end = 0.0
    for pet in ctx.pets:
        plan = plan_attack_chain(
            pet.pet_attacks,
            stance=Stance.MELEE,
            movement=ctx.movement,
            scenario=ctx.scenario,
            budget=_UNLIMITED,
        )
        single = plan.best_overall.dps if plan.best_overall else 0.0
        arcana = arcana_cast_time(pet.summon_cast_time) if pet.summon_cast_time > 0 else 0.0
        cycle = f32(arcana + pet.summon_recharge)
        timed = pet.duration != inf
        uptime = min(1.0, f32(pet.duration / cycle)) if timed and cycle > 0 else 1.0
        dps = f32(f32(single * pet.pet_count) * uptime)
        detail.append((pet.pet_name, dps))
        weights.append((dps, pet.damage_buff_baseline))
        uptimes.append(f"{pet.pet_name} x{pet.pet_count} uptime {uptime:.0%}" + ("" if timed else " (permanent)"))
        if timed and cycle > 0:
            share = f32(share + f32(arcana / cycle))
            end = f32(end + f32(pet.summon_end_cost / cycle))
    total = f32(sum(d for _, d in detail))
    baseline = f32(sum(d * b for d, b in weights) / total) if total > 0 else 1.0
    return SourceResult(
        source=SOURCE_PET,
        dps=total,
        timeline_share=share,
        end_per_sec=end,
        detail=tuple(detail),
        label=f"{_APPROXIMATION}; {', '.join(uptimes)}",
        damage_buff_baseline=baseline,
    )
