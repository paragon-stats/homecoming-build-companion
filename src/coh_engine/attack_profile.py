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

Role precedence (first match wins):

1. creates an entity (``Create_Entity``) and a boost-set category is a pet category -> ``PET_SUMMON``
2. creates an entity otherwise (e.g. a location damage patch) -> ``PATCH``
3. type ``Toggle`` or ``Auto`` with foe damage -> ``AURA`` (never activated, so never in the chain)
4. foe damage (any ``*_Dmg`` attribute with aspect ``Absolute`` not aimed at ``Self``) -> ``CHAIN``
5. type ``Click`` with a ``*_Dmg`` attribute of aspect ``Strength`` aimed at ``Self`` -> ``BUFF_CLICK``
6. otherwise ``E37`` (the caller filters non-damaging, non-buff powers)

Effects are searched recursively through ``child_effects``. A power whose ``redirect`` is
non-empty is classified on its own base fields; redirect targets are not followed.

Spec: issue #67 (planner core). Novel to the port: no Mids parity reference.
"""

from collections.abc import Iterable, Iterator, Mapping
from json import JSONDecodeError, loads
from pathlib import Path
from typing import Any

from coh_engine.damage_model import AttackProfile, Delivery, Role, Shape

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


def _role(power: Mapping[str, Any]) -> Role:
    templates = list(_templates(power["effects"]))
    attribs = [(a, t["aspect"], t["target"]) for t in templates for a in t["attribs"]]
    if any(a == "Create_Entity" for a, _, _ in attribs):
        return Role.PET_SUMMON if _PET_CATEGORIES & set(power["allowed_boostset_cats"]) else Role.PATCH
    if any(a.endswith("_Dmg") and aspect == "Absolute" and target != "Self" for a, aspect, target in attribs):
        return Role.AURA if power["type"] in ("Toggle", "Auto") else Role.CHAIN
    if power["type"] == "Click" and any(
        a.endswith("_Dmg") and aspect == "Strength" and target == "Self" for a, aspect, target in attribs
    ):
        return Role.BUFF_CLICK
    raise ValueError(
        f"E37: {power['full_name']} (type {power['type']}) has no entity creation, foe damage or "
        "self damage buff; filter non-damaging powers before classifying"
    )


def classify_attack(power: Mapping[str, Any], *, melee_range_ft_max: float = 20.0) -> AttackProfile:
    """Classify a canonical power once: delivery, shape, role and area figures.

    Raises:
        ValueError: ``E36`` for an unmapped ``effect_area``; ``E37`` for a power that is
            neither damaging nor a damage buff.
    """
    shape = _shape(power)
    range_ft = float(power["range"])
    return AttackProfile(
        full_name=power["full_name"],
        delivery=Delivery.MELEE if range_ft <= melee_range_ft_max else Delivery.RANGED,
        shape=shape,
        role=_role(power),
        range_ft=range_ft,
        radius_ft=float(power["radius"]),
        arc_rad=float(power["arc"]),
        max_targets=int(power["max_targets_hit"]),
        set_categories=tuple(power["allowed_boostset_cats"]),
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
