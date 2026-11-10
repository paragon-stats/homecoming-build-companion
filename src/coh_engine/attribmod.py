"""Loader for MidsReborn's AttribMod modifier tables and the GetModifier lookup.

The AttribMod tables resolve a power effect's abstract scale into a concrete base
magnitude for an archetype at a level: ``table[iTable].Table[iLevel][classColumn]``.
Cells are C# ``float`` (single precision) and are quantized to f32 on load.

Mirrors:
- ``Modifiers.ModifierTable`` (``Core/Modifiers.cs:124-153``) — ``{Table, BaseIndex, ID}``.
- ``DatabaseAPI.GetModifier(iClass, iTable, iLevel)`` (``Core/DatabaseAPI.cs:2632-2647``)
    — the private bounds-checked lookup, reproduced in :func:`get_modifier`.
- ``DatabaseAPI.NidFromUidAttribMod`` (``Core/DatabaseAPI.cs:98-119``) — name -> table index.

Every out-of-range condition returns ``0.0`` exactly as the C# does. The class
column is ``Classes[iClass].Column`` — a stored indirection, NOT ``iClass`` itself.
Spec: docs/engine/mids-port-spec.md § at-modifier-tables.
"""

import json
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

from coh_engine.maths import f32


class _HasColumn(Protocol):
    """Minimal shape the modifier lookup needs from a class record."""

    @property
    def column(self) -> int:
        """The record's column."""


@dataclass(frozen=True, slots=True)
class ModifierTable:
    """One named AttribMod table: ``id`` plus a ``[level][classColumn]`` grid."""

    id: str
    base_index: int
    table: tuple[tuple[float, ...], ...]


@dataclass(frozen=True, slots=True)
class AttribMods:
    """All AttribMod tables, indexed as ``AttribMods.Modifier`` is in MidsReborn."""

    tables: tuple[ModifierTable, ...]

    def nid_from_uid_attribmod(self, name: str) -> int:
        """Case-insensitive table-name -> index lookup; -1 if not found."""
        target = name.casefold()
        for i, t in enumerate(self.tables):
            if t.id.casefold() == target:
                return i
        return -1


def load_attribmod(path: Path | str) -> AttribMods:
    """Parse an ``AttribMod.json`` file into :class:`AttribMods`.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        payload = json.load(stream)
    tables = tuple(
        ModifierTable(
            id=entry["ID"],
            base_index=entry["BaseIndex"],
            table=tuple(tuple(f32(cell) for cell in row) for row in entry["Table"]),
        )
        for entry in payload["Modifier"]
    )
    return AttribMods(tables=tables)


def get_modifier(
    mods: AttribMods,
    classes: Sequence[_HasColumn],
    i_class: int,
    i_table: int,
    i_level: int,
) -> float:
    """Return one table cell, reproducing ``DatabaseAPI.GetModifier`` bounds checks.

    The private C# ``GetModifier(iClass, iTable, iLevel)`` (``DatabaseAPI.cs:2632-2647``)
    returns ``0.0`` for any out-of-range index and dereferences the column through
    ``Classes[iClass].Column`` rather than ``iClass`` directly.
    """
    if i_class < 0 or i_table < 0 or i_level < 0:
        return 0.0
    if i_class > len(classes) - 1:
        return 0.0
    column = classes[i_class].column
    if column < 0:
        return 0.0
    if i_table > len(mods.tables) - 1:
        return 0.0
    rows = mods.tables[i_table].table
    if i_level > len(rows) - 1:
        return 0.0
    row = rows[i_level]
    if column > len(row) - 1:
        return 0.0
    return row[column]
