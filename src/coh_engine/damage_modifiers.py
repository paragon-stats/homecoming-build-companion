"""Damage modifiers for the layered damage model: click buffs and target resistance debuffs.

Each modifier is one ``@damage_modifier`` function (``.claude/rules/check-registry.md``).

**Novel to the port — no Mids parity reference.** Click buffs and location-patch debuffs are fired on
cooldown, since their casts are charged to the timeline as such. A chain attack's debuff counts only
when the attack is in the chosen chain (``damage_sources.chosen_chain``), once per chosen cycle: an
attack the planner leaves out is never cast, so its debuff is never applied.

Spec: issue #67.
"""

from coh_engine.attack_chain import arcana_cast_time
from coh_engine.damage_model import (
    SOURCE_AURA,
    SOURCE_CHAIN,
    SOURCE_PATCH,
    ChainAttack,
    DamageContext,
    ModifierResult,
    Role,
    damage_modifier,
)
from coh_engine.damage_sources import chosen_chain
from coh_engine.maths import f32


def _uptime(a: ChainAttack, duration: float) -> float:
    """Fraction of time an effect of ``duration`` is up when the power fires on cooldown."""
    return min(1.0, f32(duration / f32(arcana_cast_time(a.cast_time) + a.recharge_time)))


@damage_modifier
def click_buff_modifier(ctx: DamageContext) -> ModifierResult | None:
    """Self damage-buff clicks, additive; they scale the character's own damage, not pets'."""
    buffs = [a for a in ctx.by_role(Role.BUFF_CLICK) if a.self_damage_buff > 0.0]
    if not buffs:
        return None
    ups = [_uptime(a, a.self_damage_buff_duration) for a in buffs]
    bonus = f32(sum(f32(a.self_damage_buff * u) for a, u in zip(buffs, ups, strict=True)))
    text = "; ".join(
        f"{a.full_name} +{a.self_damage_buff:.0%} at {u:.0%} uptime" for a, u in zip(buffs, ups, strict=True)
    )
    return ModifierResult(
        modifier="click_buff",
        multiplier=f32(1.0 + bonus),
        uptime=1.0,
        applies_to=frozenset({SOURCE_CHAIN, SOURCE_AURA, SOURCE_PATCH}),
        assumption=f"click buffs fired on cooldown: {text}",
        additive=True,
    )


@damage_modifier
def target_debuff_modifier(ctx: DamageContext) -> ModifierResult | None:
    """Resistance debuffs on the target: damage factor (1 - r + d) / (1 - r); pets benefit too.

    Raises:
        ValueError: ``E57`` if ``target.resistance`` lies outside [-1, 0.95].
    """
    chain = chosen_chain(ctx)
    rotation = set(chain.rotation) if chain is not None else set()
    debuffs = [
        a
        for a in ctx.attacks
        if a.target_res_debuff > 0.0
        and (a.profile.role is Role.PATCH or (a.profile.role is Role.CHAIN and a.full_name in rotation))
    ]
    if not debuffs:
        return None
    r = ctx.assumptions.get("target.resistance", 0.0)
    if not -1.0 <= r <= 0.95:
        raise ValueError(f"E57: assumption target.resistance={r} is outside [-1, 0.95]; set it within that range")
    patches = [a for a in debuffs if a.profile.role is Role.PATCH]
    ups = [_uptime(a, a.target_res_debuff_duration) for a in patches]
    # The chain part is the chosen option's own figure (attack_chain.plan_attack_chain), not recomputed here.
    chain_part = chain.res_debuff if chain is not None else 0.0
    stacked = f32(chain_part + sum(f32(a.target_res_debuff * u) for a, u in zip(patches, ups, strict=True)))
    parts = [f"{a.full_name} -{a.target_res_debuff:.0%} res" for a in debuffs if a.profile.role is Role.CHAIN]
    parts += [
        f"{a.full_name} -{a.target_res_debuff:.0%} res at {u:.0%} uptime" for a, u in zip(patches, ups, strict=True)
    ]
    text = "; ".join(parts)
    return ModifierResult(
        modifier="target_debuff",
        multiplier=f32(f32(1.0 - r + stacked) / f32(1.0 - r)),
        uptime=1.0,
        applies_to=None,
        assumption=f"debuffs from the chosen chain and patches against {r:.0%} resistance, stacked -{stacked:.1%}: {text}",
        additive=False,
    )
