"""Run the engine's own hard-limit rules against a scraped build, in either format.

A scraped build is just a file; nothing so far has asked whether it is a *legal* build.
That matters for scoring, because "does this obey the game's own limits" is a far harder
signal than anything readable off a thread title — a build that overspends the 67-slot
budget or picks a power below its level was never going to work in game, whoever posted it
and however often they were quoted.

The engine already owns those rules (:mod:`coh_engine.hard_limits`: twelve of them, slot
budget, pick count, pool cap, tier prerequisites, slot-grant levels). What it does not own
is a way in from a ``.mbd`` file, because its rules read the ``Power`` DTO that the C#
harness dump produces for one specific build at a time. This module is that missing step:
it turns a parsed ``.mbd`` into the DTOs the rules expect, so the verdict on a scraped
build comes from the same code that validates a build the repo writes itself, not from a
second opinion invented here.

Both formats go through the same bridge. ``mxd.py`` builds its ``MxdBuild`` out of the
very ``PowerEntry`` dataclass ``mbd.py`` defines, and both store levels 0-based, so the
conversion below never needs to know which file it came from. That matters because the
legacy format is not dead data: MidsReborn still opens a ``.mxd`` and converts it, saving
in it stayed possible long after the new format landed, and it holds the oldest builds —
the ones whose era matters most when reading a legality verdict.

Two fields have to come from outside the build file, because it records what was picked
and not what the game allows:

- ``level``, the earliest level a power *may* be taken, from each powerset's
    ``index.json`` in ``data/canonical/powers``.
- ``requires_powers``, the tier prerequisites, from the pool and epic power files — the
    only places tier gating applies.

Everything else the rules read (pick level, slot levels, slot counts, power names) is in
the file. Fields the rules never touch are filled with neutral placeholders rather than
invented values; see :func:`_placeholder_power`.
"""

import json
import re
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path

from coh_engine.buildfile.dbindex import EnhIndexEntry, load_enhancement_index, load_power_index
from coh_engine.buildfile.mbd import Build, PowerEntry, parse_mbd
from coh_engine.buildfile.mxd import MxdBuild, classify, read_mxd
from coh_engine.diagnostics import Diagnostic
from coh_engine.effect import Power
from coh_engine.enhancement import SlotRef
from coh_engine.hard_limits import check_hard_limits
from coh_engine.levels import LevelSchedule, load_level_schedule

# A `requires` term naming a power looks like "Group.Set.Power". Anything with a
# parenthesis or a comparison is one of Mids' expression forms (`ownPowerNum?(Epic) > 0`,
# `$archetype == @Class_Mastermind`), which the engine deliberately does not evaluate.
_POWER_TERM = re.compile(r"^[A-Za-z0-9_]+\.[A-Za-z0-9_]+\.[A-Za-z0-9_:']+$")
# Power groups Mids adds to a build rather than the player picking them. The harness dump
# records this as `PowerEntry.Chosen = false`; a .mbd has no such field, so the group name
# is what distinguishes them. Accolades are the ones that bite: a build with four of them
# reads as 28 picks against a cap of 24, and every accolade also reports as picked at a
# level that grants no pick. `Inherent` and `Incarnate` are here for completeness — the
# hard-limits rules already exclude those two by group.
_AUTO_GRANTED_GROUPS = frozenset({"Temporary_Powers", "Incarnate", "Inherent", "Pets", "Set_Bonus"})
_ENH_GRADE = ("None", "TrainingO", "DualO", "SingleO")
_ENH_RELATIVE = (
    "None",
    "MinusThree",
    "MinusTwo",
    "MinusOne",
    "Even",
    "PlusOne",
    "PlusTwo",
    "PlusThree",
    "PlusFour",
    "PlusFive",
)


@dataclass(frozen=True, slots=True)
class PowerCatalog:
    """What the game allows, keyed by power full name — the half a ``.mbd`` cannot know."""

    levels: Mapping[str, int]  # earliest pick level, 0-based like the build's levels
    requires: Mapping[str, tuple[tuple[str, ...], ...]]  # OR list of AND-groups


