"""Power/Effect records from the Mids dump harness and the base magnitude math.

The harness (``tools/mids-dump``) dumps a build's resolved DB powers with the
full magnitude-driving effect field set as ``powers_effects.json``. This module
loads those records and reproduces:

- ``DatabaseAPI.GetModifier(IEffect)`` (``DatabaseAPI.cs:2609-2630``) — the
    public entry that picks the class row (ForcedClass -> Absorbed_Class_nID ->
    selected archetype) and always passes the fixed level row
    ``MidsContext.MathLevelBase`` (49, i.e. character level 50).
- ``Effect.Mag`` (``Effect.cs:390-403``) — ``sign * Scale * nMagnitude *
    GetModifier(this)`` with the Damage sign flip and the per-``AttribType``
    branches.

Enum-valued fields are stored as the C# enum member names exactly as the
harness dumps them; ``fixtures/mids/enums.json`` maps names to ordinals.
Spec: docs/engine/mids-port-spec.md § effect-model.
"""

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from coh_engine.archetypes import ArchetypeDb
from coh_engine.attribmod import AttribMods, get_modifier
from coh_engine.maths import f32

# MidsContext.MathLevelBase (MidsContext.cs:21): the zero-based AttribMod row
# every base magnitude is read from, regardless of the character's level.
MATH_LEVEL_BASE = 49


@dataclass(frozen=True, slots=True)
class Effect:
    """One power effect record, mirroring the harness dump field-for-field."""

    index: int
    effect_type: str
    damage_type: str
    mez_type: str
    et_modifies: str
    scale: float
    n_magnitude: float
    n_duration: float
    attrib_type: str
    aspect: str
    modifier_table: str
    n_modifier_table: int
    to_who: str
    pv_mode: str
    stacking: str
    suppression: int
    buffable: bool
    resistible: bool
    ignore_ed: bool
    ignore_scaling: bool
    variable_modified: bool
    base_probability: float
    probability: float
    procs_per_minute: float
    ticks: int
    delayed_time: float
    effect_class: str
    special_case: str
    n_id_class_name: int
    absorbed_effect: bool
    absorbed_power_type: str
    absorbed_class_n_id: int
    active_conditionals_count: int
    expr_magnitude: str
    expr_duration: str
    expr_probability: str
    display_percentage: bool
    # Set by GBPA_AddEnhFX for enhancement-granted effects; always False for
    # effects loaded straight from the DB dump.
    is_enhancement_effect: bool = False


@dataclass(frozen=True, slots=True)
class Power:
    """One resolved build power with its DB effect records."""

    build_index: int
    nid_power: int
    full_name: str
    static_index: int
    power_type: str
    forced_class: str
    click_buff: bool
    level: int
    end_cost: float
    activate_period: float
    toggle_cost: float
    variable_enabled: bool
    stat_include: bool
    variable_value: int
    effects: tuple[Effect, ...]
    # eSetType ordinals this power accepts (``Power.SetTypes``); an IO set is
    # slottable here only if its set-type ordinal is in this tuple. Empty for
    # powers that take no set IOs (and for records from dumps predating the field).
    set_types: tuple[int, ...] = ()
    # EnhancementClass IDs this power accepts (``Power.Enhancements``); a standard
    # (non-set) IO is slottable here only if one of its class IDs is in this tuple
    # (``IsEnhancementValid``). Empty for powers from dumps predating the field.
    enhancements: tuple[int, ...] = ()
    # The level this build actually picked the power (``PowerEntry.Level``), distinct
    # from ``level`` (the DB minimum pick level). The set-bonus exemplar gate uses the
    # PICK level (Build.cs:1160); the two coincide at ForceLevel 50 but diverge under
    # exemplar. 0-based, like the slot placement levels. Defaults to 0 (always in gate)
    # for directly-constructed powers that never reach the tally.
    pick_level: int = 0
    # Base (unenhanced) per-power scalars the derived-stat layer (CP6.1) folds
    # enhancement + global buffs into: recharge time, cast (activation) time,
    # interrupt time. Cast time is not recharge-reduced; it feeds the end/sec and DPS
    # cadence. Default 0.0 for directly-constructed powers that skip that layer.
    recharge_time: float = 0.0
    cast_time: float = 0.0
    interrupt_time: float = 0.0
    # eEnhance aspect names this power ignores (``Power.IgnoreEnh``). The per-power
    # scalar fold skips an ignored aspect entirely (``IgnoreEnhancement``, Pass 3),
    # so e.g. One with the Shield (ignores RechargeTime) keeps its recharge at base.
    ignore_enh: tuple[str, ...] = ()
    # Power-availability prerequisites (``Power.Requires``, Requirement.cs). The
    # hard-limits validator ports ``Build.MeetsRequirement`` over these — no expression
    # evaluator, just structured full-name comparison. ``requires_powers`` is an OR list
    # of AND-groups of prerequisite power full-names (pool/ancillary tier unlocks live
    # here); ``requires_powers_not`` is the forbidden/mutex list. Empty tuples for a power
    # with no prerequisites (and for dumps predating the field). Archetype gating
    # (``RequiresClass``) is not modelled here — a loaded build is already AT-resolved, and
    # class-prerequisite validation belongs to the post-CP10 AT-specific tier.
    requires_powers: tuple[tuple[str, ...], ...] = ()
    requires_powers_not: tuple[tuple[str, ...], ...] = ()
    # ``PowerEntry.Chosen`` — whether the player spent one of the 24 picks on this power.
    # Mids also adds powers the build never picked: auto-granted sub-powers (Mystic Flight
    # grants Translocation), inherents, and incarnates all arrive via ``AddPower`` with
    # ``chosen=false``. Defaults True so a directly-constructed power, and a dump predating
    # the field, still reads as a real pick.
    chosen: bool = True


