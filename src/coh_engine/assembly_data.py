"""The harness export power assembly reads: the powers it pulls effects from, and the build's own powers.

Three files feed it (``.claude/plans/assembly-seams.md`` § Seam 1):

- ``assembly_powers.json``, database wide: each enhancement's own power and every power reachable
    through a GrantPower or ExecutePower effect, with its effects in the extended record shape.
- ``builds/<name>/power_assembly.json``: the build powers' assembly fields and the extended keys of
    their effects, matched to ``powers_effects.json`` by build index and effect index.
- ``builds/<name>/slots.json``: ``ProcInclude`` per build power.

``builds/<name>/assembled_powers.json`` holds Mids' own assembled effect lists, the reference the
assembly step is tested against.

Every loader refuses a record missing a key it needs (``E85``) and a pointer to a record the file does not
hold (``E86``), rather than defaulting it.
"""

import json
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass, replace
from pathlib import Path
from types import MappingProxyType
from typing import Any

from coh_engine.effect import Effect, Power, _assembly_fields, _parse_effect
from coh_engine.maths import f32

# The keys an extended effect record carries: the powers_effects.json record plus the assembly keys.
EFFECT_KEYS = (
    "Index",
    "EffectType",
    "DamageType",
    "MezType",
    "ETModifies",
    "Scale",
    "nMagnitude",
    "nDuration",
    "AttribType",
    "Aspect",
    "ModifierTable",
    "nModifierTable",
    "ToWho",
    "PvMode",
    "Stacking",
    "Suppression",
    "Buffable",
    "Resistible",
    "IgnoreED",
    "IgnoreScaling",
    "VariableModified",
    "BaseProbability",
    "Probability",
    "ProcsPerMinute",
    "Ticks",
    "DelayedTime",
    "EffectClass",
    "SpecialCase",
    "nIDClassName",
    "Absorbed_Effect",
    "Absorbed_PowerType",
    "Absorbed_Class_nID",
    "ActiveConditionalsCount",
    "ExprMagnitude",
    "ExprDuration",
    "ExprProbability",
    "DisplayPercentage",
    *(
        EXTENSION_KEYS := (
            "Summon",
            "nSummon",
            "Duration",
            "EffectId",
            "Reward",
            "ActiveConditionals",
            "IsEnhancementEffect",
            "Absorbed_Power_nID",
        )
    ),
)
_FLAG_KEYS = ("HasGrantPowerEffect", "HasPowerOverrideEffect", "AbsorbSummonEffects", "AbsorbSummonAttributes")
_POWER_KEYS = (
    "Nid",
    "FullName",
    "PowerType",
    "PowerSetType",
    "EntitiesAffected",
    "EntitiesAutoHit",
    "ActivatePeriod",
    "RechargeTime",
    "BaseRechargeTime",
    "CastTime",
    "AoEModifier",
    "Enhancements",
    *_FLAG_KEYS,
    "Effects",
)
_BUILD_POWER_KEYS = (
    "BuildIndex",
    "EntitiesAffected",
    "EntitiesAutoHit",
    "PowerSetType",
    "BaseRechargeTime",
    "AoEModifier",
    *_FLAG_KEYS,
    "SubPowers",
    "Effects",
)


@dataclass(frozen=True, slots=True)
class AssemblyPower:
    """A power as assembly reads it: the fields that steer absorption, and its effects."""

    nid: int
    full_name: str
    power_type: str
    power_set_type: str
    entities_affected: frozenset[str]
    entities_auto_hit: frozenset[str]
    activate_period: float
    recharge_time: float
    base_recharge_time: float
    cast_time: float
    aoe_modifier: float
    enhancements: frozenset[int]
    has_grant_power_effect: bool
    has_power_override_effect: bool
    absorb_summon_effects: bool
    absorb_summon_attributes: bool
    effects: tuple[Effect, ...]


@dataclass(frozen=True, slots=True)
class BuildPowerAssembly:
    """One build power ready for assembly: its own record, the entry's sub-powers and its ProcInclude flag."""

    build_index: int
    proc_include: bool
    sub_powers: tuple[tuple[int, bool], ...]  # (power nid, stat include)
    power: AssemblyPower


def _require(record: Mapping[str, Any], keys: Iterable[str], where: str) -> None:
    missing = [key for key in keys if key not in record]
    if missing:
        raise ValueError(
            f"E85: {where} is missing {missing}; expected every key of the assembly export record; "
            "re-run the Mids dump harness so the file carries them"
        )


def _parse_extended_effect(raw: Mapping[str, Any], where: str) -> Effect:
    _require(raw, EFFECT_KEYS, f"{where} effect {raw.get('Index', '?')}")
    return _parse_effect(dict(raw))


def _power_fields(raw: Mapping[str, Any]) -> dict[str, Any]:
    """The assembly fields both a database power and a build power record carry."""
    return {
        "power_set_type": raw["PowerSetType"],
        "entities_affected": frozenset(raw["EntitiesAffected"]),
        "entities_auto_hit": frozenset(raw["EntitiesAutoHit"]),
        "base_recharge_time": f32(raw["BaseRechargeTime"]),
        "aoe_modifier": f32(raw["AoEModifier"]),
        "has_grant_power_effect": raw["HasGrantPowerEffect"],
        "has_power_override_effect": raw["HasPowerOverrideEffect"],
        "absorb_summon_effects": raw["AbsorbSummonEffects"],
        "absorb_summon_attributes": raw["AbsorbSummonAttributes"],
    }