def parse_requires(expression: str) -> tuple[tuple[str, ...], ...]:
    """Turn a canonical ``requires`` string into the engine's OR-of-ANDs shape.

    ``"A && B || A && C"`` becomes ``(("A", "B"), ("A", "C"))``. Terms that are not power
    names are dropped, and a group left empty by that drop is dropped with them: a
    prerequisite the engine cannot check must not become a prerequisite of nothing, which
    would read as "satisfied" and silently pass a build that fails it.
    """
    groups: list[tuple[str, ...]] = []
    for clause in expression.split("||"):
        terms = tuple(term.strip() for term in clause.split("&&") if _POWER_TERM.match(term.strip()))
        if terms:
            groups.append(terms)
    return tuple(groups)


def load_power_catalog(canonical: Path) -> PowerCatalog:
    """Read minimum pick levels and tier prerequisites out of the canonical power tree.

    Levels come from every powerset's ``index.json``, where ``available_level`` runs
    parallel to ``power_names``. Prerequisites are read only from the pool and epic
    powers, which is where tier gating exists; reading all 44k power files to learn that
    an archetype attack has no prerequisite would cost a great deal to learn nothing.
    """
    levels: dict[str, int] = {}
    for index_file in (canonical / "powers").rglob("index.json"):
        try:
            data = json.loads(index_file.read_text(encoding="utf-8"))
        except OSError, json.JSONDecodeError:
            continue
        names = data.get("power_names") or []
        available = data.get("available_level") or []
        for name, level in zip(names, available, strict=False):
            if isinstance(level, int):
                levels[name] = level

    requires: dict[str, tuple[tuple[str, ...], ...]] = {}
    for group in ("pool", "epic"):
        for power_file in (canonical / "powers" / group).rglob("*.json"):
            if power_file.name == "index.json":
                continue
            try:
                data = json.loads(power_file.read_text(encoding="utf-8"))
            except OSError, json.JSONDecodeError:
                continue
            parsed = parse_requires(data.get("requires") or "")
            if parsed:
                requires[data.get("full_name", "")] = parsed
    return PowerCatalog(levels=levels, requires=requires)


def _placeholder_power(
    index: int,
    entry: PowerEntry,
    catalog: PowerCatalog,
) -> Power:
    """One ``Power`` carrying only what the hard-limit rules actually read.

    The rules touch ``build_index``, ``full_name``, ``level``, ``pick_level``, ``chosen``
    and ``requires_powers``. Everything else on the DTO belongs to the numeric layers
    (effects, endurance costs, cast times) which do not run here, so those are left at
    neutral values rather than guessed — a fabricated end cost would be a number someone
    could later mistake for a measurement.
    """
    return Power(
        build_index=index,
        nid_power=-1,
        full_name=entry.power_name,
        static_index=-1,
        power_type="",
        forced_class="",
        click_buff=False,
        level=catalog.levels.get(entry.power_name, 0),
        end_cost=0.0,
        activate_period=0.0,
        toggle_cost=0.0,
        variable_enabled=False,
        stat_include=entry.stat_include,
        variable_value=entry.variable_value,
        effects=(),
        pick_level=entry.level,
        requires_powers=catalog.requires.get(entry.power_name, ()),
        # An entry with no power name is an empty slot in Mids' grid, not a pick; nor is
        # anything from a group the game grants on its own. Marking those unchosen keeps
        # them out of the 24-pick tally while holding the index, so the slot mapping stays
        # aligned and their slots still count against the budget.
        chosen=bool(entry.power_name) and entry.power_name.split(".")[0] not in _AUTO_GRANTED_GROUPS,
    )


def build_dtos(
    build: Build | MxdBuild,
    catalog: PowerCatalog,
    enhancement_ids: Mapping[str, int] | None = None,
) -> tuple[list[Power], dict[int, list[SlotRef]]]:
    """Convert a parsed build into the ``(powers, slots)`` pair the rules consume.

    Takes either format. ``MxdBuild.power_entries`` is the same ``PowerEntry`` dataclass
    the ``.mbd`` reader produces — ``mxd.py`` imports it from ``mbd.py`` — and both store
    levels 0-based, so nothing here needs to know which file it came from.
    """
    ids = enhancement_ids or {}
    powers: list[Power] = []
    slots: dict[int, list[SlotRef]] = {}
    for index, entry in enumerate(build.power_entries):
        powers.append(_placeholder_power(index, entry, catalog))
        slots[index] = [
            SlotRef(
                level=slot.level,
                is_inherent=slot.is_inherent,
                enh=ids.get(slot.enhancement.uid, -1) if slot.enhancement else -1,
                grade=_ENH_GRADE.index(slot.enhancement.grade) if slot.enhancement else 0,
                io_level=slot.enhancement.io_level if slot.enhancement else 0,
                relative_level=(_ENH_RELATIVE.index(slot.enhancement.relative_level) if slot.enhancement else 0),
            )
            for slot in entry.slot_entries
        ]
    return powers, slots


