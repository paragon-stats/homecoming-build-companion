"""Reader for the ``.mbd`` modern build format (plain indented JSON).

Mirrors ``CharacterBuildData.LoadBuild`` (``Core/BuildFile/CharacterBuildData.cs``)
and ``EnhancementDataConverter`` (``Core/BuildFile/EnhancementDataConverter.cs``).

Offsets, reproduced exactly (spec § data-and-build-formats, B.4 + gotchas):
- Power ``Level`` and slot ``Level`` are stored **+1** and get **-1** on load, so
    the model holds MidsReborn's internal 0-based level (character level = level + 1).
- ``LastPower`` is assigned **raw** on ``.mbd`` load — no ``-1`` (CharacterBuildData.cs:284),
    unlike the ``.mxd`` path.
- The top-level character ``Level`` is a raw integer, no offset.

An ``Enhancement`` object carries five keys; ``EnhancementDataConverter`` also
accepts a legacy ``Enhancement`` key as the ``Uid`` and a JSON ``null`` as an
empty slot.
"""

import json
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.levels import LevelSchedule


@dataclass(frozen=True, slots=True)
class MetaData:
    """The ``BuiltWith`` block: app + database provenance."""

    app: str
    version: str
    database: str
    database_version: str


@dataclass(frozen=True, slots=True)
class Enhancement:
    """A slotted enhancement (``EnhancementData``)."""

    uid: str
    grade: str = "None"
    io_level: int = 1
    relative_level: str = "Even"
    obtained: bool = False


@dataclass(frozen=True, slots=True)
class Slot:
    """One enhancement slot on a power. ``level`` is 0-based (character level - 1)."""

    level: int
    is_inherent: bool
    enhancement: Enhancement | None
    flipped_enhancement: Enhancement | None


@dataclass(frozen=True, slots=True)
class SubPower:
    """A sub-power entry (e.g. a toggle's granted variant)."""

    power_name: str
    stat_include: bool


@dataclass(frozen=True, slots=True)
class PowerEntry:
    """A picked power. ``level`` is 0-based (character level - 1)."""

    power_name: str
    level: int
    stat_include: bool
    proc_include: bool
    variable_value: int
    inherent_slots_used: int
    sub_power_entries: tuple[SubPower, ...]
    slot_entries: tuple[Slot, ...]


@dataclass(frozen=True, slots=True)
class Build:
    """A parsed ``.mbd`` build."""

    meta: MetaData
    level: int
    class_name: str
    origin: str
    alignment: str
    name: str
    comment: str
    power_sets: tuple[str, ...]
    last_power: int
    power_entries: tuple[PowerEntry, ...] = field(default_factory=tuple)


def _parse_enhancement(obj: Any) -> Enhancement | None:
    """Parse an ``EnhancementData`` object, or ``None`` for a JSON null/empty slot."""
    if obj is None:
        return None
    # Legacy converter fallback: an "Enhancement" key stands in for "Uid".
    uid = obj.get("Uid")
    if uid is None:
        uid = obj.get("Enhancement")
    if uid is None:
        return None
    return Enhancement(
        uid=uid,
        grade=obj.get("Grade", "None"),
        io_level=obj.get("IoLevel", 1),
        relative_level=obj.get("RelativeLevel", "Even"),
        obtained=obj.get("Obtained", False),
    )


def _parse_slot(obj: dict[str, Any]) -> Slot:
    return Slot(
        level=obj["Level"] - 1,  # -1 offset
        is_inherent=obj.get("IsInherent", False),
        enhancement=_parse_enhancement(obj.get("Enhancement")),
        flipped_enhancement=_parse_enhancement(obj.get("FlippedEnhancement")),
    )


def _parse_power_entry(obj: dict[str, Any]) -> PowerEntry:
    power_name = obj["PowerName"]
    # Mids applies the -1 only to a real (resolved) power; an empty placeholder
    # entry keeps its stored level (Mids substitutes Levels_MainPowers, a DB value
    # not available at the parse layer — deferred).
    level = obj["Level"] - 1 if power_name else obj["Level"]
    return PowerEntry(
        power_name=power_name,
        level=level,
        stat_include=obj.get("StatInclude", False),
        proc_include=obj.get("ProcInclude", False),
        variable_value=obj.get("VariableValue", 0),
        inherent_slots_used=obj.get("InherentSlotsUsed", 0),
        sub_power_entries=tuple(
            SubPower(power_name=s["PowerName"], stat_include=s.get("StatInclude", False))
            for s in obj.get("SubPowerEntries", [])
        ),
        slot_entries=tuple(_parse_slot(s) for s in obj.get("SlotEntries", [])),
    )


