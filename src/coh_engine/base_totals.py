"""Per-attribute totals for a build, with slotted-enhancement values.

Ports the totals pipeline of MidsReborn: the buff pass of
``CalculateAndApplyEffects`` (``clsToonX.cs:507-832``) feeding ``_selfBuffs`` and
``GBD_Totals`` (``clsToonX.cs:839-1002``) folding it into ``Totals`` and
``TotalsCapped``.

Enhancement path: when slot + enhancement data are supplied,
:func:`_compute_enh_multipliers` builds each effect's enhancement multiplier
``1 + ED(Σ slot values)`` (Pass1 aggregate → Pass2 ED → Pass4 ++), and the buff
pass reads that enhanced ``BuffedMag`` (``base Mag x multiplier``) — Pass5's
multiply folded into the sum. With no slots every multiplier is 1 and
``BuffedMag`` collapses to raw ``Mag`` (the empty-slot path). The
``_selfEnhance`` accumulator (global enhancement buffs via
``GetEnhancementMagSum``) is added to each effect's multiplier after ED.

Semantics ported verbatim from the C# control flow, including the quirks:

- ``BuffsX.Effect[]`` is written by ``eEffectType`` ordinal and read back by
    ``eStatType`` ordinal (the shared entries coincide; ``enums.json`` is the
    source of both orderings).
- Generic (``DamageType == None``) Defense/Resistance effects fall through to
    ``Effect[eEffectType.*]``, which ``GBD_Totals`` never reads — only typed
    effects reach ``Totals.Def``/``Res``.
- ``MezRes``/``DebuffRes`` are scaled ``* 100`` at copy; ``Def``/``Res`` stay
    fractional.
- The Absorb %-of-HP normalization reads the *previous* recompute's
    ``Totals.HPMax`` (zero on a fresh headless compute).

Conditional stance: effects carrying ``ActiveConditionals`` or a
``SpecialCase`` are excluded (``ValidateConditional`` and the ``CanInclude``
character-state switch are unported leaves). This is a *known divergence* —
Mids' ``CanInclude`` keeps many SpecialCase effects that are true in the
default character state (Stalker ``Hidden``, Dual Blades ``ComboLevel0``,
``NotDisintegrated``, ...), so a build with such an effect feeding a read
bucket will under-report until the switch is ported. It is safe only
where no excluded effect reaches a bucket ``GBD_Totals`` reads; the
``test_excluded_conditionals_do_not_affect_totals`` parity test enforces that
over the committed fixtures.

Spec: docs/engine/mids-port-spec.md § effect-model, § gbpa-pass-pipeline,
§ gbd-totals-and-caps.
"""

import json
import math
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass, field, replace
from pathlib import Path
from types import MappingProxyType

from coh_engine.archetypes import Archetype, ArchetypeDb
from coh_engine.attribmod import AttribMods
from coh_engine.effect import Effect, Power, effect_mag
from coh_engine.enh_aspects import END, fold_scalar
from coh_engine.enh_pipeline import aggregate_and_ed
from coh_engine.enhancement import EnhancementRecord, SlotRef
from coh_engine.incarnate import Incarnate, IncarnateAddends, resolve_incarnates
from coh_engine.maths import MathTables, f32
from coh_engine.set_bonuses import (
    SET_BONUS_BUILD_INDEX,
    SetBonusDb,
    build_set_bonus_power,
    validate_set_slotting,
)

_SPEED_EFFECT_TYPES = ("SpeedFlying", "SpeedRunning", "SpeedJumping")

# Static: the eStatType MaxXSpeed bucket -> the eEffectType speed it sums from.
_MAX_SPEED_SOURCES = {
    "MaxRunSpeed": "SpeedRunning",
    "MaxJumpSpeed": "SpeedJumping",
    "MaxFlySpeed": "SpeedFlying",
}


@dataclass(frozen=True, slots=True)
class EnumMaps:
    """Name -> ordinal maps for the enums Mids indexes arrays by.

    ``inverse`` holds the ordinal -> name reverse of each map, built once at
    construction so the buff pass does not rebuild it per power.
    """

    maps: Mapping[str, Mapping[str, int]]
    inverse: Mapping[str, Mapping[int, str]] = field(init=False)

    def __post_init__(self) -> None:
        """Build and freeze the ordinal -> name inverse of every enum map."""
        object.__setattr__(
            self,
            "inverse",
            MappingProxyType({name: MappingProxyType({v: k for k, v in m.items()}) for name, m in self.maps.items()}),
        )

    def size(self, enum_name: str) -> int:
        """Array length for ``enum_name`` — C# ``Enum.GetValues<T>().Length``."""
        return len(self.maps[enum_name])


def load_enum_maps(path: Path | str) -> EnumMaps:
    """Parse an ``enums.json`` Mids reference dump.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        raw = json.load(stream)
    return EnumMaps(maps=MappingProxyType({name: MappingProxyType(dict(members)) for name, members in raw.items()}))


@dataclass(frozen=True, slots=True)
class EngineConfig:
    """The MidsContext.Config state the reference totals were computed under."""

    suppression: int
    disable_pve: bool
    force_level: int
    scaling_to_hit: float


def load_engine_config(path: Path | str) -> EngineConfig:
    """Parse a ``config.json`` Mids reference dump.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        raw = json.load(stream)
    return EngineConfig(
        suppression=raw["Suppression"],
        disable_pve=raw["DisablePvE"],
        force_level=raw["ForceLevel"],
        scaling_to_hit=f32(raw["ScalingToHit"]),
    )


@dataclass(frozen=True, slots=True)
class ServerData:
    """Server-wide constants (``Core/ServerData.cs``), from the reference dump."""

    base_to_hit: float
    max_slots: int
    enable_inherent_slotting: bool
    base_fly_speed: float
    base_jump_speed: float
    base_jump_height: float
    base_perception: float
    base_run_speed: float
    max_fly_speed: float
    max_jump_speed: float
    max_jump_height: float
    max_run_speed: float
    max_max_fly_speed: float
    max_max_jump_speed: float
    max_max_run_speed: float