def load_enhancement_ids(dump_dir: Path) -> dict[str, int]:
    """Map enhancement UID -> nID, so a slot can name what is in it."""
    path = dump_dir / "enhancements.json"
    if not path.exists():
        return {}
    return {record["UID"]: record["Nid"] for record in json.loads(path.read_text(encoding="utf-8"))}


def validate_mbd(
    data: bytes,
    catalog: PowerCatalog,
    schedule: LevelSchedule,
    max_slots: int,
    enhancement_ids: Mapping[str, int] | None = None,
) -> list[Diagnostic]:
    """Every hard-limit violation in a scraped ``.mbd``. Empty means the build is legal.

    Raises:
        ValueError: if the bytes are not a parseable ``.mbd``.
    """
    document = json.loads(data.decode("utf-8"))
    if not isinstance(document, dict) or "PowerEntries" not in document:
        raise ValueError("not a .mbd build")
    powers, slots = build_dtos(parse_mbd(document), catalog, enhancement_ids)
    return check_hard_limits(powers, slots, schedule, max_slots)


def validate_mxd(
    text: str,
    catalog: PowerCatalog,
    schedule: LevelSchedule,
    max_slots: int,
    power_index: Mapping[int, str],
    enh_index: Mapping[int, EnhIndexEntry],
    enhancement_ids: Mapping[str, int] | None = None,
) -> list[Diagnostic]:
    """Every hard-limit violation in a legacy ``.mxd`` share block.

    The old format is not dead data: MidsReborn still opens a ``.mxd`` and converts it,
    people were able to save in it for a long time after the new format arrived, and it is
    where the oldest builds live. Skipping it would drop roughly a third of everything
    collected — and disproportionately the builds from before the current format, which
    are exactly the ones whose era matters most when reading a legality verdict.

    Raises:
        ValueError: if the block does not decode or parse.
    """
    build = read_mxd(text, dict(power_index), dict(enh_index), None)
    powers, slots = build_dtos(build, catalog, enhancement_ids)
    return check_hard_limits(powers, slots, schedule, max_slots)


def load_server_limits(dump_dir: Path) -> int:
    """``ServerData.MaxSlots`` — the added-slot budget (67 on Homecoming)."""
    path = dump_dir / "server_data.json"
    if not path.exists():
        return 67
    data = json.loads(path.read_text(encoding="utf-8"))
    value = data.get("MaxSlots", 67)
    return int(value) if isinstance(value, int | str) else 67


def validate_many(
    paths: Iterable[Path],
    canonical: Path,
    dump_dir: Path,
) -> list[tuple[Path, Sequence[Diagnostic] | None]]:
    """Validate a batch of either format, loading the shared references once.

    A ``None`` verdict means the file did not parse as a build at all, which is a
    different thing from being an illegal build and is reported as such.
    """
    catalog = load_power_catalog(canonical)
    schedule = load_level_schedule(dump_dir / "levels.json")
    max_slots = load_server_limits(dump_dir)
    ids = load_enhancement_ids(dump_dir)

    power_index = load_power_index(dump_dir / "power_static_index.json")
    enh_index = load_enhancement_index(dump_dir / "enhancements.json")

    results: list[tuple[Path, Sequence[Diagnostic] | None]] = []
    for path in paths:
        try:
            raw = path.read_bytes()
            text = raw.decode("utf-8", "replace")
            # Decided by looking at the file, not its extension: a scraped artifact is a
            # .txt whichever format's share block it holds.
            if classify(text) == "mxd":
                verdict = validate_mxd(text, catalog, schedule, max_slots, power_index, enh_index, ids)
            else:
                verdict = validate_mbd(raw, catalog, schedule, max_slots, ids)
            results.append((path, verdict))
        except OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError, KeyError, IndexError, TypeError:
            results.append((path, None))
    return results
