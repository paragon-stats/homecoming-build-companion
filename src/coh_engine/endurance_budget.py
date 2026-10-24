"""Endurance budget for the chain planner: pool, recovery income, and committed toggle drain.

Builds the :class:`~coh_engine.damage_model.EnduranceBudget` from the engine's ``Totals`` /
``TotalsCapped`` (CP5), and loads those from a Mids dump ``totals.json`` so a budget can be
built without re-running the totals pipeline.

``max_end`` follows ``Statistics.cs:31``: ``EnduranceMaxEnd => Totals.EndMax + 100f`` --
``EndMax`` is the absolute bonus in endurance points over the base 100 pool, taken from the
uncapped ``Totals``. Recovery reuses :func:`coh_engine.scoring.endurance_recovery_per_sec`
(``Statistics.cs:33``, capped totals); toggle drain is ``Totals.EndUse`` (uncapped), as in
:func:`coh_engine.scoring.endurance_diagnostic`.

Recovery scales with max endurance (``EndMax / 100 + 1``), and HP regeneration with max HP
(``Statistics.cs:51``), so a permanent pool increase also raises the per-second tick. Mids
caps HP (``hp_cap``) but never max endurance; the canonical AT data carries both hard caps
(``attrib_max_max``), so the planner clamps the pool -- and the recovery scaling -- at the
canonical endurance cap when one is supplied (CLAUDE.md B5: canonical wins on numbers).

Spec: docs/engine/mids-port-spec.md; error-output.md.
"""

from dataclasses import dataclass, replace
from pathlib import Path
from typing import Any

from coh_engine.archetypes import Archetype
from coh_engine.attack_profile import read_json
from coh_engine.base_totals import TotalStatistics
from coh_engine.damage_model import EnduranceBudget
from coh_engine.maths import f32
from coh_engine.scoring import endurance_recovery_per_sec

_BASE_END = 100.0  # Statistics.cs:31
_REGEN_MAGIC = f32(1.66666662693024)  # Statistics.cs:51
_LEVEL_50 = 49  # canonical per-level arrays are 0-based

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


@dataclass(frozen=True, slots=True)
class HealthBudget:
    """Max HP (AT-capped) and the HP regenerated per second, which scales with it."""

    max_hp: float
    regen_per_sec: float


def load_canonical_caps(archetypes_dir: Path, class_name: str) -> tuple[float, float]:
    """The AT's hard caps at level 50 from canonical ``attrib_max_max``: (max endurance, max HP).

    Raises:
        ValueError: ``E32`` if no canonical archetype file exists for ``class_name``; ``E33`` if it is not valid JSON.
    """
    path = archetypes_dir / f"{class_name.removeprefix('Class_').lower()}.json"
    if not path.is_file():
        raise ValueError(
            f"E32: no canonical archetype file {path} for {class_name}; pass the AT class name "
            "(e.g. Class_Arachnos_Soldier) or restore data/canonical/archetypes"
        )
    caps = read_json(path, "E33")["attrib_max_max"]
    return f32(caps["endurance"][_LEVEL_50]), f32(caps["hit_points"][_LEVEL_50])


def build_endurance_budget(
    totals: TotalStatistics,
    totals_capped: TotalStatistics,
    archetype: Archetype,
    max_end_cap: float | None = None,
) -> EnduranceBudget:
    """The endurance pool, recovery/sec and toggle drain/sec a build starts from.

    Args:
        totals: Uncapped ``Totals`` (``EndMax`` and ``EndUse`` are read from here).
        totals_capped: ``TotalsCapped`` (recovery is computed over these).
        archetype: The build's class record (``BaseRecovery``).
        max_end_cap: The AT's hard max-endurance cap (:func:`load_canonical_caps`); the pool and
            the recovery scaling are both clamped to it. ``None`` applies no cap, as Mids does.

    Returns:
        The :class:`EnduranceBudget`; ``max_end`` is ``100 + Totals.EndMax`` (``Statistics.cs:31``).
    """
    max_end = f32(_BASE_END + totals.end_max)
    if max_end_cap is not None and max_end > max_end_cap:
        max_end = f32(max_end_cap)
        totals_capped = replace(totals_capped, end_max=f32(max_end_cap - _BASE_END))
    return EnduranceBudget(
        max_end=max_end,
        recovery_per_sec=endurance_recovery_per_sec(totals_capped, archetype),
        toggle_drain_per_sec=totals.end_use,
    )


def build_health_budget(totals_capped: TotalStatistics, archetype: Archetype) -> HealthBudget:
    """Max HP and HP regenerated per second -- ``Statistics.cs:51`` ``HealthRegenHPPerSec``.

    ``(HPRegen + 1) * BaseRegen * 1.6666666 * HPMax / 100`` over the capped totals, so the
    tick grows with max HP, which is already clamped to the AT ``hp_cap`` in ``TotalsCapped``.
    """
    rate = f32(f32(totals_capped.hp_regen + 1.0) * f32(archetype.base_regen * _REGEN_MAGIC))
    return HealthBudget(max_hp=totals_capped.hp_max, regen_per_sec=f32(rate * totals_capped.hp_max / 100.0))


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
        ValueError: ``E31`` if a section or a field ``TotalStatistics`` needs is missing; ``E33`` if the file is not valid JSON.
    """
    path = Path(path)
    data: dict[str, Any] = read_json(path, "E33")
    totals = _totals_from(_required(data, "Totals", "<file>", path), "Totals", path)
    capped = _totals_from(_required(data, "TotalsCapped", "<file>", path), "TotalsCapped", path)
    return totals, capped