def load_server_data(path: Path | str) -> ServerData:
    """Parse a ``server_data.json`` Mids reference dump.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        raw = json.load(stream)
    return ServerData(
        base_to_hit=f32(raw["BaseToHit"]),
        max_slots=raw["MaxSlots"],
        enable_inherent_slotting=raw["EnableInherentSlotting"],
        base_fly_speed=f32(raw["BaseFlySpeed"]),
        base_jump_speed=f32(raw["BaseJumpSpeed"]),
        base_jump_height=f32(raw["BaseJumpHeight"]),
        base_perception=f32(raw["BasePerception"]),
        base_run_speed=f32(raw["BaseRunSpeed"]),
        max_fly_speed=f32(raw["MaxFlySpeed"]),
        max_jump_speed=f32(raw["MaxJumpSpeed"]),
        max_jump_height=f32(raw["MaxJumpHeight"]),
        max_run_speed=f32(raw["MaxRunSpeed"]),
        max_max_fly_speed=f32(raw["MaxMaxFlySpeed"]),
        max_max_jump_speed=f32(raw["MaxMaxJumpSpeed"]),
        max_max_run_speed=f32(raw["MaxMaxRunSpeed"]),
    )


@dataclass(slots=True)
class TotalStatistics:
    """Mirror of ``Character.TotalStatistics`` (``Character.cs:1857``)."""

    def_: list[float] = field(default_factory=list)
    res: list[float] = field(default_factory=list)
    mez: list[float] = field(default_factory=list)
    mez_res: list[float] = field(default_factory=list)
    debuff_res: list[float] = field(default_factory=list)
    elusivity: list[float] = field(default_factory=list)
    hp_regen: float = 0.0
    hp_max: float = 0.0
    absorb: float = 0.0
    end_rec: float = 0.0
    end_use: float = 0.0
    end_max: float = 0.0
    run_spd: float = 0.0
    max_run_spd: float = 0.0
    jump_spd: float = 0.0
    max_jump_spd: float = 0.0
    fly_spd: float = 0.0
    max_fly_spd: float = 0.0
    jump_height: float = 0.0
    stealth_pve: float = 0.0
    stealth_pvp: float = 0.0
    threat_level: float = 0.0
    perception: float = 0.0
    buff_haste: float = 0.0
    buff_acc: float = 0.0
    buff_to_hit: float = 0.0
    buff_dam: float = 0.0
    buff_end_rdx: float = 0.0
    buff_range: float = 0.0
    buff_heal: float = 0.0


@dataclass(frozen=True, slots=True)
class GlobalEnhance:
    """The global ``_selfEnhance`` scalar terms Pass 3 folds into every per-power scalar.

    ``GBPA_Pass3_EnhancePostED`` (clsToonX.cs:1924) adds each of these to the matching
    per-power multiplier before the ``+1`` and the divide. CP5 folded only
    ``end_discount`` into toggle ``EndUse``; the derived-stat layer
    (:mod:`coh_engine.statistics`) folds ``recharge``, ``end_discount``, and
    ``interrupt`` into per-power scalars. ``recharge`` is ``_selfEnhance.Effect[Haste]``
    (== ``eEffectType.RechargeTime``, the same index the character ``BuffHaste`` reads).

    ``accuracy`` and ``range`` are the complete Pass-3 global surface, reserved for the
    per-aspect enhancement handler (the accuracy fold feeds to-hit / DPS in CP6.2, the
    range fold multiplies per-power range). They are populated but not yet consumed —
    kept so the struct is the whole set of global scalars, not a partial one.
    """

    recharge: float
    end_discount: float
    accuracy: float
    interrupt: float
    range: float


@dataclass(frozen=True, slots=True)
class BaseTotals:
    """Result pair: uncapped ``Totals`` and AT/server-capped ``TotalsCapped``, plus globals.

    ``incarnate_addends`` carries the resolved incarnate (Alpha-slot) pre/post-ED
    contributions (:class:`~coh_engine.incarnate.IncarnateAddends`); the effect-mag half is
    already folded into these totals, and the scalar half is threaded to the per-power
    derived-stat layer (:mod:`coh_engine.statistics`). ``None`` when no incarnate was
    supplied.
    """

    totals: TotalStatistics
    totals_capped: TotalStatistics
    global_enhance: GlobalEnhance
    incarnate_addends: IncarnateAddends | None = None


@dataclass(slots=True)
class _BuffsX:
    """Working accumulator — ``Enums.BuffsX`` (``Enums.cs:1906``), current subset.

    ``effect_aux`` is the debuff half of the enhancement pass's speed-scalar split.
    """

    effect: list[float]
    effect_aux: list[float]
    mez: list[float]
    mez_res: list[float]
    damage: list[float]
    defense: list[float]
    resistance: list[float]
    elusivity: list[float]
    status_protection: list[float]
    status_resistance: list[float]
    debuff_resistance: list[float]
    boosts: list[float]
    boosts_mez: list[float]
    max_end: float


def _new_buffs(enums: EnumMaps) -> _BuffsX:
    n_effect = enums.size("eEffectType")
    n_mez = enums.size("eMez")
    n_damage = enums.size("eDamage")
    return _BuffsX(
        effect=[0.0] * n_effect,
        effect_aux=[0.0] * n_effect,
        mez=[0.0] * n_mez,
        mez_res=[0.0] * n_mez,
        damage=[0.0] * n_damage,
        defense=[0.0] * n_damage,
        resistance=[0.0] * n_damage,
        elusivity=[0.0] * n_damage,
        status_protection=[0.0] * n_mez,
        status_resistance=[0.0] * n_mez,
        debuff_resistance=[0.0] * n_effect,
        boosts=[0.0] * n_effect,
        boosts_mez=[0.0] * n_mez,
        max_end=0.0,
    )


@dataclass(frozen=True, slots=True)
class _MagContext:
    """The shared lookup context every magnitude computation needs.

    ``enh_mult`` maps ``(power.build_index, effect.index)`` to the enhancement
    multiplier ``1 + ED(Σ slot values)`` for that effect. Absent keys mean an
    unenhanced effect (multiplier 1) — the fallback when no slots/enhancement
    data are supplied.
    """

    mods: AttribMods
    classes: ArchetypeDb
    archetype_index: int
    config: EngineConfig
    enh_mult: Mapping[tuple[int, int], float] = field(default_factory=dict)
    enhance_names: frozenset[str] = frozenset()
    enums: EnumMaps | None = None


def _can_include(fx: Effect) -> bool:
    """Partial port of ``Effect.CanInclude`` (``Effect.cs:1856+``).

    True only for unconditional effects. Effects with ``ActiveConditionals``
    or a ``SpecialCase`` need the character-state evaluation leaves that are
    not ported yet, so they are excluded. This diverges from Mids, which keeps
    many default-true SpecialCase effects (see the module docstring); the
    ``test_excluded_conditionals_do_not_affect_totals`` gate confirms the
    committed fixtures have no such effect reaching a read bucket.
    """
    return fx.active_conditionals_count == 0 and fx.special_case == "None"


def _pvx_include(fx: Effect, archetype_index: int, config: EngineConfig) -> bool:
    """``Effect.PvXInclude`` (``Effect.cs:2610-2616``) with a non-null archetype."""
    pv_ok = (fx.pv_mode != "PvP" and not config.disable_pve) or (fx.pv_mode != "PvE" and config.disable_pve)
    class_ok = fx.n_id_class_name == -1 or fx.n_id_class_name == archetype_index
    return pv_ok and class_ok


def _get_effect_mag_sum(
    power: Power,
    i_effect: str,
    ctx: _MagContext,
    *,
    include_delayed: bool = False,
    only_self: bool = False,
    only_target: bool = False,
    max_mode: bool = False,
) -> list[tuple[Effect, float]]:
    """``Power.GetEffectMagSum`` (``Power.cs:1464-1522``) as (effect, mag) pairs.

    With no enhancements ``BuffedMag`` falls back to raw ``Mag``, so each pair
    carries ``Effect.Mag`` (ticks-multiplied for stacking DoTs).
    """
    out: list[tuple[Effect, float]] = []
    for fx in power.effects:
        flag = (
            (fx.to_who == "Target" and not only_self) or (fx.to_who == "Self" and not only_target) or fx.to_who == "All"
        )
        if i_effect in _SPEED_EFFECT_TYPES and not max_mode and fx.aspect == "Max":
            flag = False
        if ctx.config.suppression & fx.suppression:
            flag = False
        if (
            not flag
            or fx.probability <= 0
            or (max_mode and fx.aspect != "Max")
            or fx.effect_type != i_effect
            or fx.effect_class in ("Ignored", "Special")
            or (fx.delayed_time > 5 and not include_delayed)
            or not _can_include(fx)
            or not _pvx_include(fx, ctx.archetype_index, ctx.config)
        ):
            continue
        # BuffedMag: the enhanced Math_Mag (base Mag x the enhancement
        # multiplier) that GetEffectMagSum sums. With no enhancement the
        # multiplier is absent and this falls back to raw Mag, exactly as
        # BuffedMag falls back when Math_Mag ≈ 0 (Effect.cs:405). The ticks/
        # stacking DoT multiply happens after, matching Power.cs:1513-1516.
        mag = _mag(fx, power, ctx)
        mult = ctx.enh_mult.get((power.build_index, fx.index))
        if mult is not None:
            mag = f32(mag * mult)
        if fx.ticks > 1 and fx.stacking == "Yes":
            mag = f32(mag * fx.ticks)
        out.append((fx, mag))
    return out


def _mag(fx: Effect, power: Power, ctx: _MagContext) -> float:
    return effect_mag(fx, power, ctx.mods, ctx.classes, ctx.archetype_index)


def _enhance_names(enums: EnumMaps) -> frozenset[str]:
    """The ``eEnhance`` value names (minus ``None``) — the Pass1 name-identity route set."""
    return frozenset(enums.maps["eEnhance"]) - {"None"}


def _enhance_aspect(fx: Effect, enhance_names: frozenset[str]) -> str | None:
    """The ``eEnhance`` aspect whose slotted value enhances a Buffable effect's magnitude (``GBPA_Pass1_EnhancePreED``).

    An effect is enhanced under the ``eEnhance`` aspect whose *name* equals its ``EffectType`` name,
    plus the special cases the name match misses — ``Enhancement`` + ``ETModifies == Accuracy →
    Accuracy`` and ``ResEffect`` + ``ETModifies == Defense → Defense``. Every other effect reaches a
    slot only through that enhancement's allowed-effects list, and of those only the Heal
    enhancement's ``ResEffect`` + ``ETModifies == Regeneration`` is given a value. A Special damage
    effect and a duration-typed Mez effect gain duration or nothing, never magnitude. ``None`` means
    the slots leave the magnitude unenhanced.
    """
    if not fx.buffable:
        return None
    if fx.effect_type == "Damage" and fx.damage_type == "Special":
        return None
    if fx.effect_type == "Mez" and fx.attrib_type == "Duration":
        return None
    if fx.effect_type in enhance_names:
        return fx.effect_type
    if fx.effect_type == "Enhancement" and fx.et_modifies == "Accuracy":
        return "Accuracy"
    if fx.effect_type == "ResEffect" and fx.et_modifies == "Defense":
        return "Defense"
    if _is_allowed_list_only(fx):
        return "Heal"
    return None


def _is_allowed_list_only(fx: Effect) -> bool:
    """The one effect enhanced only through an enhancement's allowed-effects list, which ED never reaches."""
    return fx.effect_type == "ResEffect" and fx.et_modifies == "Regeneration" and fx.mez_type == "None"