def parse_mbd(data: dict[str, Any]) -> Build:
    """Build a :class:`Build` from a decoded ``.mbd`` JSON object.

    Raises:
        ValueError: if the required ``BuiltWith`` block is missing.
    """
    meta_obj = data.get("BuiltWith")
    if meta_obj is None:
        raise ValueError("mbd build is missing the required BuiltWith block")
    meta = MetaData(
        app=meta_obj.get("App", ""),
        version=meta_obj.get("Version", ""),
        database=meta_obj.get("Database", ""),
        database_version=meta_obj.get("DatabaseVersion", ""),
    )
    return Build(
        meta=meta,
        level=int(data["Level"]),
        class_name=data["Class"],
        origin=data.get("Origin", ""),
        alignment=data.get("Alignment", ""),
        name=data.get("Name", ""),
        comment=data.get("Comment", ""),
        power_sets=tuple(data.get("PowerSets", [])),
        last_power=data.get("LastPower", 0),  # raw, no -1 offset
        power_entries=tuple(_parse_power_entry(p) for p in data.get("PowerEntries", [])),
    )


def load_mbd(path: Path | str) -> Build:
    """Read and parse a ``.mbd`` file from disk.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
        ValueError: if the JSON is invalid or the ``BuiltWith`` block is missing.
    """
    with open(path, encoding="utf-8") as stream:
        data = json.load(stream)
    return parse_mbd(data)


def _dump_enhancement(enh: Enhancement | None) -> dict[str, Any] | None:
    if enh is None:
        return None
    return {
        "Uid": enh.uid,
        "Grade": enh.grade,
        "IoLevel": enh.io_level,
        "RelativeLevel": enh.relative_level,
        "Obtained": enh.obtained,
    }


def _dump_entry(entry: PowerEntry) -> dict[str, Any]:
    return {
        "PowerName": entry.power_name,
        "Level": entry.level + 1 if entry.power_name else entry.level,  # the inverse of _parse_power_entry
        "StatInclude": entry.stat_include,
        "ProcInclude": entry.proc_include,
        "VariableValue": entry.variable_value,
        "InherentSlotsUsed": entry.inherent_slots_used,
        "SubPowerEntries": [
            {"PowerName": s.power_name, "StatInclude": s.stat_include} for s in entry.sub_power_entries
        ],
        "SlotEntries": [
            {
                "Level": s.level + 1,
                "IsInherent": s.is_inherent,
                "Enhancement": _dump_enhancement(s.enhancement),
                "FlippedEnhancement": _dump_enhancement(s.flipped_enhancement),
            }
            for s in entry.slot_entries
        ],
    }


def dump_mbd(build: Build) -> dict[str, Any]:
    """The ``.mbd`` JSON object for a parsed :class:`Build`: the inverse of :func:`parse_mbd`."""
    return {
        "BuiltWith": {
            "App": build.meta.app,
            "Version": build.meta.version,
            "Database": build.meta.database,
            "DatabaseVersion": build.meta.database_version,
        },
        "Level": str(build.level),
        "Class": build.class_name,
        "Origin": build.origin,
        "Alignment": build.alignment,
        "Name": build.name,
        "Comment": build.comment,
        "PowerSets": list(build.power_sets),
        "LastPower": build.last_power,
        "PowerEntries": [_dump_entry(e) for e in build.power_entries],
    }


# --- writing: a build walked level by level, as the game grants it ----------------------

HOMECOMING = MetaData(app="Mids Reborn", version="3.8.6.0", database="Homecoming", database_version="2026.1.1242")
_EXEMPLAR_LEVEL = 15  # .claude/rules/exemplar-10.md: essentials hold two added slots by this level
_EXEMPLAR_SLOTS = 2


class BuildSpecError(ValueError):
    """A build spec the game could not grant: the message carries an E-code and the level."""


