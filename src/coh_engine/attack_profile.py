"""Attack classification: one canonical power -> one :class:`~coh_engine.damage_model.AttackProfile`.

The chain planner needs to know, per power, *where* it is delivered from (melee or ranged),
*what area* it covers, and *how* it participates in damage (chain attack, aura, patch, pet
summon, click buff). :func:`classify_attack` is the single verdict (``check-registry.md``
"Classify once"); :func:`category_disagreement` is a cross-check consumer of that verdict,
never a second classifier.

Canonical ``effect_area`` values and their shapes:

- ``SingleTarget`` -> ``SINGLE``
- ``Cone`` -> ``CONE``
- ``AoE`` with ``range > 0`` -> ``TARGETED_AOE``; with ``range == 0`` -> ``PBAOE``
- ``Location`` -> ``LOCATION``
- ``Chain`` (jumping attacks such as Chain Lightning) -> ``TARGETED_AOE``, bounded by ``max_targets``
- anything else (``Self``, ``Map``, ``Touch``: not attack areas) -> ``E36``

Delivery is ``MELEE`` when ``range <= melee_range_ft_max`` (a PBAOE has range 0, so it is
melee), else ``RANGED``.

Effect direction comes from the template table name, not the sign of the scale. Foe damage is a
``*_Dmg`` attribute (not ``Heal_Dmg``) with aspect ``Absolute`` not aimed at ``Self``. Foe -Res is aspect
``Resistance`` on a ``*Res_Dmg`` table that is not a ``Buff`` table, not aimed at the caster; the plain
``Ranged_Res_Dmg`` and ``Melee_Res_Dmg`` tables also carry ally shields, so there the negative scale decides.
A damage buff is aspect ``Strength`` on a ``*Buff_Dmg`` table.

With a ``resolve`` function, effects behind ``Execute_Power`` and ``Create_Entity`` params (power names,
redirects, and the ``power_full_names`` of the ``entity_def``) count as the power's own, one level deep.

Role precedence (first match wins):

1. creates an entity and a boost-set category is a pet category -> ``PET_SUMMON``
2. creates an entity and its linked powers deal foe damage -> ``PATCH``
3. creates an entity and its effects put -Res on foes -> ``SUPPORT`` (a location debuff)
4. creates an entity otherwise -> ``PATCH``
5. foe damage on a ``Toggle`` or ``Auto`` -> ``AURA`` (never activated, so never in the chain)
6. foe damage otherwise -> ``CHAIN``
7. a ``Click`` with foe -Res or a team damage buff -> ``SUPPORT``
8. a ``Click`` with a self damage buff, own or linked -> ``BUFF_CLICK``
9. a ``Toggle`` or ``Auto`` with foe -Res -> ``PERSISTENT_DEBUFF``
10. otherwise ``E37`` (the caller filters the rest; self and team damage-buff toggles land here because
    Mids already counts them)

Effects are searched recursively through ``child_effects``. A power whose ``redirect`` is
non-empty is classified on its own base fields; redirect targets are not followed.

Spec: issue #67 (planner core). Novel to the port: no Mids parity reference.
"""

import re
from collections.abc import Callable, Iterable, Iterator, Mapping
from dataclasses import dataclass
from json import JSONDecodeError, loads
from pathlib import Path
from typing import Any

from coh_engine.damage_model import AttackProfile, Delivery, Role, Shape

type Resolver = Callable[[str], Mapping[str, Any] | None]

_SELF_TARGETS = frozenset({"Self", "SelfAndPets"})
_PET_CATEGORIES = frozenset({"Pet Damage", "Recharge Intensive Pets"})

_SHAPES = {
    "SingleTarget": Shape.SINGLE,
    "Cone": Shape.CONE,
    "Location": Shape.LOCATION,
    "Chain": Shape.TARGETED_AOE,
}

# Set categories that name where the set's attacks are delivered from.
_MELEE_CATEGORIES = frozenset({"Melee Damage", "Melee AoE Damage"})
_RANGED_CATEGORIES = frozenset({"Ranged Damage", "Ranged AoE Damage", "Sniper Attacks"})