# Effect types Pass 3 folds into the power's scalars instead of into each effect.
_PASS3_SCALAR_TYPES = frozenset({"Accuracy", "EnduranceDiscount", "InterruptTime", "Range", "RechargeTime"})
_PASS3_SPEED_TYPES = frozenset({"SpeedRunning", "SpeedJumping", "JumpHeight", "SpeedFlying"})


def _global_enhancement_term(fx: Effect, base_mag: float, self_enhance: _BuffsX | None, enums: EnumMaps) -> float:
    """The build-wide enhancement buff Pass 3 adds to one effect's enhancement after ED (``_selfEnhance``)."""
    if self_enhance is None or not fx.buffable or fx.effect_type in _PASS3_SCALAR_TYPES:
        return 0.0
    e_damage = enums.maps["eDamage"]
    match fx.effect_type:
        case "Damage":
            return self_enhance.damage[e_damage[fx.damage_type]]
        case "Defense":
            return self_enhance.defense[e_damage[fx.damage_type]]
        case "Mez":
            return 0.0 if fx.attrib_type == "Duration" else self_enhance.mez[enums.maps["eMez"][fx.mez_type]]
        case "Resistance":
            return self_enhance.resistance[e_damage[fx.damage_type]]
    e_type = enums.maps["eEffectType"]
    if fx.effect_type == "Enhancement" and fx.et_modifies in _PASS3_SPEED_TYPES:
        speed = fx.et_modifies
    elif fx.effect_type in _PASS3_SPEED_TYPES:
        speed = fx.effect_type
    else:
        return self_enhance.effect[e_type[fx.effect_type]]
    if base_mag > 0:
        return self_enhance.effect[e_type[speed]]
    if base_mag < 0:
        return self_enhance.effect_aux[e_type[speed]]
    return 0.0


def _effect_multiplier(
    power: Power,
    fx: Effect,
    power_slots: Sequence[SlotRef],
    memo: dict[tuple[str, int, bool, bool, bool, float, float, float], float],
    inc: tuple[float, float] | None,
    *,
    enh_db: Mapping[int, EnhancementRecord],
    tables: MathTables,
    force_level: int,
    ctx: _MagContext,
    self_enhance: _BuffsX | None,
) -> float | None:
    """One effect's enhancement multiplier ``1 + ED(Σ slot + incarnate pre) + global + incarnate post``.

    Slotted-only and incarnate-touched effects flow through the *same* accumulator: ``inc`` is
    the incarnate ``(pre_ed, post_ed)`` for this effect or ``None`` (treated as ``(0, 0)``, which
    is bit-identical to the slotted-only path). The global term is the build-wide enhancement buff
    (:func:`_global_enhancement_term`), added after ED and before the incarnate post-ED term.
    Returns ``None`` when the accumulator is zero — an unenhanced effect the caller records as an
    absent key (multiplier 1). Pass1 gates on the pre-Pass5 base ``Math_Mag`` — the unenhanced base
    magnitude used here — so the sign key matches Mids' gate.
    """
    assert ctx.enums is not None
    aspect = _enhance_aspect(fx, ctx.enhance_names)
    base_mag = _mag(fx, power, ctx)
    glob = _global_enhancement_term(fx, base_mag, self_enhance, ctx.enums)
    if aspect is None and inc is None and glob == 0.0:
        return None
    # One accumulator for both the slotted-only and incarnate-touched effect: co-ED the
    # incarnate pre-ED term (0.0 when absent — bit-identical to no addend, since f32(x + 0) ==
    # x) with the power's slotted aggregate through one ApplyED, then add the global and
    # post-ED (IgnoreED) terms. The ED schedule keys on the routed aspect, or — for an effect
    # whose type is neither an eEnhance name nor a special case — on the effect's own type (its
    # slotted aggregate is 0, so only the schedule label matters).
    pre_ed, post_ed = inc if inc is not None else (0.0, 0.0)
    ed_aspect = aspect if aspect is not None else fx.effect_type
    sub_enh = ctx.enums.maps["eMez"][fx.mez_type] if ed_aspect == "Mez" else -1
    skip_ed = _is_allowed_list_only(fx)
    key = (ed_aspect, sub_enh, skip_ed, base_mag <= 0.0, base_mag >= 0.0, pre_ed, glob, post_ed)
    if key not in memo:
        aggregate = aggregate_and_ed(
            power_slots if aspect is not None else (),
            aspect=ed_aspect,
            enh_db=enh_db,
            force_level=force_level,
            tables=tables,
            sub_enh=sub_enh,
            mag=base_mag,
            pre_ed_addend=pre_ed,
            skip_ed=skip_ed,
        )
        memo[key] = f32(f32(aggregate + glob) + post_ed)
    accumulator = memo[key]
    return f32(1.0 + accumulator) if accumulator != 0.0 else None


def _compute_enh_multipliers(
    included: Sequence[Power],
    *,
    slots: Mapping[int, Sequence[SlotRef]],
    enh_db: Mapping[int, EnhancementRecord],
    tables: MathTables,
    force_level: int,
    ctx: _MagContext,
    incarnate_effect: Mapping[tuple[int, int], tuple[float, float]] = MappingProxyType({}),
    self_enhance: _BuffsX | None = None,
) -> dict[tuple[int, int], float]:
    """Per-effect enhancement multipliers ``1 + ED(Σ slot + incarnate pre) + global + incarnate post``.

    For every effect of every build power, aggregate the aspect's per-slot enhancement value plus
    any incarnate pre-ED contribution and apply ED once (Pass1→2), add the build-wide enhancement
    buffs ``self_enhance`` carries (Pass3), then the incarnate post-ED (``IgnoreED``) term (Pass4
    adds the 1). Only non-unit multipliers are stored — an absent key means an unenhanced effect.
    ``ctx.enh_mult`` is empty while this runs, so the base magnitudes fed to the buff/debuff sign
    gate are unenhanced. The set-bonus virtual power is not a build power and is never enhanced.
    """
    out: dict[tuple[int, int], float] = {}
    for power in included:
        if power.build_index == SET_BONUS_BUILD_INDEX:
            continue
        power_slots = slots.get(power.build_index, ())
        memo: dict[tuple[str, int, bool, bool, bool, float, float, float], float] = {}
        for fx in power.effects:
            mult = _effect_multiplier(
                power,
                fx,
                power_slots,
                memo,
                incarnate_effect.get((power.build_index, fx.index)),
                enh_db=enh_db,
                tables=tables,
                force_level=force_level,
                ctx=ctx,
                self_enhance=self_enhance,
            )
            if mult is not None:
                out[(power.build_index, fx.index)] = mult
    return out