# --- the reference tables a ``.mbd`` is checked and resolved against (Mids dump harness) ---

POWERSETS_EXPECTED = 8  # primary, secondary, "", four pools, ancillary (Character.cs:23)
_MAX_LEVEL_TEXT = 49  # CharacterBuildData.cs:41: the header level is 0-based


@dataclass(frozen=True, slots=True)
class EnhInfo:
    """One enhancement: its nID, type, owning set and the InventO IO-level bounds."""

    nid: int
    type_name: str
    set_nid: int
    level_min: int
    level_max: int


@dataclass(frozen=True, slots=True)
class PowerTemplate:
    """A power from ``powers.json``: the database fields, without per-build state or effects."""

    full_name: str
    nid: int
    static_index: int
    power_type: str
    level: int
    set_types: tuple[int, ...]
    enhancements: tuple[int, ...]
    requires_powers: tuple[tuple[str, ...], ...]
    requires_powers_not: tuple[tuple[str, ...], ...]
    requires_class: tuple[str, ...]
    requires_class_not: tuple[str, ...]
    power_set_type: str


@dataclass(frozen=True, slots=True)
class MbdDb:
    """What the writer, the invariants and the resolution read: one bundle from one Mids dump."""

    enhancements: Mapping[str, EnhInfo]
    set_bounds: Mapping[int, tuple[int, int]]
    archetypes: ArchetypeDb
    powers: Mapping[str, PowerTemplate]


def load_mbd_db(mids_dir: Path | str) -> MbdDb:
    """Read ``enhancements.json``, ``enhancement_sets.json``, ``archetypes.json`` and ``powers.json`` from a dump.

    Raises:
        FileNotFoundError: if one of the files is missing.
        KeyError: if a file lacks a field; re-dump with the current harness.
    """
    root = Path(mids_dir)
    enhancements = {
        r["UID"]: EnhInfo(r["Nid"], r["TypeName"], r["nIDSet"], r["LevelMin"], r["LevelMax"])
        for r in json.loads((root / "enhancements.json").read_text(encoding="utf-8"))
    }
    set_bounds = {
        r["Nid"]: (r["LevelMin"], r["LevelMax"])
        for r in json.loads((root / "enhancement_sets.json").read_text(encoding="utf-8"))
    }
    powers = {
        r["FullName"]: PowerTemplate(
            full_name=r["FullName"],
            nid=r["NIDPower"],
            static_index=r["StaticIndex"],
            power_type=r["PowerType"],
            level=r["Level"],
            set_types=tuple(r["SetTypes"]),
            enhancements=tuple(r["Enhancements"]),
            requires_powers=tuple(tuple(g) for g in r["RequiresPowers"]),
            requires_powers_not=tuple(tuple(g) for g in r["RequiresPowersNot"]),
            requires_class=tuple(r["RequiresClass"]),
            requires_class_not=tuple(r["RequiresClassNot"]),
            power_set_type=r["PowerSetType"],
        )
        for r in json.loads((root / "powers.json").read_text(encoding="utf-8"))
    }
    return MbdDb(enhancements, set_bounds, load_archetypes(root / "archetypes.json"), powers)


def powersets_arity(power_sets: Sequence[object]) -> int:
    """The number of ``PowerSets`` entries; MidsReborn indexes eight (``Character.Reset``)."""
    return len(power_sets)


def io_level_bounds(uid: str, db: MbdDb) -> tuple[int, int] | None:
    """The 0-based ``IoLevel`` range MidsReborn keeps for ``uid``; ``None`` for a uid not in the table.

    Ported from ``Enhancement.CheckAndFixIOLevel`` (``Enhancement.cs:340-358``): a set piece
    takes its set's bounds, an InventO its own, a set piece with no set the defaults (9, 52).
    Every other type has no IO level and Mids keeps 1.
    """
    info = db.enhancements.get(uid)
    if info is None:
        return None
    if info.type_name == "InventO":
        return (info.level_min, info.level_max)
    if info.type_name == "SetO":
        return db.set_bounds.get(info.set_nid, (9, 52))
    return (1, 1)