def _parse_effect(raw: dict[str, Any]) -> Effect:
    return Effect(
        index=raw["Index"],
        effect_type=raw["EffectType"],
        damage_type=raw["DamageType"],
        mez_type=raw["MezType"],
        et_modifies=raw["ETModifies"],
        scale=f32(raw["Scale"]),
        n_magnitude=f32(raw["nMagnitude"]),
        n_duration=f32(raw["nDuration"]),
        attrib_type=raw["AttribType"],
        aspect=raw["Aspect"],
        modifier_table=raw["ModifierTable"],
        n_modifier_table=raw["nModifierTable"],
        to_who=raw["ToWho"],
        pv_mode=raw["PvMode"],
        stacking=raw["Stacking"],
        suppression=raw["Suppression"],
        buffable=raw["Buffable"],
        resistible=raw["Resistible"],
        ignore_ed=raw["IgnoreED"],
        ignore_scaling=raw["IgnoreScaling"],
        variable_modified=raw["VariableModified"],
        base_probability=f32(raw["BaseProbability"]),
        probability=f32(raw["Probability"]),
        procs_per_minute=f32(raw["ProcsPerMinute"]),
        ticks=raw["Ticks"],
        delayed_time=f32(raw["DelayedTime"]),
        effect_class=raw["EffectClass"],
        special_case=raw["SpecialCase"],
        n_id_class_name=raw["nIDClassName"],
        absorbed_effect=raw["Absorbed_Effect"],
        absorbed_power_type=raw["Absorbed_PowerType"],
        absorbed_class_n_id=raw["Absorbed_Class_nID"],
        active_conditionals_count=raw["ActiveConditionalsCount"],
        expr_magnitude=raw["ExprMagnitude"],
        expr_duration=raw["ExprDuration"],
        expr_probability=raw["ExprProbability"],
        display_percentage=raw["DisplayPercentage"],
    )