@dataclass(frozen=True, slots=True)
class _BuffMaps:
    """The name->ordinal maps the buff-pass bucket routing indexes by."""

    stat: Mapping[str, int]
    e_type: Mapping[str, int]
    e_mez: Mapping[str, int]
    e_damage: Mapping[str, int]


def _apply_enhancement_boosts(
    fx: Effect, value: float, buffs: _BuffsX, m: _BuffMaps, *, include_range: bool = True
) -> None:
    """Enhancement-effect boosts: Boosts / Boosts_Mez / Range / Heal (clsToonX.cs:588-612).

    Shared by both passes. The buff pass includes the ``Range`` redirect (591-594);
    the enhancement pass calls with ``include_range=False`` — that redirect is
    ``!enhancementPass`` only.
    """
    if fx.effect_type != "Enhancement":
        return
    if fx.et_modifies == "Mez" and fx.mez_type != "None":
        _add(buffs.boosts_mez, m.e_mez[fx.mez_type], value)
    elif fx.et_modifies not in ("None", "Null", "NullBool", "Mez"):
        _add(buffs.boosts, m.e_type[fx.et_modifies], value)
    if include_range and fx.et_modifies == "Range":
        _add(buffs.effect, m.e_type["Range"], value)
    if fx.et_modifies == "Heal":
        _add(buffs.effect, m.e_type["Heal"], value)


def _apply_status_buckets(fx: Effect, value: float, buffs: _BuffsX, m: _BuffMaps) -> None:
    """Status-protection / status-resistance / debuff-resistance buckets (clsToonX.cs:611-625)."""
    if fx.effect_type == "Mez":
        _add(buffs.status_protection, m.e_mez[fx.mez_type], value)
    elif fx.effect_type == "MezResist":
        _add(buffs.status_resistance, m.e_mez[fx.mez_type], value)
    elif fx.effect_type == "ResEffect":
        _add(buffs.debuff_resistance, m.e_type[fx.et_modifies], value)


def _route_heal_endurance(fx: Effect, value: float, buffs: _BuffsX) -> bool:
    """Heal skip and MaxEnd (clsToonX.cs:705-712). True if the effect is consumed."""
    if fx.effect_type == "Heal" or (fx.effect_type == "Enhancement" and fx.et_modifies == "Heal"):
        return True
    if fx.effect_type == "Endurance" and fx.aspect == "Max":
        buffs.max_end = f32(buffs.max_end + value)
        return True
    return False


def _route_mez_etmod(fx: Effect, value: float, buffs: _BuffsX, m: _BuffMaps) -> bool:
    """Mez / MezResist expressed as ``ETModifies`` (clsToonX.cs:713-728). True if consumed."""
    if fx.effect_type != "ResEffect" and fx.et_modifies == "Mez":
        _add(buffs.mez, m.e_mez[fx.mez_type], value)
        return True
    if fx.effect_type != "ResEffect" and fx.et_modifies == "MezResist":
        _add(buffs.mez_res, m.e_mez[fx.mez_type], value)
        return True
    return False


def _route_typed_vectors(fx: Effect, value: float, buffs: _BuffsX, i_effect: str, m: _BuffMaps) -> bool:
    """Typed Defense / Resistance / Elusivity vectors (clsToonX.cs:729-742). True if consumed."""
    if i_effect == "Defense" and fx.damage_type != "None":
        _add(buffs.defense, m.e_damage[fx.damage_type], value)
        return True
    if i_effect == "Resistance" and fx.damage_type != "None":
        _add(buffs.resistance, m.e_damage[fx.damage_type], value)
        return True
    if i_effect == "Elusivity" and fx.damage_type != "None":
        _add(buffs.elusivity, m.e_damage[fx.damage_type], value)
        return True
    return False


def _route_scalar_and_speed(
    fx: Effect, value: float, buffs: _BuffsX, i_effect: str, eff_index: int, m: _BuffMaps, *, global_acc_source: bool
) -> bool:
    """Accuracy-as-ToHit, MaxSpeed caps, and ToHit (clsToonX.cs:754-798). True if consumed."""
    if fx.effect_type != "ResEffect" and fx.et_modifies == "Accuracy":
        # IsGlobalAccuracySource (clsToonX.cs:829) routes set-bonus-virtual-power /
        # GlobalBoost accuracy to BuffAcc; every ordinary power's accuracy buff acts
        # as ToHit (all in-game accuracy buffs behave as ToHit).
        _add(buffs.effect, m.stat["BuffAcc" if global_acc_source else "ToHit"], value)
        return True
    if _is_max_speed_cap(fx, "SpeedRunning"):
        _add(buffs.effect, m.stat["MaxRunSpeed"], value)
        return True
    if _is_max_speed_cap(fx, "SpeedFlying"):
        _add(buffs.effect, m.stat["MaxFlySpeed"], value)
        return True
    if _is_max_speed_cap(fx, "SpeedJumping"):
        _add(buffs.effect, m.stat["MaxJumpSpeed"], value)
        return True
    if i_effect == "ToHit":
        if not (fx.is_enhancement_effect and fx.effect_class == "Tertiary"):
            _add(buffs.effect, eff_index, value)
        return True
    return False


def _apply_effect_fallback(
    fx: Effect,
    value: float,
    buffs: _BuffsX,
    ctx: _MagContext,
    power: Power,
    prev_hp_max: float,
    eff_index: int,
    m: _BuffMaps,
) -> None:
    """Generic ``Effect[eff_index]`` add with Absorb %-of-HP and click-mirror (clsToonX.cs:799-815)."""
    if eff_index == m.stat["Absorb"] and fx.display_percentage:
        value = f32(value * prev_hp_max)
    _add(buffs.effect, eff_index, value)
    if power.power_type == "Click" and not power.click_buff:
        # C# also guards on !BuildEffectString().Contains("From Enh")
        # (clsToonX.cs:813), which only matters for enhancement-granted effects
        # (is_enhancement_effect). No committed fixture has one (the enhancement
        # path scales existing effects, not GBPA_AddEnhFX-injected ones); the
        # guard lands with that injection when it is ported.
        buffs.effect[eff_index] = f32(buffs.effect[eff_index] - _mag(fx, power, ctx))


def _route_buff_effect(
    fx: Effect,
    value: float,
    buffs: _BuffsX,
    ctx: _MagContext,
    power: Power,
    prev_hp_max: float,
    *,
    i_effect: str,
    eff_index: int,
    m: _BuffMaps,
    global_acc_source: bool,
) -> None:
    """Route one contributing sub-effect to its ``BuffsX`` bucket (clsToonX.cs:588-815).

    Order is load-bearing: the enhancement-boost and status buckets accumulate
    unconditionally, then the first matching typed/scalar router consumes the
    effect, else the generic fallback runs. ``global_acc_source`` is
    ``IsGlobalAccuracySource(power)`` — it steers Accuracy to BuffAcc vs ToHit.
    """
    _apply_enhancement_boosts(fx, value, buffs, m)
    if fx.absorbed_power_type == "GlobalBoost":
        return
    _apply_status_buckets(fx, value, buffs, m)
    if _route_heal_endurance(fx, value, buffs):
        return
    if _route_mez_etmod(fx, value, buffs, m):
        return
    if _route_typed_vectors(fx, value, buffs, i_effect, m):
        return
    if _route_scalar_and_speed(fx, value, buffs, i_effect, eff_index, m, global_acc_source=global_acc_source):
        return
    _apply_effect_fallback(fx, value, buffs, ctx, power, prev_hp_max, eff_index, m)