def level_text_problem(value: object) -> str | None:
    """Why ``value`` is not a header ``Level`` MidsReborn loads: a string of an integer from 0 to 49, or ``None``."""
    if isinstance(value, str) and value.lstrip("-").isdigit() and 0 <= int(value) <= _MAX_LEVEL_TEXT:
        return None
    return f"{value!r} is not a string of an integer from 0 to {_MAX_LEVEL_TEXT}"


def missing_inherents(entry_names: Sequence[str], class_name: str, level: int, db: MbdDb) -> list[str]:
    """The inherent powers Mids adds to a character of ``level`` that ``entry_names`` lacks.

    A level 0 inherent is Mids' virtual set-bonus power, added only when a set bonus exists, so it
    is never required.
    """
    nid = db.archetypes.nid_from_uid_class(class_name)
    if nid < 0:
        raise BuildSpecError(f"E76: class {class_name} is not in the archetype table; re-dump the Mids data")
    present = set(entry_names)
    return [
        p.full_name
        for p in db.archetypes.classes[nid].inherent_powers
        if 1 <= p.level <= level and p.full_name not in present
    ]


def _enhancement_uid(obj: Mapping[str, Any] | None) -> str | None:
    """The uid of an ``EnhancementData`` object, with the legacy ``Enhancement`` key accepted."""
    if obj is None:
        return None
    uid = obj.get("Uid", obj.get("Enhancement"))
    return uid if isinstance(uid, str) else None


def check_mbd_invariants(doc: Mapping[str, Any], db: MbdDb) -> None:
    """Refuse a ``.mbd`` JSON object MidsReborn would crash on or load wrongly.

    Raises:
        BuildSpecError: E73 for a ``PowerSets`` list not 8 long, E75 for a ``Level`` that is not
            a string of 0 to 49, E76 for a missing inherent power, E74 for an ``IoLevel`` outside
            the bounds of its enhancement (MidsReborn clamps it on load, which once left every
            typed defence and resistance negative).
    """
    arity = powersets_arity(doc["PowerSets"])
    if arity != POWERSETS_EXPECTED:
        raise BuildSpecError(
            f"E73: PowerSets has {arity} entries; MidsReborn needs {POWERSETS_EXPECTED}; pad with empty strings"
        )
    problem = level_text_problem(doc["Level"])
    if problem:
        raise BuildSpecError(f"E75: Level {problem}; write the character level minus one as text")
    entries = doc["PowerEntries"]
    missing = missing_inherents([e["PowerName"] for e in entries], doc["Class"], int(doc["Level"]) + 1, db)
    if missing:
        raise BuildSpecError(f"E76: missing inherent powers {', '.join(missing)}; add them as unslotted entries")
    for entry in entries:
        for slot in entry["SlotEntries"]:
            for key in ("Enhancement", "FlippedEnhancement"):
                enh = slot.get(key)
                uid = _enhancement_uid(enh)
                bounds = io_level_bounds(uid, db) if uid else None
                if bounds and not bounds[0] <= enh.get("IoLevel", 1) <= bounds[1]:
                    raise BuildSpecError(
                        f"E74: {entry['PowerName']} slot at level {slot['Level']}: {uid} has IoLevel "
                        f"{enh.get('IoLevel', 1)}, outside {bounds[0]} to {bounds[1]}; write at most {bounds[1]}"
                    )


@dataclass(frozen=True, slots=True)
class PickSpec:
    """One power and its enhancements, as character levels (the ``.mbd`` stores them so).

    ``enhancements[0]`` fills the free (A) slot at ``level``; the rest are added slots, filled
    in order as they are placed. ``slot_levels`` pins the added slots to given grant levels;
    left ``None``, the writer places them. ``essential`` marks an exemplar-10 essential (a
    defensive toggle or an anchor attack): its first two added slots come first, by level 15.
    """

    power: str
    level: int
    enhancements: tuple[str | None, ...] = (None,)
    stat_include: bool = False
    essential: bool = False
    slot_levels: tuple[int, ...] | None = None


@dataclass(frozen=True, slots=True)
class BuildSpec:
    """A whole build: the picks, the auto-granted powers that carry slots, the incarnates."""

    class_name: str
    origin: str
    alignment: str
    name: str
    comment: str
    power_sets: tuple[str, ...]
    picks: tuple[PickSpec, ...]
    inherents: tuple[PickSpec, ...] = ()
    incarnates: tuple[str, ...] = ()
    meta: MetaData = HOMECOMING
    level: int = 50  # the character level; the file stores it minus one


