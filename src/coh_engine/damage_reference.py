"""Mids reference damage input, used until #31's ``FXGetDamageValue`` port lands.

``statistics.compute_power_dps`` raises E15 because the faithful per-power damage port is
issue #31. This module is the explicit stand-in: it reads the numbers Mids itself computed
(``enhanced_powers.json``: ``DamagePerActivation``, buffed ``CastTime`` / ``RechargeTime`` /
``EndCost``, buffed effect magnitudes) and joins them with the structural fields of
``powers_effects.json`` (``PowerType``, ``ActivatePeriod``, ``StatInclude``, and each effect's
``ToWho``). Nothing here is computed by the port; replace this module's source of damage with
the port when #31 lands.

**Novel to the port — no Mids parity reference.** Mids has no notion of a "self damage buff"
or "target resistance debuff" summary per power; those are folded from the effect lists for
the chain planner (#67).

Effect fields used. ``enhanced_powers.json`` effects carry ``Index``, ``EffectType``,
``DamageType``, ``Math_Mag`` (the enhanced, post-ED magnitude) and ``Math_Duration``;
``powers_effects.json`` effects carry the same ``Index`` plus ``ToWho`` and ``Scale`` (the
unenhanced scale, deliberately unused). Effects are joined on ``Index``.
"""

from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from types import MappingProxyType
from typing import Any

from coh_engine.attack_profile import read_json
from coh_engine.maths import f32

#: The eight core damage types; Mids' positional types (Melee, Ranged, AoE, Special) are excluded.
_CORE_TYPES = frozenset({"Smashing", "Lethal", "Fire", "Cold", "Energy", "Negative", "Toxic", "Psionic"})

_FILES = ("enhanced_powers.json", "powers_effects.json")


@dataclass(frozen=True, slots=True)
class PowerDamageRef:
    """One power's Mids-computed damage inputs.

    Attributes:
        full_name: Power full name, e.g. ``Scrapper_Melee.Dark_Melee.Soul_Drain``.
        damage_per_activation: Mids ``DamagePerActivation`` (0.0 for non-damaging powers).
        cast_time: Buffed cast time in seconds.
        recharge_time: Buffed recharge time in seconds.
        end_cost: Buffed endurance cost.
        activate_period: Toggle / auto tick period in seconds (0.0 for clicks).
        power_type: Mids power type, e.g. ``Click``, ``Toggle``, ``Auto_``.
        stat_include: Whether the power's buffs feed the build totals.
        self_damage_buff: Self damage buff as a fraction (0.25 is +25%), 0.0 if none.
        self_damage_buff_duration: Seconds the self damage buff lasts, 0.0 if none.
        target_res_debuff: Fraction of target resistance removed (positive), 0.0 if none.
        target_res_debuff_duration: Seconds the resistance debuff lasts, 0.0 if none.
    """

    full_name: str
    damage_per_activation: float
    cast_time: float
    recharge_time: float
    end_cost: float
    activate_period: float
    power_type: str
    stat_include: bool
    self_damage_buff: float
    self_damage_buff_duration: float
    target_res_debuff: float
    target_res_debuff_duration: float


def _read(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = read_json(path, "E43")
    return records


def _fold(
    full_name: str, enhanced: list[dict[str, Any]], structural: list[dict[str, Any]]
) -> tuple[float, float, float, float]:
    """Return ``(buff, buff_duration, debuff, debuff_duration)`` for one power.

    Self ``DamageBuff`` magnitudes are summed per core damage type (a power can list several
    stacking groups, e.g. Soul Drain's +10% and +50%), then the largest per-type total is the
    power's buff: Aim-style powers list one equal effect per type, so max is that shared value.
    Target ``Resistance`` magnitudes are negative; the debuff is the weakest (closest to zero)
    per-type total across the types listed, so a debuff that misses a core type reports no debuff for it.
    """
    by_index = {e["Index"]: e for e in enhanced}
    buff: dict[str, tuple[float, float]] = {}
    debuff: dict[str, tuple[float, float]] = {}
    for fx in structural:
        kind, who, dtype = fx["EffectType"], fx["ToWho"], fx["DamageType"]
        if dtype not in _CORE_TYPES or (kind, who) not in (("DamageBuff", "Self"), ("Resistance", "Target")):
            continue
        match = by_index.get(fx["Index"])
        if match is None:
            raise ValueError(
                f"E42: {full_name} effect Index {fx['Index']} ({kind} {dtype}) is in powers_effects.json "
                "but not enhanced_powers.json; re-dump both files from the same build"
            )
        mag, dur = f32(match["Math_Mag"]), f32(match["Math_Duration"])
        table = buff if kind == "DamageBuff" else debuff
        old = table.get(dtype, (0.0, 0.0))
        table[dtype] = (old[0] + mag, max(old[1], dur))
    buff_mag, buff_dur = max(buff.values(), default=(0.0, 0.0))
    # Debuffs: only count when every core type is covered (an all-type -Res like Venom Grenade).
    if len(debuff) == len(_CORE_TYPES):
        deb_mag, deb_dur = max(debuff.values(), key=lambda v: v[0])
        deb_mag = max(0.0, -deb_mag)
    else:
        deb_mag, deb_dur = 0.0, 0.0
    return buff_mag, buff_dur, deb_mag, deb_dur


def load_damage_refs(build_dir: Path | str) -> Mapping[str, PowerDamageRef]:
    """Load a Mids dump build directory into per-power damage references.

    Args:
        build_dir: Directory holding ``enhanced_powers.json`` and ``powers_effects.json``.

    Returns:
        Read-only mapping of power full name to :class:`PowerDamageRef`.

    Raises:
        ValueError: E40 a file is missing; E41 a ``BuildIndex`` is in only one file; E42 an effect is
            in ``powers_effects.json`` but not ``enhanced_powers.json``; E43 a file is not valid JSON.
    """
    root = Path(build_dir)
    for name in _FILES:
        if not (root / name).is_file():
            raise ValueError(f"E40: {root / name} not found; point build_dir at a Mids dump build directory")
    # Joined on BuildIndex, not FullName: Mids dumps a redirected power (Arsenal Sniper Rifle) under
    # its redirect target's name in enhanced_powers.json but under the picked name in powers_effects.json.
    enhanced = {p["BuildIndex"]: p for p in _read(root / _FILES[0])}
    structural = {p["BuildIndex"]: p for p in _read(root / _FILES[1])}
    if enhanced.keys() != structural.keys():
        only_e = sorted(enhanced.keys() - structural.keys())
        only_s = sorted(structural.keys() - enhanced.keys())
        raise ValueError(
            f"E41: BuildIndex differs between dump files (only in enhanced_powers.json: {only_e}; "
            f"only in powers_effects.json: {only_s}); re-dump both files from the same build"
        )
    refs: dict[str, PowerDamageRef] = {}
    for index, e in enhanced.items():
        s = structural[index]
        name = s["FullName"]
        buff, buff_dur, deb, deb_dur = _fold(name, e["Effects"], s["Effects"])
        refs[name] = PowerDamageRef(
            full_name=name,
            damage_per_activation=f32(e["DamagePerActivation"]),
            cast_time=f32(e["CastTime"]),
            recharge_time=f32(e["RechargeTime"]),
            end_cost=f32(e["EndCost"]),
            activate_period=f32(s["ActivatePeriod"]),
            power_type=s["PowerType"],
            stat_include=s["StatInclude"],
            self_damage_buff=buff,
            self_damage_buff_duration=buff_dur,
            target_res_debuff=deb,
            target_res_debuff_duration=deb_dur,
        )
    return MappingProxyType(refs)