def _apply_buff_effects(
    power: Power,
    buffs: _BuffsX,
    ctx: _MagContext,
    *,
    enums: EnumMaps,
    prev_hp_max: float,
    global_acc_source: bool,
) -> None:
    """Buff (non-enhancement) pass of ``CalculateAndApplyEffects`` for one power.

    ``clsToonX.cs:507-832`` with ``enhancementPass == false``. Gathers each
    ``eEffectType`` bucket's contributing sub-effects and routes them via
    :func:`_route_buff_effect`. GlobalBoost powers are skipped by the caller's
    contract in C#; none exist in a dumped build's power list, so the guard is
    kept here for fidelity. ``global_acc_source`` is ``IsGlobalAccuracySource``.
    """
    if power.power_type == "GlobalBoost":
        return

    effect_type_names = enums.inverse["eEffectType"]
    m = _BuffMaps(
        stat=enums.maps["eStatType"],
        e_type=enums.maps["eEffectType"],
        e_mez=enums.maps["eMez"],
        e_damage=enums.maps["eDamage"],
    )

    for eff_index in range(len(buffs.effect)):
        i_effect = effect_type_names[eff_index]
        if i_effect == "Damage":
            continue

        if i_effect in _MAX_SPEED_SOURCES:
            pairs = _get_effect_mag_sum(power, _MAX_SPEED_SOURCES[i_effect], ctx, max_mode=True)
            self_sum = _sum_mags(_get_effect_mag_sum(power, i_effect, ctx, only_self=True))
            buffs.effect[m.stat[i_effect]] = f32(buffs.effect[m.stat[i_effect]] + self_sum)
        else:
            pairs = _get_effect_mag_sum(power, i_effect, ctx)

        for fx, value in pairs:
            if fx.to_who in ("Self", "All"):
                _route_buff_effect(
                    fx,
                    value,
                    buffs,
                    ctx,
                    power,
                    prev_hp_max,
                    i_effect=i_effect,
                    eff_index=eff_index,
                    m=m,
                    global_acc_source=global_acc_source,
                )


# Speed-scalar eEffectTypes routed buff-vs-debuff in the enhancement pass
# (clsToonX.cs:686).
_ENHANCE_SPEED_SCALARS = frozenset({"SpeedRunning", "SpeedFlying", "SpeedJumping", "JumpHeight"})


def _get_enhancement_mag_sum(power: Power, i_effect: str, ctx: _MagContext) -> list[tuple[Effect, float]]:
    """``Power.GetEnhancementMagSum(iEffect, -1)`` (Power.cs:1433-1462) as (effect, mag) pairs.

    Sums the ``Enhancement``/``DamageBuff`` effects whose ``ETModifies == i_effect``
    and that are directed at Self/All (``ToWho != Target``). This is the enhancement
    pass's gather; it reads the pre-Pass5 ``BuffedMag`` — the unenhanced base ``Mag``
    for an effect the multiplier passes have not yet scaled. ``ctx`` carries no
    enhancement multiplier here (the caller passes a base-mag context), so
    ``_mag`` returns that base value directly.
    """
    out: list[tuple[Effect, float]] = []
    for fx in power.effects:
        if (
            fx.probability <= 0
            or fx.et_modifies != i_effect
            or fx.effect_type not in ("Enhancement", "DamageBuff")
            or (fx.absorbed_effect and fx.absorbed_power_type == "GlobalBoost")
            or not _can_include(fx)
            or not _pvx_include(fx, ctx.archetype_index, ctx.config)
            or fx.to_who == "Target"
        ):
            continue
        out.append((fx, _mag(fx, power, ctx)))
    return out


def _route_enhance_main(
    fx: Effect, value: float, buffs: _BuffsX, *, i_effect: str, eff_index: int, m: _BuffMaps
) -> None:
    """Enhancement-pass main routing for a DamageBuff/Enhancement effect (clsToonX.cs:630-704).

    Every branch is terminal (the C# ``continue``). ``Accuracy``/``Heal`` are dropped
    here — Accuracy is folded by the buff pass (BuffAcc/ToHit) and Heal by the boost
    head. A speed scalar from a debuff-mode effect goes to ``EffectAux``.
    """
    etm = fx.et_modifies
    if etm == "Mez":
        _add(buffs.mez, m.e_mez[fx.mez_type], value)
        return
    if etm in ("Defense", "Resistance"):
        bucket = buffs.defense if etm == "Defense" else buffs.resistance
        if fx.damage_type != "None":
            _add(bucket, m.e_damage[fx.damage_type], value)
        else:
            _add(buffs.effect, eff_index, value)
        return
    if i_effect == "DamageBuff":
        # Defiance-tagged DamageBuffs are excluded (clsToonX.cs:666-676). The
        # ValidateConditional / SpecialCase==Defiance arms are already handled
        # upstream: _can_include drops any conditional/SpecialCase effect, so only
        # the enhancement-effect Tertiary arm can reach here.
        if not (fx.is_enhancement_effect and fx.effect_class == "Tertiary"):
            _add(buffs.damage, m.e_damage[fx.damage_type], value)
        return
    if etm in ("Accuracy", "Heal"):
        return
    if etm in _ENHANCE_SPEED_SCALARS:
        _add(buffs.effect_aux if fx.buff_mode == "Debuff" else buffs.effect, m.e_type[etm], value)
        return
    _add(buffs.effect, eff_index, value)


def _route_enhance_effect(
    fx: Effect, value: float, buffs: _BuffsX, *, i_effect: str, eff_index: int, m: _BuffMaps
) -> None:
    """Route one enhancement-pass sub-effect (clsToonX.cs:576-704, enhancementPass=true).

    Boost head accumulates unconditionally, GlobalBoost-absorbed effects are skipped,
    then the main routing consumes the effect (every branch terminal for a
    DamageBuff/Enhancement effect, so the non-enhancement tail is never reached).
    """
    _apply_enhancement_boosts(fx, value, buffs, m, include_range=False)
    if fx.absorbed_power_type == "GlobalBoost":
        return
    _route_enhance_main(fx, value, buffs, i_effect=i_effect, eff_index=eff_index, m=m)


def _apply_enhance_effects(power: Power, buffs: _BuffsX, ctx: _MagContext, *, enums: EnumMaps) -> None:
    """Enhancement pass of ``CalculateAndApplyEffects`` for one power (enhancementPass=true).

    Feeds ``_selfEnhance``: the global enhancement buffs (set-bonus recharge,
    damage, endredux; ``BuffHaste``/``BuffDam``/``BuffEndRdx``). ``DamageBuff`` is
    gathered with the normal effect-mag sum (clsToonX.cs:530); every other bucket
    uses ``GetEnhancementMagSum``. ``ctx`` must carry no enhancement multiplier
    (the pass reads pre-Pass5 base magnitudes).
    """
    if power.power_type == "GlobalBoost":
        return
    effect_type_names = enums.inverse["eEffectType"]
    m = _BuffMaps(
        stat=enums.maps["eStatType"],
        e_type=enums.maps["eEffectType"],
        e_mez=enums.maps["eMez"],
        e_damage=enums.maps["eDamage"],
    )
    for eff_index in range(len(buffs.effect)):
        i_effect = effect_type_names[eff_index]
        if i_effect == "Damage":
            continue
        if i_effect == "DamageBuff":
            pairs = _get_effect_mag_sum(power, i_effect, ctx)
        else:
            pairs = _get_enhancement_mag_sum(power, i_effect, ctx)
        for fx, value in pairs:
            if fx.to_who in ("Self", "All"):
                _route_enhance_effect(fx, value, buffs, i_effect=i_effect, eff_index=eff_index, m=m)


def _is_max_speed_cap(fx: Effect, mod: str) -> bool:
    return fx.effect_type != "ResEffect" and fx.et_modifies == mod and fx.aspect == "Max"


def _add(bucket: list[float], index: int, value: float) -> None:
    bucket[index] = f32(bucket[index] + value)


def _sum_mags(pairs: list[tuple[Effect, float]]) -> float:
    total = 0.0
    for _, mag in pairs:
        total = f32(total + mag)
    return total


def _is_global_acc_source(power: Power) -> bool:
    """``IsGlobalAccuracySource`` (clsToonX.cs:829): the set-bonus virtual power or a GlobalBoost."""
    return power.build_index == SET_BONUS_BUILD_INDEX or power.power_type == "GlobalBoost"