def read_json(path: Path, code: str) -> Any:
    """Parse a JSON file (BOM tolerated); a corrupt file raises ``ValueError`` ``<code>`` naming the path."""
    try:
        return loads(path.read_text(encoding="utf-8-sig"))
    except JSONDecodeError as error:
        raise ValueError(
            f"{code}: {path} is not valid JSON ({error}); re-run tools/mids-dump or restore the file"
        ) from error


def canonical_power_path(powers_root: Path, full_name: str) -> Path:
    """Return the canonical file path for a power full name. THE naming rule; every tool must agree.

    Each dot segment is lower-cased and becomes a directory (the last a ``.json`` file). ':' in a
    segment becomes '_' because NTFS reads 'name:stream' as an alternate data stream, which left
    empty placeholder files. ``scripts/lane-b/run.ps1`` and ``scripts/check-overrides.ps1`` apply
    the same mapping.
    """
    segments = [s.lower().replace(":", "_") for s in full_name.split(".")]
    return powers_root.joinpath(*segments[:-1], f"{segments[-1]}.json")


def load_canonical_powers(powers_root: Path, full_names: Iterable[str]) -> Mapping[str, Mapping[str, Any]]:
    """Load canonical power JSON for each full name.

    The path comes from :func:`canonical_power_path`; only names whose derived file is
    missing or empty (0-byte NTFS-stream placeholders) fall back to a file-name search of the tree.

    Raises:
        ValueError: ``E35`` naming every full name with no non-empty canonical file; ``E38`` for a
            corrupt file when no other candidate matches.
    """
    found: dict[str, Mapping[str, Any]] = {}
    missing: list[str] = []
    for name in full_names:
        derived = canonical_power_path(powers_root, name)
        candidates = [derived]
        if not derived.is_file() or derived.stat().st_size == 0:
            candidates = [p for p in powers_root.rglob(derived.name) if p.stat().st_size > 0]
        data = None
        corrupt: ValueError | None = None
        for p in candidates:
            try:
                parsed = read_json(p, "E38")
            except ValueError as error:
                corrupt = error  # a corrupt same-named file is skipped if another candidate matches
                continue
            if parsed.get("full_name") == name:
                data = parsed
                break
        if data is None and corrupt is not None:
            raise corrupt
        if data is None:
            missing.append(name)
        else:
            found[name] = data
    if missing:
        raise ValueError(
            f"E35: no canonical power file under {powers_root} for {missing}; "
            "check the full_name spelling or restore the file (0-byte files are skipped)"
        )
    return found


def _templates(effects: Iterable[Mapping[str, Any]]) -> Iterator[Mapping[str, Any]]:
    for effect in effects:
        yield from effect.get("templates", ())
        yield from _templates(effect.get("child_effects", ()))


def _shape(power: Mapping[str, Any]) -> Shape:
    area = power["effect_area"]
    if area == "AoE":
        return Shape.TARGETED_AOE if power["range"] > 0 else Shape.PBAOE
    if area not in _SHAPES:
        raise ValueError(
            f"E36: {power['full_name']} has effect_area {area!r}, expected one of "
            f"{['AoE', *_SHAPES]}; add the value to attack_profile._SHAPES or filter the power"
        )
    return _SHAPES[area]


@dataclass(frozen=True, slots=True)
class _Facts:
    """What a set of templates does, folded once for :func:`_role`."""

    creates_entity: bool = False
    foe_damage: bool = False
    foe_res: bool = False
    self_buff: bool = False
    team_buff: bool = False


def _foe_res(attrib: str, aspect: str, target: str, table: str, scale: float) -> bool:
    """Direction comes from the table name; the plain ``*_Res_Dmg`` table also carries ally shields, so its sign decides."""
    return (
        attrib.endswith("_Dmg")
        and attrib != "Heal_Dmg"
        and aspect == "Resistance"
        and target not in _SELF_TARGETS
        and "Res_Dmg" in table
        and "Buff" not in table
        and ("Debuff" in table or scale < 0)
    )