def _ordered_picks(picks: tuple[PickSpec, ...], schedule: LevelSchedule) -> list[PickSpec]:
    """The picks in schedule order; each must sit on the level its position is granted at."""
    order = schedule.power_pick_levels
    if len(picks) > len(order):
        raise BuildSpecError(f"E46: {len(picks)} picks; the schedule grants {len(order)}")
    ordered = sorted(picks, key=lambda p: p.level)  # stable: the two level-1 picks keep their order
    for position, pick in enumerate(ordered):
        granted = order[position] + 1
        if pick.level != granted:
            raise BuildSpecError(
                f"E46: pick {position + 1} of {len(order)} ({pick.power}) is at level {pick.level}; "
                f"the schedule grants it at level {granted}"
            )
    return ordered


def _place_slots(powers: list[PickSpec], schedule: LevelSchedule) -> dict[int, list[int]]:
    """Each power's added-slot levels, walking the grant levels in order (keyed by list index).

    Pinned slots take their grants first. The rest go, grant by grant, to the powers already
    picked: essentials until they hold two, then the power least slotted for its need, the
    earlier-listed on a tie. A grant is given only if every later need can still be met by
    the grants after it, so a late pick is never left without the slots it asks for.
    """
    grants = [(level + 1, count) for level, count in sorted(schedule.slots_per_level.items())]
    capacity = dict(grants)
    need = {i: len(p.enhancements) - 1 for i, p in enumerate(powers) if len(p.enhancements) > 1}
    total = sum(need.values())
    if total > schedule.total_slots_granted:
        raise BuildSpecError(f"E49: {total} added slots; the schedule grants {schedule.total_slots_granted}")
    placed = _pin_slots(powers, need, capacity)
    open_grants = [level for level, _ in grants for _ in range(capacity[level])]
    remaining = {i: need[i] - len(placed[i]) for i in need if need[i] > len(placed[i])}
    for i, n in remaining.items():
        reachable = sum(1 for level in open_grants if level >= powers[i].level)
        if n > reachable:
            raise BuildSpecError(
                f"E49: {powers[i].power} picked at level {powers[i].level} needs {n} added slots; "
                f"{reachable} can be placed from level {powers[i].level}"
            )
    if not _feasible(open_grants, remaining, powers):
        raise BuildSpecError("E49: the powers picked late need more added slots than the grants after their picks")
    _fill_slots(powers, remaining, open_grants, placed)
    return placed


def _pin_slots(powers: list[PickSpec], need: dict[int, int], capacity: dict[int, int]) -> dict[int, list[int]]:
    """Take the grants each pinned power names, refusing a slot before its power or off a grant."""
    placed: dict[int, list[int]] = {i: [] for i in need}
    for i, pick in enumerate(powers):
        if pick.slot_levels is None or i not in need:
            continue
        if len(pick.slot_levels) != need[i]:
            plural = "s" if len(pick.slot_levels) != 1 else ""
            raise BuildSpecError(
                f"E48: {pick.power} names {len(pick.slot_levels)} slot level{plural} for {need[i]} added slots"
            )
        for level in pick.slot_levels:
            if level < pick.level:
                raise BuildSpecError(
                    f"E47: {pick.power} places a slot at level {level}, before it is picked at level {pick.level}"
                )
            if capacity.get(level, 0) < 1:
                raise BuildSpecError(f"E48: {pick.power} places a slot at level {level}, which grants no slot")
            capacity[level] -= 1
            placed[i].append(level)
    return placed


def _fill_slots(
    powers: list[PickSpec], remaining: dict[int, int], open_grants: list[int], placed: dict[int, list[int]]
) -> None:
    """Give each open grant, in level order, to the highest-ranked power that keeps the rest placeable."""
    taken = dict.fromkeys(remaining, 0)
    for position, level in enumerate(open_grants):
        waiting = [i for i in remaining if taken[i] < remaining[i] and powers[i].level <= level]
        ranked = sorted(
            waiting,
            key=lambda i: (
                not (powers[i].essential and taken[i] < _EXEMPLAR_SLOTS and level <= _EXEMPLAR_LEVEL),
                taken[i] / remaining[i],
                i,
            ),
        )
        for i in ranked:
            taken[i] += 1
            if _feasible(open_grants[position + 1 :], {j: remaining[j] - taken[j] for j in remaining}, powers):
                placed[i].append(level)
                break
            taken[i] -= 1