def compute_base_totals(
    powers: Sequence[Power],
    *,
    class_name: str,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    prev_hp_max: float = 0.0,
    slots: Mapping[int, Sequence[SlotRef]] | None = None,
    enh_db: Mapping[int, EnhancementRecord] | None = None,
    tables: MathTables | None = None,
    set_db: SetBonusDb | None = None,
    incarnates: Iterable[Incarnate] | None = None,
) -> BaseTotals:
    """Compute ``Totals``/``TotalsCapped`` for a build.

    ``powers`` are the dumped build powers; only ``stat_include`` entries
    contribute (``GBD_Totals`` preamble). ``prev_hp_max`` is the previous
    recompute's ``Totals.HPMax`` that the Absorb normalization reads — zero on
    a fresh headless compute.

    When ``slots``, ``enh_db`` and ``tables`` are all supplied, each slotted
    power's effect magnitudes are enhanced (Pass1→2→4→5 folded into the buff
    pass, which then reads ``BuffedMag``). Omit them (or leave any ``None``) for
    the empty-slot path, where every multiplier is 1.

    When ``set_db`` is supplied (with ``slots`` and ``enh_db``), the set-bonus
    virtual power is assembled (:func:`~coh_engine.set_bonuses.build_set_bonus_power`)
    and folded into both passes: the enhancement pass (``_selfEnhance``, reading
    pre-Pass5 base magnitudes) feeds ``BuffHaste``/``BuffDam``/``BuffEndRdx``; the
    buff pass folds typed Defense/Resistance/etc. and routes the virtual power's
    accuracy to ``BuffAcc`` (``IsGlobalAccuracySource``). Slotting legality is
    checked first (:func:`~coh_engine.set_bonuses.validate_set_slotting`) — an IO
    set in a power that rejects it fails loud (``H-ENH-001``) before any math runs.

    When ``incarnates`` (Alpha-slot powers, resolved from ``incarnates.json``) are
    supplied together with the enhancement inputs, their GrantPower-delivered enhancement
    is applied per target power (:func:`~coh_engine.incarnate.resolve_incarnates`): the
    effect-mag half (Defense/Recovery/…) folds into these totals here, and the resolved
    addends ride on the result for the per-power scalar layer. The ``Incarnate.*`` powers
    themselves are excluded from the buff/enhance passes — they are GrantPower delivery
    vehicles, not direct-effect powers, so their raw effects contribute nothing to Totals.

    Raises:
        ValueError: ``E09`` if ``class_name`` is unknown; ``E11`` if the
            enhancement inputs (``slots``/``enh_db``/``tables``) are supplied
            partially; ``E14`` if ``set_db`` is supplied without
            ``slots``/``enh_db``; ``E21`` if ``incarnates`` is supplied without the
            enhancement inputs; ``H-ENH-001`` if a set IO is slotted in a power that
            does not accept it.
        NotImplementedError: ``E19``/``E20`` for an unported incarnate delivery path
            (:mod:`coh_engine.incarnate`).
    """
    _enh_inputs = (slots, enh_db, tables)
    if any(x is not None for x in _enh_inputs) and not all(x is not None for x in _enh_inputs):
        raise ValueError("E11: slots, enh_db and tables must be supplied together or all omitted")

    archetype_index = classes.nid_from_uid_class(class_name)
    if archetype_index < 0:
        raise ValueError(f"E09: unknown archetype class name: {class_name!r}")
    # nid_from_uid_class returns the record's stored Index; the dump harness
    # writes Index positionally (DumpArchetypes: Index = idx), so it equals the
    # tuple position get_modifier also indexes by. This invariant holds for all
    # harness-produced dumps.
    archetype = classes.classes[archetype_index]
    # base_ctx carries no enhancement multiplier: the enhancement pass and the ED
    # gate both read pre-Pass5 base magnitudes. The buff pass reads buff_ctx (the
    # enhanced BuffedMag).
    base_ctx = _MagContext(
        mods=mods,
        classes=classes,
        archetype_index=archetype_index,
        config=config,
        enhance_names=_enhance_names(enums),
        enums=enums,
    )

    self_buffs = _new_buffs(enums)
    self_enhance = _new_buffs(enums)
    # Every StatInclude power contributes — incarnate powers included, exactly as Mids'
    # GenerateBuffData does (clsToonX.cs:2157): an Alpha's raw GrantPower/LevelShift/SetMode
    # effects are non-Buffable and contribute nothing, while a Destiny/Lore power's direct
    # effects fold in. The incarnate's GrantPower-delivered enhancement is applied separately
    # via the resolved addends.
    included = [p for p in powers if p.stat_include]

    addends = _resolve_build_incarnates(
        incarnates, powers, archetype_index=archetype_index, mods=mods, classes=classes, tables=tables
    )
    incarnate_scalar = addends.scalar if addends is not None else MappingProxyType({})

    if set_db is not None:
        if slots is None or enh_db is None:
            missing = [name for name, val in (("slots", slots), ("enh_db", enh_db)) if val is None]
            raise ValueError(
                f"E14: set_db was supplied but {' and '.join(missing)} is None; assembling the set-bonus "
                "virtual power requires slots and enh_db — pass them together with set_db"
            )
        validate_set_slotting(powers, slots, enh_db, set_db)
        virtual = build_set_bonus_power(
            powers, slots, enh_db, set_db, force_level=config.force_level, disable_pve=config.disable_pve
        )
        if virtual.effects:
            included = [*included, virtual]

    # The enhancement pass runs first: its build-wide buffs feed every effect's multiplier and
    # the buff pass. The set-bonus virtual power takes part in it and in the buff pass.
    for power in included:
        _apply_enhance_effects(power, self_enhance, base_ctx, enums=enums)

    buff_ctx = base_ctx
    toggle_end_agg: dict[int, float] = {}
    if slots is not None and enh_db is not None and tables is not None:
        enh_mult = _compute_enh_multipliers(
            included,
            slots=slots,
            enh_db=enh_db,
            tables=tables,
            force_level=config.force_level,
            ctx=base_ctx,
            incarnate_effect=addends.effect if addends is not None else MappingProxyType({}),
            self_enhance=self_enhance,
        )
        buff_ctx = replace(base_ctx, enh_mult=enh_mult)
        toggle_end_agg = _compute_toggle_end_agg(
            included,
            slots=slots,
            enh_db=enh_db,
            tables=tables,
            force_level=config.force_level,
            incarnate_scalar=incarnate_scalar,
        )

    for power in included:
        _apply_buff_effects(
            power,
            self_buffs,
            buff_ctx,
            enums=enums,
            prev_hp_max=prev_hp_max,
            global_acc_source=_is_global_acc_source(power),
        )

    result = _gbd_totals(
        included,
        self_buffs,
        self_enhance,
        archetype=archetype,
        ctx=buff_ctx,
        enums=enums,
        server=server,
        toggle_end_agg=toggle_end_agg,
        incarnate_scalar=incarnate_scalar,
    )
    return result if addends is None else replace(result, incarnate_addends=addends)


def _resolve_build_incarnates(
    incarnates: Iterable[Incarnate] | None,
    powers: Sequence[Power],
    *,
    archetype_index: int,
    mods: AttribMods,
    classes: ArchetypeDb,
    tables: MathTables | None,
) -> IncarnateAddends | None:
    """Resolve the build's incarnate addends, or ``None`` when none were supplied.

    ``tables`` is only a presence gate here: an incarnate's enhancement folds through the
    per-effect multiplier and per-power scalar path, both of which need the enhancement
    inputs, so refuse incarnates without them (``E21``). The resolver itself needs no ED
    tables — it emits raw pre/post magnitudes the consumer co-EDs.

    Raises:
        ValueError: ``E21`` if incarnates were supplied without the enhancement inputs.
    """
    if incarnates is None:
        return None
    if tables is None:
        raise ValueError(
            "E21: incarnates require the enhancement inputs (slots, enh_db, tables) — an incarnate's "
            "enhancement folds through the per-effect multiplier and per-power scalar path; pass them together"
        )
    return resolve_incarnates(incarnates, powers, mods=mods, classes=classes, archetype_index=archetype_index)