def _facts(templates: Iterable[Mapping[str, Any]], *, linked: bool = False) -> _Facts:
    """Fold templates; in a linked power every damage buff is the caster's (it was executed on the caster)."""
    found = dict.fromkeys(_Facts.__slots__, False)
    for t in templates:
        table, aspect, target = t.get("table", ""), t["aspect"], t["target"]
        for a in t["attribs"]:
            dmg = a.endswith("_Dmg") and a != "Heal_Dmg"
            found["creates_entity"] |= a == "Create_Entity"
            found["foe_damage"] |= dmg and aspect == "Absolute" and target != "Self"
            found["foe_res"] |= _foe_res(a, aspect, target, table, t.get("scale", 0.0))
            if dmg and aspect == "Strength" and "Buff_Dmg" in table:
                found["self_buff" if linked or target in _SELF_TARGETS else "team_buff"] = True
    return _Facts(**found)


def _linked_powers(templates: Iterable[Mapping[str, Any]], resolve: Resolver) -> Iterator[Mapping[str, Any]]:
    """The powers one level behind ``Execute_Power`` / ``Create_Entity``; unknown names are skipped."""
    names: list[str] = []
    for t in templates:
        params = t.get("params") or {}
        if "Execute_Power" in t["attribs"]:
            names += params.get("power_names") or []
        if "Create_Entity" in t["attribs"]:
            names += (params.get("power_names") or []) + (params.get("redirects") or [])
            if entity := params.get("entity_def"):
                names += ((resolve(entity) or {}).get("defaults") or {}).get("power_full_names") or []
    for name in names:
        if (linked := resolve(name)) is not None:
            yield linked


def _linked_templates(templates: Iterable[Mapping[str, Any]], resolve: Resolver) -> Iterator[Mapping[str, Any]]:
    """Templates of the powers one level behind ``Execute_Power`` / ``Create_Entity``."""
    for linked in _linked_powers(templates, resolve):
        yield from _templates(linked["effects"])


# PvE combat is against critters, so an effect group gated only on this test always applies.
_PVE_ALWAYS = "target>enttype eq 'critter'"


def applies_in_pve(effect: Mapping[str, Any]) -> bool:
    """Whether an effect group fires in PvE: not PvP-only, and unconditional or gated only on the critter test.

    This is THE PvE rule; damage and debuff magnitudes read from canonical templates use it.
    """
    if effect.get("is_pvp") == "PVP_ONLY":
        return False
    return (effect.get("requires_expression") or "").strip() in ("", _PVE_ALWAYS)


def _pve_templates(effects: Iterable[Mapping[str, Any]]) -> Iterator[Mapping[str, Any]]:
    for effect in effects:
        if applies_in_pve(effect):
            yield from effect.get("templates", ())
        yield from _pve_templates(effect.get("child_effects", ()))


def foe_res_debuff_templates(power: Mapping[str, Any], resolve: Resolver | None) -> list[Mapping[str, Any]]:
    """The PvE-applicable templates that put -Res on foes, from the power itself and its linked powers."""
    own = list(_templates(power["effects"]))
    sources = [power, *(_linked_powers(own, resolve) if resolve else ())]
    return [
        t
        for p in sources
        for t in _pve_templates(p["effects"])
        if any(_foe_res(a, t["aspect"], t["target"], t.get("table", ""), t.get("scale", 0.0)) for a in t["attribs"])
    ]


_NO_MODE_REDIRECT = re.compile(r"Always|!Source\.Mode\?\(\w+\)")


def _default_redirect(power: Mapping[str, Any], resolve: Resolver) -> Mapping[str, Any] | None:
    """The power an effectless power redirects to with no mode active; ``None`` when no condition is decidable."""
    for redirect in power.get("redirect") or ():
        if _NO_MODE_REDIRECT.fullmatch((redirect.get("condition_expression") or "Always").strip()):
            return resolve(redirect["name"])
    return None


