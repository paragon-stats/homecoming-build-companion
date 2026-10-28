"""The character-level schedule (``Database.Levels``) — slot-grant and power-pick levels.

The hard-limits validator checks that an added enhancement slot lands on a real
slot-grant level and that a power is picked at a real power-pick level. Both come
from the game's own level table (``Database.Levels``, one ``LevelMap`` per level),
dumped by the harness as ``levels.json`` — never a schedule reconstructed from
memory (:mod:`coh_engine.hard_limits`).

Level convention: the dump carries a 0-based ``LevelIndex`` (in-game level minus 1)
alongside the 1-based ``GameLevel``. Slot placement levels (``SlotRef.level``) and
power pick levels (``Power.pick_level``) are the same 0-based indices, so the
schedule is keyed on ``LevelIndex`` to compare against them directly.

Spec: docs/engine/mids-port-spec.md; .claude/rules/hard-limits.md § Slot rules.
"""

import json
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from types import MappingProxyType


@dataclass(frozen=True, slots=True)
class LevelSchedule:
    """The game's slot-grant and power-pick levels, as 0-based level indices.

    ``slots_per_level`` maps a slot-grant level index to how many enhancement slots
    it grants (2 at levels 3-29, 3 at 31-50 on Homecoming; 67 total). ``slot_grant_levels``
    is its key set. ``power_pick_levels`` is the 24-pick schedule in order: the n-th entry
    is the level index of the n-th pick (level index 0 twice, for the two level-1 picks).
    Mids places a pick by its position among the 24, so the order is part of the rule.
    """

    slots_per_level: Mapping[int, int]
    power_pick_levels: tuple[int, ...]

    @property
    def slot_grant_levels(self) -> frozenset[int]:
        """The level indices that grant at least one enhancement slot."""
        return frozenset(self.slots_per_level)

    @property
    def total_slots_granted(self) -> int:
        """The total enhancement slots the schedule grants across all levels (67)."""
        return sum(self.slots_per_level.values())


def load_level_schedule(path: Path | str) -> LevelSchedule:
    """Parse a ``levels.json`` Mids reference dump into a :class:`LevelSchedule`.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        raw = json.load(stream)
    slots_per_level = {entry["LevelIndex"]: entry["Slots"] for entry in raw["SlotGrantLevels"]}
    power_pick_levels = tuple(entry["LevelIndex"] for entry in raw["PowerPickLevels"])
    return LevelSchedule(
        slots_per_level=MappingProxyType(slots_per_level),
        power_pick_levels=power_pick_levels,
    )