def _compute_toggle_end_agg(
    included: Sequence[Power],
    *,
    slots: Mapping[int, Sequence[SlotRef]],
    enh_db: Mapping[int, EnhancementRecord],
    tables: MathTables,
    force_level: int,
    incarnate_scalar: Mapping[tuple[int, str], tuple[float, float]] = MappingProxyType({}),
) -> dict[int, float]:
    """Per-toggle endurance-discount aggregate ``ED(Σ EndRdx + incarnate pre)`` for EndUse.

    ``EnduranceDiscount`` is a scalar aspect (it reaches no effect bucket), so
    :func:`_compute_enh_multipliers` skips it; the buffed toggle cost needs it
    separately. Any incarnate EnduranceDiscount pre-ED term (a Cardiac Alpha) co-EDs with
    the slotted EndRdx here, exactly as the per-power ``statistics`` fold does. Only Toggle
    powers consume the result (``_gbd_toggle_end_and_fly``), so non-toggles are skipped.
    Only non-zero aggregates are stored; the global ``_selfEnhance`` EndRdx term and the
    incarnate post-ED term are added per toggle at fold time, not here.
    """
    out: dict[int, float] = {}
    for power in included:
        if power.power_type != "Toggle":
            continue
        pre_ed, _post = incarnate_scalar.get((power.build_index, END.aspect), (0.0, 0.0))
        # aggregate_and_ed over an empty/EndRdx-free slot list with no incarnate pre returns
        # 0, so no separate empty-slots guard is needed; only non-zero aggregates are stored.
        aggregate = aggregate_and_ed(
            slots.get(power.build_index, ()),
            aspect="EnduranceDiscount",
            enh_db=enh_db,
            force_level=force_level,
            tables=tables,
            pre_ed_addend=pre_ed,
        )
        if aggregate != 0.0:
            out[power.build_index] = aggregate
    return out


def _gbd_toggle_end_and_fly(
    included: Sequence[Power],
    totals: TotalStatistics,
    ctx: _MagContext,
    toggle_end_agg: Mapping[int, float],
    global_end_rdx: float,
    recharge_cap: float,
    incarnate_scalar: Mapping[tuple[int, str], tuple[float, float]] = MappingProxyType({}),
) -> bool:
    """Sum toggle end-use into ``totals`` and report canFly (clsToonX.cs:852-865).

    ``EndUse`` sums the *buffed* toggle cost — ``Power.ToggleCost`` derives from the
    enhancement-reduced ``EndCost`` (Power.cs:388): ``buffed.EndCost = base.EndCost /
    (((ED(Σ slotted EndRdx + incarnate pre) + global EndRdx) + incarnate post) + 1)``, then
    ``/ ActivatePeriod`` when the toggle has one. The divisor comes from the shared
    per-aspect handler (:func:`~coh_engine.enh_aspects.fold_scalar` with the ``END`` /
    EnduranceDiscount handler) — the same fold the per-power ``statistics`` arm uses, so the
    two cannot diverge: a Cardiac incarnate lowers toggle EndUse exactly as it lowers the
    per-power end cost, and a toggle that ignores EnduranceDiscount (``Power.IgnoreEnh``)
    keeps its base cost. EndUse is uncapped (the ``END`` handler is uncapped), so
    ``recharge_cap`` is inert for this aspect.
    """
    can_fly = False
    for power in included:
        if power.power_type == "Toggle":
            _pre, post_ed = incarnate_scalar.get((power.build_index, END.aspect), (0.0, 0.0))
            divisor = fold_scalar(
                END,
                toggle_end_agg.get(power.build_index, 0.0),
                global_end_rdx,
                ignore_enh=power.ignore_enh,
                recharge_cap=recharge_cap,
                post_ed_extra=post_ed,
            )
            buffed_end_cost = f32(power.end_cost / divisor)
            cost = f32(buffed_end_cost / power.activate_period) if power.activate_period > 0 else buffed_end_cost
            totals.end_use = f32(totals.end_use + cost)
        for fx in power.effects:
            if fx.effect_type == "Fly" and _mag(fx, power, ctx) > 0:
                can_fly = True
    return can_fly


def _gbd_fold_and_copy_vectors(totals: TotalStatistics, self_buffs: _BuffsX) -> None:
    """Generic-defense fold at index 0, then copy the vector buckets (clsToonX.cs:867-890)."""
    if abs(self_buffs.defense[0]) > _FLOAT_EPSILON:
        for index in range(1, len(self_buffs.defense)):
            self_buffs.defense[index] = f32(self_buffs.defense[index] + self_buffs.defense[0])
    for index in range(len(self_buffs.defense)):
        totals.def_[index] = self_buffs.defense[index]
        totals.res[index] = self_buffs.resistance[index]
        totals.elusivity[index] = self_buffs.elusivity[index]
    for index in range(len(self_buffs.status_protection)):
        totals.mez[index] = self_buffs.status_protection[index]
        totals.mez_res[index] = f32(self_buffs.status_resistance[index] * 100)
    for index in range(len(self_buffs.debuff_resistance)):
        totals.debuff_res[index] = f32(self_buffs.debuff_resistance[index] * 100)


def _gbd_movement(
    totals: TotalStatistics, self_buffs: _BuffsX, server: ServerData, stat: Mapping[str, int], *, can_fly: bool
) -> None:
    """Movement speeds, their MaxSpeed caps, the MaxMax clamps and fly-zero (clsToonX.cs:907-945)."""
    totals.fly_spd = _speed(self_buffs.effect[stat["FlySpeed"]], server.base_fly_speed)
    totals.run_spd = _speed(self_buffs.effect[stat["RunSpeed"]], server.base_run_speed)
    totals.jump_spd = _speed(self_buffs.effect[stat["JumpSpeed"]], server.base_jump_speed)
    totals.jump_height = _speed(self_buffs.effect[stat["JumpHeight"]], server.base_jump_height)
    totals.max_fly_spd = f32(server.max_fly_speed + f32(self_buffs.effect[stat["MaxFlySpeed"]] * server.base_fly_speed))
    totals.max_run_spd = f32(server.max_run_speed + f32(self_buffs.effect[stat["MaxRunSpeed"]] * server.base_run_speed))
    totals.max_jump_spd = f32(
        server.max_jump_speed + f32(self_buffs.effect[stat["MaxJumpSpeed"]] * server.base_jump_speed)
    )
    totals.fly_spd = min(totals.fly_spd, server.max_max_fly_speed)
    totals.run_spd = min(totals.run_spd, server.max_max_run_speed)
    totals.jump_spd = min(totals.jump_spd, server.max_max_jump_speed)
    if not can_fly:
        totals.fly_spd = 0.0


def _gbd_buff_damage(totals: TotalStatistics, self_buffs: _BuffsX, self_enhance: _BuffsX) -> None:
    """Seed damage from ``_selfEnhance`` then the min/avg/max BuffDam heuristic (clsToonX.cs:953-976)."""
    if all(abs(e) < _FLOAT_EPSILON for e in self_buffs.damage):
        for i in range(len(self_buffs.damage)):
            self_buffs.damage[i] = f32(self_buffs.damage[i] + self_enhance.damage[i])
    dmg_slice = self_buffs.damage[1:8]
    min_dmg = min(dmg_slice)
    max_dmg = max(dmg_slice)
    avg_dmg = f32(_float_average(dmg_slice))
    # C# subtracts float32 values (Damage is float[]), so each difference is
    # itself f32-rounded before the comparison (clsToonX.cs:965-969).
    hi_gap = f32(max_dmg - avg_dmg)
    lo_gap = f32(avg_dmg - min_dmg)
    if hi_gap < lo_gap:
        totals.buff_dam = max_dmg
    elif hi_gap > lo_gap and min_dmg > 0:
        totals.buff_dam = min_dmg
    else:
        totals.buff_dam = max_dmg