def _role(power: Mapping[str, Any], resolve: Resolver | None) -> Role:
    if not power["effects"] and resolve is not None and (target := _default_redirect(power, resolve)) is not None:
        power = {**power, "effects": target["effects"]}
    templates = list(_templates(power["effects"]))
    own = _facts(templates)
    link = _facts(_linked_templates(templates, resolve) if resolve else (), linked=True)
    foe_res = own.foe_res or link.foe_res
    persistent = power["type"] in ("Toggle", "Auto")
    # A click that hits on activation is an attack; any entity it also spawns (Chain Induction's jump) is a side effect.
    if power["type"] == "Click" and own.foe_damage and not _PET_CATEGORIES & set(power["allowed_boostset_cats"]):
        return Role.CHAIN
    if own.creates_entity:
        if _PET_CATEGORIES & set(power["allowed_boostset_cats"]):
            return Role.PET_SUMMON
        return Role.SUPPORT if foe_res and not link.foe_damage else Role.PATCH
    if own.foe_damage or link.foe_damage:
        return Role.AURA if persistent else Role.CHAIN
    if power["type"] == "Click":
        if foe_res or own.team_buff:
            return Role.SUPPORT
        if own.self_buff or link.self_buff:
            return Role.BUFF_CLICK
    elif persistent and foe_res:
        return Role.PERSISTENT_DEBUFF
    raise ValueError(
        f"E37: {power['full_name']} (type {power['type']}) has no entity creation, foe damage, foe -Res or "
        "click damage buff; filter it out before classifying (Mids already counts self and team damage-buff toggles)"
    )


def canonical_resolver(powers_root: Path) -> Resolver:
    """Return a cached lookup from name to canonical mapping that gives ``None`` instead of raising.

    A dotted name is a power full name; an undotted name is an entity definition, read from
    ``entities/<lower-cased name>.json`` beside ``powers_root``.
    """
    cache: dict[str, Mapping[str, Any] | None] = {}

    def load(name: str) -> Mapping[str, Any] | None:
        if "." in name:
            path = canonical_power_path(powers_root, name)
            return load_canonical_powers(powers_root, [name])[name] if path.stat().st_size > 0 else None
        path = powers_root.parent / "entities" / f"{name.lower()}.json"
        return read_json(path, "E38") if path.stat().st_size > 0 else None

    def resolve(name: str) -> Mapping[str, Any] | None:
        if name not in cache:
            try:
                cache[name] = load(name)
            except ValueError, OSError:
                cache[name] = None
        return cache[name]

    return resolve


def classify_attack(
    power: Mapping[str, Any], *, melee_range_ft_max: float = 20.0, resolve: Resolver | None = None
) -> AttackProfile:
    """Classify a canonical power once: delivery, shape, role and area figures.

    ``resolve`` (:func:`canonical_resolver`) lets the role follow ``Execute_Power`` and ``Create_Entity``
    links one level, counting the linked powers' effects as this power's.

    Raises:
        ValueError: ``E36`` for an unmapped ``effect_area``; ``E37`` for a power that is
            not damaging, a damage buff or a -Res debuff.
    """
    shape = _shape(power)
    range_ft = float(power["range"])
    return AttackProfile(
        full_name=power["full_name"],
        delivery=Delivery.MELEE if range_ft <= melee_range_ft_max else Delivery.RANGED,
        shape=shape,
        role=_role(power, resolve),
        range_ft=range_ft,
        radius_ft=float(power["radius"]),
        arc_rad=float(power["arc"]),
        max_targets=int(power["max_targets_hit"]),
        set_categories=tuple(power["allowed_boostset_cats"]),
        modes_required=tuple(power.get("modes_required") or ()),
    )


def category_disagreement(profile: AttackProfile) -> str | None:
    """Describe a conflict between the verdict's delivery and the power's set categories, else ``None``.

    Only melee/ranged damage categories vote; universal and archetype categories are silent.
    A power with categories of both kinds never disagrees (hybrid sets exist).
    """
    cats = set(profile.set_categories)
    melee, ranged = cats & _MELEE_CATEGORIES, cats & _RANGED_CATEGORIES
    if profile.delivery is Delivery.MELEE and ranged and not melee:
        return (
            f"{profile.full_name} is classified MELEE (range {profile.range_ft:g} ft) but its set categories "
            f"{sorted(ranged)} are ranged; check the melee range threshold or the power's range"
        )
    if profile.delivery is Delivery.RANGED and melee and not ranged:
        return (
            f"{profile.full_name} is classified RANGED (range {profile.range_ft:g} ft) but its set categories "
            f"{sorted(melee)} are melee; check the melee range threshold or the power's range"
        )
    return None