def _load(path: Path | str) -> Any:
    with open(path, encoding="utf-8") as stream:
        return json.load(stream)


def load_assembly_powers(path: Path | str) -> Mapping[int, AssemblyPower]:
    """Parse ``assembly_powers.json``, keyed by database index.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
        ValueError: ``E85`` for a record missing a key.
    """
    out: dict[int, AssemblyPower] = {}
    for raw in _load(path):
        where = f"{Path(path).name} power {raw.get('FullName', raw.get('Nid', '?'))}"
        _require(raw, _POWER_KEYS, where)
        out[raw["Nid"]] = AssemblyPower(
            nid=raw["Nid"],
            full_name=raw["FullName"],
            power_type=raw["PowerType"],
            activate_period=f32(raw["ActivatePeriod"]),
            recharge_time=f32(raw["RechargeTime"]),
            cast_time=f32(raw["CastTime"]),
            enhancements=frozenset(raw["Enhancements"]),
            effects=tuple(_parse_extended_effect(fx, where) for fx in raw["Effects"]),
            **_power_fields(raw),
        )
    return MappingProxyType(out)


def _enrich(power: Power, raw_effects: Sequence[Mapping[str, Any]], where: str) -> tuple[Effect, ...]:
    """The power's effects with the extended keys from its ``power_assembly.json`` record, matched by index."""
    by_index = {}
    for raw in raw_effects:
        _require(raw, ("Index", *EXTENSION_KEYS), f"{where} effect {raw.get('Index', '?')}")
        by_index[raw["Index"]] = raw
    missing = [fx.index for fx in power.effects if fx.index not in by_index]
    if missing:
        raise ValueError(
            f"E85: {where} has no extended keys for effect(s) {missing} of {power.full_name}; expected one entry "
            "per effect of powers_effects.json; re-run the Mids dump harness"
        )
    return tuple(
        replace(
            fx,
            is_enhancement_effect=by_index[fx.index]["IsEnhancementEffect"],
            **_assembly_fields(by_index[fx.index]),
        )
        for fx in power.effects
    )


def load_build_power_assembly(
    power_assembly_path: Path | str, slots_path: Path | str, powers: Sequence[Power]
) -> Mapping[int, BuildPowerAssembly]:
    """Join a build's ``power_assembly.json`` and ``slots.json`` onto its ``powers_effects.json`` powers.

    Raises:
        FileNotFoundError: if either path does not exist.
        ValueError: ``E85`` for a record missing a key or an effect missing its extended keys; ``E86`` for a
            build power with no ``power_assembly.json`` or ``slots.json`` record.
    """
    name = Path(power_assembly_path).name
    records = {}
    for raw in _load(power_assembly_path):
        _require(raw, _BUILD_POWER_KEYS, f"{name} build power {raw.get('BuildIndex', '?')}")
        records[raw["BuildIndex"]] = raw
    proc_include = {}
    for raw in _load(slots_path):
        _require(
            raw, ("BuildIndex", "ProcInclude"), f"{Path(slots_path).name} build power {raw.get('BuildIndex', '?')}"
        )
        proc_include[raw["BuildIndex"]] = raw["ProcInclude"]
    out: dict[int, BuildPowerAssembly] = {}
    for power in powers:
        for source, table in ((name, records), (Path(slots_path).name, proc_include)):
            if power.build_index not in table:
                raise ValueError(
                    f"E86: {source} has no record for build power {power.build_index} ({power.full_name}); "
                    "expected one per power of powers_effects.json; re-run the Mids dump harness"
                )
        raw = records[power.build_index]
        where = f"{name} build power {power.build_index}"
        out[power.build_index] = BuildPowerAssembly(
            build_index=power.build_index,
            proc_include=proc_include[power.build_index],
            sub_powers=tuple((sub["NIDPower"], sub["StatInclude"]) for sub in raw["SubPowers"]),
            power=AssemblyPower(
                nid=power.nid_power,
                full_name=power.full_name,
                power_type=power.power_type,
                activate_period=power.activate_period,
                recharge_time=power.recharge_time,
                cast_time=power.cast_time,
                enhancements=frozenset(power.enhancements),
                effects=_enrich(power, raw["Effects"], where),
                **_power_fields(raw),
            ),
        )
    return MappingProxyType(out)


def load_assembled_powers(path: Path | str) -> Mapping[int, tuple[Effect, ...]]:
    """Parse ``assembled_powers.json``: Mids' assembled effect list per StatInclude build power, by build index.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
        ValueError: ``E85`` for a record missing a key.
    """
    out: dict[int, tuple[Effect, ...]] = {}
    for raw in _load(path):
        where = f"{Path(path).name} build power {raw.get('BuildIndex', '?')}"
        _require(raw, ("BuildIndex", "FullName", "Effects"), where)
        out[raw["BuildIndex"]] = tuple(_parse_extended_effect(fx, where) for fx in raw["Effects"])
    return MappingProxyType(out)