def _gbd_apply_caps(totals: TotalStatistics, archetype: Archetype, server: ServerData) -> TotalStatistics:
    """Value-copy ``totals`` and clamp to AT/server caps — ``TotalsCapped`` (clsToonX.cs:981-1001)."""
    capped = _assign(totals)
    capped.buff_dam = min(capped.buff_dam, f32(archetype.damage_cap - 1))
    capped.buff_haste = min(capped.buff_haste, f32(archetype.recharge_cap - 1))
    capped.hp_regen = min(capped.hp_regen, f32(archetype.regen_cap - 1))
    capped.end_rec = min(capped.end_rec, f32(archetype.recovery_cap - 1))
    for index in range(len(capped.res)):
        capped.res[index] = min(capped.res[index], f32(archetype.res_cap))
    if archetype.hp_cap > 0:
        capped.hp_max = min(capped.hp_max, f32(archetype.hp_cap))
        capped.absorb = min(capped.absorb, capped.hp_max)
    capped.run_spd = min(capped.run_spd, totals.max_run_spd)
    capped.jump_spd = min(capped.jump_spd, totals.max_jump_spd)
    capped.fly_spd = min(capped.fly_spd, totals.max_fly_spd)
    capped.jump_height = min(capped.jump_height, server.max_jump_height)
    capped.perception = min(capped.perception, f32(archetype.perception_cap))
    return capped


def _gbd_totals(
    included: Sequence[Power],
    self_buffs: _BuffsX,
    self_enhance: _BuffsX,
    *,
    archetype: Archetype,
    ctx: _MagContext,
    enums: EnumMaps,
    server: ServerData,
    toggle_end_agg: Mapping[int, float],
    incarnate_scalar: Mapping[tuple[int, str], tuple[float, float]] = MappingProxyType({}),
) -> BaseTotals:
    """``GBD_Totals`` (``clsToonX.cs:839-1002``): fold the BuffsX accumulators into Totals/TotalsCapped."""
    stat = enums.maps["eStatType"]
    totals = TotalStatistics(
        def_=[0.0] * enums.size("eDamage"),
        res=[0.0] * enums.size("eDamage"),
        mez=[0.0] * enums.size("eMez"),
        mez_res=[0.0] * enums.size("eMez"),
        debuff_res=[0.0] * enums.size("eEffectType"),
        elusivity=[0.0] * enums.size("eDamage"),
    )

    # The global _selfEnhance EnduranceDiscount reduces every toggle's cost (Mids
    # Pass3); it is the same value GBD reports as BuffEndRdx just below.
    global_end_rdx = self_enhance.effect[stat["BuffEndRdx"]]
    can_fly = _gbd_toggle_end_and_fly(
        included, totals, ctx, toggle_end_agg, global_end_rdx, archetype.recharge_cap, incarnate_scalar
    )
    _gbd_fold_and_copy_vectors(totals, self_buffs)

    totals.end_max = self_buffs.max_end
    totals.buff_acc = f32(self_enhance.effect[stat["BuffAcc"]] + self_buffs.effect[stat["BuffAcc"]])
    totals.buff_end_rdx = self_enhance.effect[stat["BuffEndRdx"]]
    totals.buff_haste = f32(self_enhance.effect[stat["Haste"]] + self_buffs.effect[stat["Haste"]])
    totals.buff_to_hit = self_buffs.effect[stat["ToHit"]]
    totals.perception = f32(server.base_perception * f32(1 + self_buffs.effect[stat["Perception"]]))
    totals.stealth_pve = self_buffs.effect[stat["StealthPvE"]]
    totals.stealth_pvp = self_buffs.effect[stat["StealthPvP"]]
    totals.threat_level = self_buffs.effect[stat["ThreatLevel"]]
    totals.hp_regen = self_buffs.effect[stat["HPRegen"]]
    totals.end_rec = self_buffs.effect[stat["EndRec"]]
    totals.absorb = self_buffs.effect[stat["Absorb"]]
    totals.buff_range = self_buffs.effect[stat["Range"]]
    totals.hp_max = f32(self_buffs.effect[stat["HPMax"]] + archetype.hitpoints)

    _gbd_movement(totals, self_buffs, server, stat, can_fly=can_fly)
    _gbd_buff_damage(totals, self_buffs, self_enhance)
    totals.buff_heal = self_buffs.effect[stat["Heal"]]

    # PvP mode: only the ApplyPvpDr defense pass (inline in GBD_Totals) is
    # ported. Mids' GenerateBuffData also folds the Temporary_Powers
    # PVP_Resist_Bonus power into the buff pass when DisablePvE is set
    # (clsToonX.cs:2179-2191) — that injection lives in the GBPA orchestration,
    # not GBD_Totals, and is deferred. PvP-mode resist/mez totals are therefore
    # incomplete; both parity fixtures are PvE (config.disable_pve == false).
    if ctx.config.disable_pve:
        for index in range(len(totals.def_)):
            totals.def_[index] = calculate_pvp_dr(totals.def_[index])

    e_type = enums.maps["eEffectType"]
    global_enhance = GlobalEnhance(
        recharge=self_enhance.effect[stat["Haste"]],
        end_discount=self_enhance.effect[stat["BuffEndRdx"]],
        accuracy=self_enhance.effect[stat["BuffAcc"]],
        interrupt=self_enhance.effect[e_type["InterruptTime"]],
        range=self_enhance.effect[e_type["Range"]],
    )
    return BaseTotals(
        totals=totals,
        totals_capped=_gbd_apply_caps(totals, archetype, server),
        global_enhance=global_enhance,
    )


def calculate_pvp_dr(value: float, a: float = f32(1.2), b: float = f32(1.0)) -> float:
    """``CalculatePvpDr`` (``clsToonX.cs:335-338``): PvP defense diminishing returns.

    Called with ``1.2f`` / ``1.0f`` (clsToonX.cs:57), so the defaults are the
    float32 values of 1.2 and 1.0. C# widens ``a`` to double for ``a*(double)val``,
    computes the bracket in double, then ``(float)``-narrows it before the final
    ``val *`` float multiply — reproduced here as two f32 rounding steps.
    """
    factor = f32(1.0 - abs(math.atan(a * value)) * (2.0 / math.pi) * b)
    return f32(value * factor)


def _speed(effect_value: float, base: float) -> float:
    return f32(f32(1 + max(effect_value, f32(-0.9))) * base)


def _float_average(values: Sequence[float]) -> float:
    # .NET LINQ Average over float[] sums in double, divides, then narrows.
    return sum(values) / len(values)


def _assign(totals: TotalStatistics) -> TotalStatistics:
    """``TotalStatistics.Assign`` — a value copy of every field."""
    return TotalStatistics(
        def_=list(totals.def_),
        res=list(totals.res),
        mez=list(totals.mez),
        mez_res=list(totals.mez_res),
        debuff_res=list(totals.debuff_res),
        elusivity=list(totals.elusivity),
        hp_regen=totals.hp_regen,
        hp_max=totals.hp_max,
        absorb=totals.absorb,
        end_rec=totals.end_rec,
        end_use=totals.end_use,
        end_max=totals.end_max,
        # Verbatim C# quirk: Assign (Character.cs:1940-1970) does NOT copy
        # MaxRunSpd/MaxJumpSpd/MaxFlySpd — TotalsCapped keeps them at 0 and
        # the speed caps below read them from the uncapped Totals instead.
        run_spd=totals.run_spd,
        jump_spd=totals.jump_spd,
        fly_spd=totals.fly_spd,
        jump_height=totals.jump_height,
        stealth_pve=totals.stealth_pve,
        stealth_pvp=totals.stealth_pvp,
        threat_level=totals.threat_level,
        perception=totals.perception,
        buff_haste=totals.buff_haste,
        buff_acc=totals.buff_acc,
        buff_to_hit=totals.buff_to_hit,
        buff_dam=totals.buff_dam,
        buff_end_rdx=totals.buff_end_rdx,
        buff_range=totals.buff_range,
        buff_heal=totals.buff_heal,
    )


# C# float.Epsilon: the smallest positive subnormal single (~1.401e-45).
_FLOAT_EPSILON = 1.401298464324817e-45
