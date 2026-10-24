"""Damage sources for the layered damage model: attack chain, damage auras, location patches, buff casts.

Each source is one ``@damage_source`` function (``.claude/rules/check-registry.md``). Patches and buff
clicks spend cast time outside the chain, so they report a ``timeline_share``; the chain source runs
last (``fills_remainder=True``) on what they leave.

**Novel to the port — no Mids parity reference.** Mids has no layered damage model; per-power damage
is the Mids reference figure (``ChainAttack.damage``) standing in for the ``FXGetDamageValue`` port (#31).
Assumption for auras: Mids ``DamagePerActivation`` of a toggle is taken as per tick (to be verified
against a fixture).

Spec: issue #67.
"""

from coh_engine.attack_chain import ChainOption, arcana_cast_time, plan_attack_chain
from coh_engine.damage_model import (
    SOURCE_AURA,
    SOURCE_CHAIN,
    SOURCE_PATCH,
    ChainAttack,
    DamageContext,
    Role,
    SourceResult,
    Stance,
    damage_source,
)
from coh_engine.maths import f32

SOURCE_BUFF_CAST = "buff_cast"


def attacks_for_form(ctx: DamageContext) -> tuple[ChainAttack, ...]:
    """The attacks usable in ``ctx.form``: mode-free ones in the base form, else those requiring the form.

    The one place form gating happens, and only for the chain. Auras, patches and buff casts are
    not filtered, so form-locked auras are not modelled.
    """
    if not ctx.form:
        return tuple(a for a in ctx.attacks if not a.profile.modes_required)
    return tuple(a for a in ctx.attacks if ctx.form in a.profile.modes_required)


def chosen_chain(ctx: DamageContext) -> ChainOption | None:
    """The planner's pick for ``ctx``: best affordable rotation, else the best overall, else None."""
    plan = plan_attack_chain(
        attacks_for_form(ctx),
        stance=ctx.stance,
        movement=ctx.movement,
        scenario=ctx.scenario,
        budget=ctx.budget,
        timeline_available=ctx.timeline_available,
        other_drain_per_sec=ctx.other_drain_per_sec,
        min_cycles=ctx.assumptions.get("chain.min_cycles", 4.0),
    )
    return plan.best_affordable or plan.best_overall


@damage_source(fills_remainder=True)
def chain_source(ctx: DamageContext) -> SourceResult | None:
    """The planned attack rotation on the timeline left after patches and buff casts."""
    option = chosen_chain(ctx)
    if option is None:
        return None
    by_name = {a.full_name: a for a in ctx.attacks}
    detail = tuple(
        (
            name,
            f32(
                f32(by_name[name].damage * ctx.scenario.targets_hit(by_name[name].profile))
                / option.cycle_time
                * ctx.timeline_available
            ),
        )
        for name in option.rotation
    )
    cycles = f32(option.time_to_empty / option.cycle_time)
    if option.sustainable:
        kind = "sustainable"
    elif cycles >= ctx.assumptions.get("chain.min_cycles", 4.0):
        kind = f"affordable: empties in {option.time_to_empty:.1f} s, {cycles:.0f} cycles"
    else:
        kind = f"not affordable (best overall): empties in {option.time_to_empty:.1f} s, {cycles:.1f} cycles"
    return SourceResult(
        source=SOURCE_CHAIN,
        dps=option.dps,
        end_per_sec=option.end_per_sec,
        detail=detail,
        label=f"chain, {kind}; damage is the Mids reference figure standing in for #31",
    )


