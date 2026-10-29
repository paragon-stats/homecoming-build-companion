"""Power-name resolution — internal names to display names and back, scoped to player powersets.

A power's internal name (``Pool.Teleportation.Long_Range_Teleport``) often differs from the
name the game shows (``Fold Space``), and bare leaf names also collide with NPC and proxy
copies (``V_Arachnos_Proxy``, ``Warriors_New``). :class:`PowerNameResolver` indexes only the
powersets a player archetype can take, so an NPC copy can never shadow a player power, and
renders a power as its display name, its internal name, or both for diagnostics and emitters.
"""

import json
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Literal

_ARCHETYPE_CATEGORY_FIELDS = ("primary_category", "secondary_category", "power_pool_category")
_EPIC_CATEGORY = "epic"


@dataclass(frozen=True)
class PowerName:
    """One player power: its full internal name, the name the game shows, and its powerset."""

    internal: str
    display: str
    powerset: str


@dataclass(frozen=True)
class PowersetName:
    """One player powerset with the archetypes that can take it and its powers in index order."""

    internal: str
    display: str
    category: str
    archetypes: tuple[str, ...]
    powers: tuple[PowerName, ...]


def _read(path: Path) -> Any:
    with open(path, encoding="utf-8") as stream:
        return json.load(stream)


def _player_set_dirs(canonical: Path) -> dict[tuple[str, str | None], set[str]]:
    """Archetype keys per ``(category dir, set dir)``; the set dir is None for a whole category.

    The player archetypes are the keys of ``powers_by_at.json`` (the archetype folder also
    holds NPC classes). Whole categories come from each archetype's primary, secondary and
    pool fields; ``epic_pool_category`` is null in the harvest, so ancillary and patron sets
    come from the ``epic.<set>`` paths in ``powers_by_at.json``.
    """
    powers_by_at = _read(canonical / "powers_by_at.json")
    found: dict[tuple[str, str | None], set[str]] = defaultdict(set)
    for at, entries in powers_by_at.items():
        record = _read(canonical / "archetypes" / f"{at}.json")
        for field in _ARCHETYPE_CATEGORY_FIELDS:
            found[(record[field].casefold(), None)].add(at)
        for _, path in entries:
            category, set_dir = path.split(".")[:2]
            if category == _EPIC_CATEGORY:
                found[(category, set_dir)].add(at)
    return found


class PowerNameResolver:
    """Bidirectional internal/display lookup over the player powersets of ``data/canonical``."""

    def __init__(self, powersets: tuple[PowersetName, ...]) -> None:
        self._powersets = powersets
        self._by_internal = {p.internal: p for ps in powersets for p in ps.powers}

    @classmethod
    def from_canonical(cls, canonical: Path) -> PowerNameResolver:
        """Index every player powerset under ``canonical`` (the ``data/canonical`` directory)."""
        powers = canonical / "powers"
        sets: dict[Path, set[str]] = defaultdict(set)
        for (category, set_dir), ats in _player_set_dirs(canonical).items():
            members = (
                [powers / category / set_dir]
                if set_dir
                else sorted(p for p in (powers / category).iterdir() if p.is_dir())
            )
            for member in members:
                sets[member] |= ats
        built = []
        for directory, ats in sets.items():
            category_name = _read(directory.parent / "index.json")["name"]
            index = _read(directory / "index.json")
            internal = f"{category_name}.{index['name']}"
            built.append(
                PowersetName(
                    internal=internal,
                    display=index["display_name"],
                    category=category_name,
                    archetypes=tuple(sorted(ats)),
                    powers=tuple(
                        PowerName(name, display, internal)
                        for name, display in zip(index["power_names"], index["power_display_names"], strict=True)
                    ),
                )
            )
        return cls(tuple(sorted(built, key=lambda ps: ps.internal)))

    def powersets(self) -> tuple[PowersetName, ...]:
        """Every player powerset, sorted by internal name."""
        return self._powersets

    def display_name(self, internal: str) -> str:
        """The name the game shows for a power's full internal name.

        Raises:
            ValueError: ``E22`` if ``internal`` is not a player power.
        """
        power = self._by_internal.get(internal)
        if power is None:
            raise ValueError(
                f"E22: internal power name {internal!r} is not a player power among the {len(self._by_internal)} indexed; "
                "pass a full name such as 'Pool.Teleportation.Long_Range_Teleport'"
            )
        return power.display

    def internal_names(self, display: str, *, powerset: str | None = None) -> tuple[str, ...]:
        """Every player power shown as ``display`` (case-insensitive), optionally within one powerset.

        Display names repeat across the archetype versions of a set, so this may return
        several names; ``()`` means none matched.
        """
        target = display.casefold()
        return tuple(
            p.internal
            for p in self._by_internal.values()
            if p.display.casefold() == target and (powerset is None or p.powerset == powerset)
        )

    def resolve(self, display: str, *, powerset: str) -> str:
        """The one internal name shown as ``display`` within ``powerset``.

        Raises:
            ValueError: ``E22`` unless exactly one power in ``powerset`` matches.
        """
        matches = self.internal_names(display, powerset=powerset)
        if len(matches) == 1:
            return matches[0]
        raise ValueError(
            f"E22: display name {display!r} matches {len(matches)} powers in powerset {powerset!r} {list(matches)}, expected 1; "
            "pass the display name and the powerset's internal name, e.g. 'Fold Space' in 'Pool.Teleportation'"
        )

    def render(self, internal: str, form: Literal["display", "internal", "both"] = "display") -> str:
        """A power as its display name, its internal name, or ``"Display (Internal)"``.

        Raises:
            ValueError: ``E22`` if ``internal`` is not a player power.
        """
        display = self.display_name(internal)
        if form == "internal":
            return internal
        return f"{display} ({internal})" if form == "both" else display