def load_powers_effects(path: Path | str) -> tuple[Power, ...]:
    """Parse a ``powers_effects.json`` Mids reference dump.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        records = json.load(stream)
    return tuple(
        Power(
            build_index=r["BuildIndex"],
            nid_power=r["NIDPower"],
            full_name=r["FullName"],
            static_index=r["StaticIndex"],
            power_type=r["PowerType"],
            forced_class=r["ForcedClass"],
            click_buff=r["ClickBuff"],
            level=r["Level"],
            end_cost=f32(r["EndCost"]),
            activate_period=f32(r["ActivatePeriod"]),
            toggle_cost=f32(r["ToggleCost"]),
            variable_enabled=r["VariableEnabled"],
            stat_include=r["StatInclude"],
            variable_value=r["VariableValue"],
            # Strict, like enhancement.py's nIDSet: a power that accepts no sets is
            # dumped as an explicit [] (loads fine and validates as reject-all); only
            # a dump predating the field is missing the key, and should fail loud
            # rather than silently validate every set IO as illegally slotted.
            set_types=tuple(r["SetTypes"]),
            # Same strictness: an explicit [] means "accepts no standard IO class";
            # a missing key is a stale dump that must fail loud, not silently reject
            # or accept every standard IO.
            enhancements=tuple(r["Enhancements"]),
            pick_level=r["PickLevel"],
            recharge_time=f32(r["RechargeTime"]),
            cast_time=f32(r["CastTime"]),
            interrupt_time=f32(r["InterruptTime"]),
            ignore_enh=tuple(r["IgnoreEnh"]),
            # Lenient (``.get``): Requirement was added after the earlier build dumps;
            # a power with no prerequisites dumps empty arrays, and a dump predating the
            # field is treated as no-prerequisites. Empty names inside an AND-pair
            # (single-power requirements leave the second slot blank) are dropped.
            requires_powers=_parse_requirement_groups(r.get("RequiresPowers", ())),
            requires_powers_not=_parse_requirement_groups(r.get("RequiresPowersNot", ())),
            # Lenient (``.get``): Chosen was added after the earlier build dumps, which
            # carry only real picks plus inherents — both correctly read as chosen there.
            chosen=r.get("Chosen", True),
            effects=tuple(_parse_effect(fx) for fx in r["Effects"]),
        )
        for r in records
    )


def _parse_requirement_groups(groups: Any) -> tuple[tuple[str, ...], ...]:
    """A ``Requires`` OR-list of AND-pairs → tuples, dropping blank names and empty groups."""
    parsed = tuple(tuple(name for name in group if name) for group in groups)
    return tuple(group for group in parsed if group)


def get_modifier_for_effect(
    effect: Effect,
    power: Power,
    mods: AttribMods,
    classes: ArchetypeDb,
    archetype_index: int,
) -> float:
    """Public ``GetModifier(IEffect)`` for an effect attached to a power.

    Class-row priority (``DatabaseAPI.cs:2609-2630``): the power's
    ``ForcedClass`` (pet powers), then the effect's ``Absorbed_Class_nID``,
    then the build's selected archetype. The level row is always
    :data:`MATH_LEVEL_BASE`. The C# ``effPower == null`` branch (returns 1.0
    or the class-0 lookup) cannot occur here: every dumped effect record is
    attached to a resolved power.
    """
    if power.forced_class:
        i_class = classes.nid_from_uid_class(power.forced_class)
    elif effect.absorbed_class_n_id > -1:
        i_class = effect.absorbed_class_n_id
    else:
        i_class = archetype_index
    return get_modifier(mods, classes.classes, i_class, effect.n_modifier_table, MATH_LEVEL_BASE)


def effect_mag(
    effect: Effect,
    power: Power,
    mods: AttribMods,
    classes: ArchetypeDb,
    archetype_index: int,
) -> float:
    """Raw per-effect magnitude — ``Effect.Mag`` (``Effect.cs:390-403``).

    Damage effects report a *negative* raw magnitude (damage is stored as a
    negative buff); every multiplication is quantized to float32 to match the
    C# ``float`` arithmetic.

    ``GBPA_MultiplyVariable`` (``clsToonX.cs:1570-1596``) is folded in here:
    on a ``VariableEnabled`` power, every ``VariableModified`` effect that
    does not ``IgnoreScaling`` has its ``Scale`` multiplied by the build
    entry's ``VariableValue`` before any magnitude is read (Mids mutates the
    assembled power's Scale in Pass0; this port scales at read time).

    Raises:
        NotImplementedError: for ``AttribType == Expression`` effects with a
            non-blank magnitude expression — the ``Expressions.Parse``
            evaluator is deferred (spec § expressions-language).
    """
    scale = effect.scale
    if power.variable_enabled and effect.variable_modified and not effect.ignore_scaling:
        scale = f32(scale * power.variable_value)
    sign = -1.0 if effect.effect_type == "Damage" else 1.0
    if effect.attrib_type == "Magnitude":
        modifier = get_modifier_for_effect(effect, power, mods, classes, archetype_index)
        return f32(f32(f32(sign * scale) * effect.n_magnitude) * modifier)
    if effect.attrib_type == "Duration":
        return f32(sign * effect.n_magnitude)
    if effect.attrib_type == "Expression":
        # C# gates on string.IsNullOrWhiteSpace (Effect.cs:398-399): a
        # whitespace-only expression is "blank" and falls through to
        # Scale * nMagnitude, not treated as a real expression.
        if effect.expr_magnitude.strip():
            raise NotImplementedError(
                f"E08: cannot compute Mag for {power.full_name} effect {effect.index}: "
                "AttribType == Expression with a magnitude expression requires the "
                "Expressions.Parse evaluator (deferred; spec § expressions-language)"
            )
        return f32(f32(sign * scale) * effect.n_magnitude)
    return 0.0