def melee_share(ctx: DamageContext) -> float:
    """Fraction of time the character stands where a damage aura reaches foes.

    Under the hybrid stance it is derived from the chosen rotation: 0 when no attack in it needs the
    melee position, 1 when all do, otherwise the melee attacks' arcana cast time plus half the move
    time, over the cycle. ``hybrid.melee_share`` in the assumptions overrides that.

    Raises:
        ValueError: ``E56`` if ``hybrid.melee_share`` lies outside [0, 1].
    """
    if ctx.stance is Stance.MELEE:
        return 1.0
    if ctx.stance is Stance.RANGED:
        return 0.0
    if "hybrid.melee_share" in ctx.assumptions:
        share = ctx.assumptions["hybrid.melee_share"]
        if not 0.0 <= share <= 1.0:
            raise ValueError(f"E56: assumption hybrid.melee_share={share} is outside [0, 1]; set it within that range")
        return share
    chain = chosen_chain(ctx)
    if chain is None:
        return 0.0
    melee = {a.full_name: a for a in ctx.attacks if a.profile.needs_melee_position}
    melee_names = [n for n in chain.rotation if n in melee]
    if not melee_names:
        return 0.0
    if len(melee_names) == len(chain.rotation):
        return 1.0
    in_melee = f32(sum(arcana_cast_time(melee[n].cast_time) for n in melee_names) + chain.move_time / 2.0)
    return f32(in_melee / chain.cycle_time)


@damage_source()
def aura_source(ctx: DamageContext) -> SourceResult | None:
    """Damage auras: damage per tick over the tick period, on every foe in reach, while in melee.

    Raises:
        ValueError: ``E55`` if an aura has ``activate_period`` of 0.
    """
    auras = ctx.by_role(Role.AURA)
    if not auras:
        return None
    share = melee_share(ctx)
    if share == 0.0:
        return None
    detail: list[tuple[str, float]] = []
    for a in auras:
        if a.activate_period <= 0.0:
            raise ValueError(
                f"E55: aura {a.full_name} has activate_period {a.activate_period} but must be > 0; "
                "supply the toggle's tick period"
            )
        dps = f32(f32(a.damage * ctx.scenario.targets_hit(a.profile)) / a.activate_period * share)
        detail.append((a.full_name, dps))
    # Toggle drain is already in budget.toggle_drain_per_sec, so end_per_sec stays 0.
    return SourceResult(
        source=SOURCE_AURA,
        dps=f32(sum(d for _, d in detail)),
        detail=tuple(detail),
        label=f"damage auras at melee share {share:.2f}; per-tick damage assumed",
    )


def _cadence(a: ChainAttack) -> tuple[float, float]:
    """(arcana cast, cycle) of a power fired on cooldown."""
    cast = arcana_cast_time(a.cast_time)
    return cast, f32(cast + a.recharge_time)


@damage_source()
def patch_source(ctx: DamageContext) -> SourceResult | None:
    """Location patches cast on cooldown; placeable at range, so every stance gets them."""
    # Decoys and placed devices deal no damage here; debuff patches are modelled separately (Wave S).
    patches = tuple(a for a in ctx.by_role(Role.PATCH) if a.damage > 0.0)
    if not patches:
        return None
    detail: list[tuple[str, float]] = []
    share = drain = 0.0
    for a in patches:
        cast, cycle = _cadence(a)
        detail.append((a.full_name, f32(f32(a.damage * ctx.scenario.targets_hit(a.profile)) / cycle)))
        share = f32(share + cast / cycle)
        drain = f32(drain + a.end_cost / cycle)
    return SourceResult(
        source=SOURCE_PATCH,
        dps=f32(sum(d for _, d in detail)),
        timeline_share=share,
        end_per_sec=drain,
        detail=tuple(detail),
        label="location patches cast on cooldown",
    )


@damage_source()
def buff_cast_source(ctx: DamageContext) -> SourceResult | None:
    """Buff clicks and support powers deal no damage but spend cast time and endurance."""
    buffs = (*ctx.by_role(Role.BUFF_CLICK), *ctx.by_role(Role.SUPPORT))
    if not buffs:
        return None
    share = drain = 0.0
    for a in buffs:
        cast, cycle = _cadence(a)
        share = f32(share + cast / cycle)
        drain = f32(drain + a.end_cost / cycle)
    return SourceResult(
        source=SOURCE_BUFF_CAST,
        dps=0.0,
        timeline_share=share,
        end_per_sec=drain,
        label="buff clicks cast on cooldown",
    )
