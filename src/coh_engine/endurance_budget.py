"""Endurance budget for the chain planner: pool, recovery income, and committed toggle drain.

Builds the :class:`~coh_engine.damage_model.EnduranceBudget` from the engine's ``Totals`` /
``TotalsCapped`` (CP5), and loads those from a Mids dump ``totals.json`` so a budget can be
built without re-running the totals pipeline.

``max_end`` follows ``Statistics.cs:31``: ``EnduranceMaxEnd => Totals.EndMax + 100f`` --
``EndMax`` is the absolute bonus in endurance points over the base 100 pool, taken from the
uncapped ``Totals``. Recovery reuses :func:`coh_engine.scoring.endurance_recovery_per_sec`
(``Statistics.cs:33``, capped totals); toggle drain is ``Totals.EndUse`` (uncapped), as in
:func:`coh_engine.scoring.endurance_diagnostic`.

Spec: docs/engine/mids-port-spec.md; error-output.md.
"""

import json
from pathlib import Path
from typing import Any

from coh_engine.archetypes import Archetype
from coh_engine.base_totals import TotalStatistics
from coh_engine.damage_model import EnduranceBudget
from coh_engine.maths import f32
from coh_engine.scoring import endurance_recovery_per_sec

_BASE_END = 100.0  # Statistics.cs:31

# Mids totals.json key -> TotalStatistics field: the single source the parity tests import.
MIDS_TOTALS_VECTOR_FIELDS = {
    "Def": "def_",
    "Res": "res",
    "Mez": "mez",
    "MezRes": "mez_res",
    "DebuffRes": "debuff_res",
    "Elusivity": "elusivity",
}
MIDS_TOTALS_SCALAR_FIELDS = {
    "HPRegen": "hp_regen",
    "HPMax": "hp_max",
    "Absorb": "absorb",
    "EndRec": "end_rec",
    "EndUse": "end_use",
    "EndMax": "end_max",
    "RunSpd": "run_spd",
    "MaxRunSpd": "max_run_spd",
    "JumpSpd": "jump_spd",
    "MaxJumpSpd": "max_jump_spd",
    "FlySpd": "fly_spd",
    "MaxFlySpd": "max_fly_spd",
    "JumpHeight": "jump_height",
    "StealthPvE": "stealth_pve",
    "StealthPvP": "stealth_pvp",
    "ThreatLevel": "threat_level",
    "Perception": "perception",
    "BuffHaste": "buff_haste",
    "BuffAcc": "buff_acc",
    "BuffToHit": "buff_to_hit",
    "BuffDam": "buff_dam",
    "BuffEndRdx": "buff_end_rdx",
    "BuffRange": "buff_range",
    "BuffHeal": "buff_heal",
}


def build_endurance_budget(
    totals: TotalStatistics, totals_capped: TotalStatistics, archetype: Archetype
) -> EnduranceBudget:
    """The endurance pool, recovery/sec and toggle drain/sec a build starts from.

    Args:
        totals: Uncapped ``Totals`` (``EndMax`` and ``EndUse`` are read from here).
        totals_capped: ``TotalsCapped`` (recovery is computed over these).
        archetype: The build's class record (``BaseRecovery``).

    Returns:
        The :class:`EnduranceBudget`; ``max_end`` is ``100 + Totals.EndMax`` (``Statistics.cs:31``).
    """
    return EnduranceBudget(
        max_end=f32(_BASE_END + totals.end_max),
        recovery_per_sec=endurance_recovery_per_sec(totals_capped, archetype),
        toggle_drain_per_sec=totals.end_use,
    )


def _totals_from(section: dict[str, Any], name: str, path: Path) -> TotalStatistics:
    values: dict[str, Any] = {}
    for json_key, attr in MIDS_TOTALS_VECTOR_FIELDS.items():
        values[attr] = [f32(v) for v in _required(section, json_key, name, path)]
    for json_key, attr in MIDS_TOTALS_SCALAR_FIELDS.items():
        values[attr] = f32(_required(section, json_key, name, path))
    return TotalStatistics(**values)


def _required(section: dict[str, Any], key: str, name: str, path: Path) -> Any:
    if key not in section:
        raise ValueError(f"E31: {path}: '{name}' has no key '{key}'; re-run tools/mids-dump to regenerate totals.json")
    return section[key]


def load_mids_totals(path: Path | str) -> tuple[TotalStatistics, TotalStatistics]:
    """Read a Mids dump ``totals.json`` into ``(Totals, TotalsCapped)``.

    Values are rounded to IEEE-754 single precision as the parity tests do. Keys the
    dump carries that ``TotalStatistics`` has no field for are ignored.

    Raises:
        ValueError: ``E31`` if a section or a field ``TotalStatistics`` needs is missing.
    """
    path = Path(path)
    with open(path, encoding="utf-8-sig") as fh:
        data: dict[str, Any] = json.load(fh)
    totals = _totals_from(_required(data, "Totals", "<file>", path), "Totals", path)
    capped = _totals_from(_required(data, "TotalsCapped", "<file>", path), "TotalsCapped", path)
    return totals, capped