def _feasible(grants: list[int], left: dict[int, int], powers: list[PickSpec]) -> bool:
    """Whether ``grants`` can still cover every remaining need: latest grant to the latest pick."""
    left = dict(left)
    for level in reversed(grants):
        waiting = [i for i in left if left[i] > 0 and powers[i].level <= level]
        if waiting:
            left[max(waiting, key=lambda i: powers[i].level)] -= 1
    return not any(left.values())


def _enhancement(uid: str | None, db: MbdDb) -> dict[str, Any] | None:
    if uid is None:
        return None
    bounds = io_level_bounds(uid, db)
    if bounds is None:
        raise BuildSpecError(
            f"E78: enhancement {uid} is not in enhancements.json; check the uid or re-dump the Mids data"
        )
    return {"Uid": uid, "Grade": "None", "IoLevel": bounds[1], "RelativeLevel": "Even", "Obtained": False}


def _entry(pick: PickSpec, added: list[int], db: MbdDb) -> dict[str, Any]:
    levels = [pick.level, *sorted(added)] if pick.enhancements else []
    return {
        "PowerName": pick.power,
        "Level": pick.level,
        "StatInclude": pick.stat_include,
        "ProcInclude": False,
        "VariableValue": 0,
        "InherentSlotsUsed": 0,
        "SubPowerEntries": [],
        "SlotEntries": [
            {"Level": level, "IsInherent": False, "Enhancement": _enhancement(uid, db), "FlippedEnhancement": None}
            for level, uid in zip(levels, pick.enhancements, strict=True)
        ],
    }


def _omitted_inherents(spec: BuildSpec, db: MbdDb) -> list[PickSpec]:
    """The class inherents the spec leaves out, as Mids adds them: unslotted but for the free slot."""
    names = [pick.power for pick in spec.inherents]
    added = []
    for name in missing_inherents(names, spec.class_name, spec.level, db):
        template = db.powers.get(name)
        if template is None:
            raise BuildSpecError(f"E77: inherent power {name} is not in powers.json; re-dump the Mids data")
        added.append(
            PickSpec(name, template.level, (None,) if template.enhancements else (), template.power_type == "Auto_")
        )
    return added


def write_mbd(spec: BuildSpec, schedule: LevelSchedule, db: MbdDb) -> dict[str, Any]:
    """The ``.mbd`` JSON object for ``spec``, picks and slots walked in the order the game grants them.

    Picks are written in pick-schedule order whatever order the spec lists them, because
    MidsReborn places a pick by its position among the 24, not by its level. The class
    inherents the spec omits follow, as Mids adds them, and the result is checked against
    :func:`check_mbd_invariants` before it is returned.

    Raises:
        BuildSpecError: E46 for a pick off its schedule level, E47 for a pinned slot before
            its power, E48 for a pinned slot off a grant level or a short pin list, E49 for
            added slots the schedule cannot place, E77 for an inherent power the powers table
            lacks, E78 for an enhancement the table lacks, and E73 to E76 from the invariants.
    """
    picks = _ordered_picks(spec.picks, schedule)
    powers = [*picks, *spec.inherents, *_omitted_inherents(spec, db)]
    placed = _place_slots(powers, schedule)
    entries = [_entry(pick, placed.get(i, []), db) for i, pick in enumerate(powers)]
    entries += [_entry(PickSpec(name, 50, (), stat_include=True), [], db) for name in spec.incarnates]
    doc = {
        "BuiltWith": {
            "App": spec.meta.app,
            "Version": spec.meta.version,
            "Database": spec.meta.database,
            "DatabaseVersion": spec.meta.database_version,
        },
        "Level": str(spec.level - 1),
        "Class": spec.class_name,
        "Origin": spec.origin,
        "Alignment": spec.alignment,
        "Name": spec.name,
        "Comment": spec.comment,
        "PowerSets": list(spec.power_sets),
        "LastPower": len(picks),
        "PowerEntries": entries,
    }
    check_mbd_invariants(doc, db)
    return doc
