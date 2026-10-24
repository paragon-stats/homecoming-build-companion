"""Damage modifiers: click buffs and target resistance debuffs."""

import pytest

from coh_engine.attack_chain import arcana_cast_time
from coh_engine.damage_model import (
    AttackProfile,
    ChainAttack,
    DamageContext,
    Delivery,
    EnduranceBudget,
    Movement,
    Role,
    Scenario,
    Shape,
    Stance,
    evaluate_damage,
)
from coh_engine.damage_modifiers import click_buff_modifier, target_debuff_modifier
from coh_engine.damage_sources import buff_cast_source, chain_source, chosen_chain, melee_share


def _attack(name: str, role: Role, **kw: float) -> ChainAttack:
    profile = AttackProfile(name, Delivery.RANGED, Shape.SINGLE, role, 80.0, 0.0, 0.0, 0)
    return ChainAttack(name, 100.0, 1.0, 9.0, 5.0, profile, **kw)


def _ctx(*attacks: ChainAttack, **kw: object) -> DamageContext:
    base: dict[str, object] = {
        "attacks": attacks,
        "budget": EnduranceBudget(max_end=100.0, recovery_per_sec=10.0, toggle_drain_per_sec=0.0),
        "stance": Stance.MELEE,
        "scenario": Scenario(),
        "movement": Movement(speed_ft_per_sec=20.0, standoff_ft=40.0),
        "archetype_class": "Class_Test",
    }
    base.update(kw)
    return DamageContext(**base)  # type: ignore[arg-type]


CYCLE = arcana_cast_time(1.0) + 9.0


def test_none_without_buffs_or_debuffs() -> None:
    """No qualifying attack means no modifier; zero-strength riders do not count."""
    ctx = _ctx(_attack("Plain", Role.CHAIN), _attack("Dud", Role.BUFF_CLICK))
    assert click_buff_modifier(ctx) is None
    assert target_debuff_modifier(ctx) is None


def test_click_buff_uptime_and_scope() -> None:
    """Uptime is duration over cycle (capped at 1); the buff is additive and spares pets."""
    full = _attack("Full", Role.BUFF_CLICK, self_damage_buff=0.25, self_damage_buff_duration=100.0)
    part = _attack("Part", Role.BUFF_CLICK, self_damage_buff=0.5, self_damage_buff_duration=CYCLE / 2)
    result = click_buff_modifier(_ctx(full, part))
    assert result is not None
    assert result.additive
    assert result.uptime == 1.0
    assert result.multiplier == pytest.approx(1.0 + 0.25 + 0.25, rel=1e-5)
    assert result.applies_to == frozenset({"chain", "aura", "patch"})
    assert "Full +25% at 100% uptime" in result.assumption
    assert "Part +50% at 50% uptime" in result.assumption


def test_target_debuff_default_and_resistant_target() -> None:
    """Factor is (1 - r + d) / (1 - r); chain and patch debuffs stack, others are ignored."""
    chain = _attack("Chain", Role.CHAIN, target_res_debuff=0.2, target_res_debuff_duration=100.0)
    patch = _attack("Patch", Role.PATCH, target_res_debuff=0.1, target_res_debuff_duration=CYCLE / 2)
    ignored = _attack("Click", Role.BUFF_CLICK, target_res_debuff=0.5, target_res_debuff_duration=100.0)
    d = 0.2 + 0.1 * 0.5
    base = target_debuff_modifier(_ctx(chain, patch, ignored))
    assert base is not None
    assert not base.additive
    assert base.applies_to is None
    assert base.multiplier == pytest.approx(1.0 + d, rel=1e-5)
    assert "Chain -20% res" in base.assumption
    tough = target_debuff_modifier(_ctx(chain, patch, assumptions={"target.resistance": 0.5}))
    assert tough is not None
    assert tough.multiplier == pytest.approx((0.5 + d) / 0.5, rel=1e-5)


def test_debuff_from_an_attack_outside_the_chosen_chain_does_not_count() -> None:
    """A debuffing attack the planner leaves out is never cast, so its debuff is never applied."""
    hitter = ChainAttack("Hitter", 100.0, 1.0, 1.0, 1.0, _attack("Hitter", Role.CHAIN).profile)
    slow = _attack("Slow", Role.CHAIN, target_res_debuff=0.2, target_res_debuff_duration=10.0)
    slow = ChainAttack(
        "Slow", 1.0, 1.0, 60.0, 1.0, slow.profile, target_res_debuff=0.2, target_res_debuff_duration=10.0
    )
    assert target_debuff_modifier(_ctx(hitter, slow)) is None


def test_chain_debuff_uptime_is_duration_over_the_chosen_cycle() -> None:
    """A picked debuff fires once per chosen cycle: uptime = duration / cycle time."""
    chain = _attack("Chain", Role.CHAIN, target_res_debuff=0.2, target_res_debuff_duration=4.0)
    result = target_debuff_modifier(_ctx(chain))
    assert result is not None
    assert result.multiplier == pytest.approx(1.0 + 0.2 * 4.0 / CYCLE, rel=1e-5)


@pytest.mark.parametrize("bad", [-1.5, 0.99])
def test_target_resistance_out_of_range(bad: float) -> None:
    """A target resistance outside -1..0.95 is E57."""
    chain = _attack("Chain", Role.CHAIN, target_res_debuff=0.2, target_res_debuff_duration=10.0)
    with pytest.raises(ValueError, match="E57"):
        target_debuff_modifier(_ctx(chain, assumptions={"target.resistance": bad}))


def test_chain_debuff_comes_from_the_chosen_option_under_a_reduced_timeline() -> None:
    """The chain part is the chosen option's own res_debuff: a one-cycle debuff is up half the time at 0.5 timeline."""
    chain = _attack("Chain", Role.CHAIN, target_res_debuff=0.4, target_res_debuff_duration=CYCLE)
    ctx = _ctx(chain, timeline_available=0.5)
    option = chosen_chain(ctx)
    assert option is not None
    assert option.res_debuff == pytest.approx(0.5 * 0.4, rel=1e-5)
    result = target_debuff_modifier(ctx)
    assert result is not None
    assert result.multiplier == pytest.approx(1.0 + option.res_debuff, rel=1e-5)
    assert "Chain" in result.assumption


def _pbaoe(name: str, role: Role, **kw: float) -> ChainAttack:
    profile = AttackProfile(name, Delivery.RANGED, Shape.PBAOE, role, 0.0, 15.0, 0.0, 16)
    return ChainAttack(name, 0.0, 1.0, 1.0, 0.0, profile, **kw)


def _melt() -> ChainAttack:
    return ChainAttack(
        "Melt",
        0.0,
        1.5,
        100.0,
        10.0,
        _attack("Melt", Role.SUPPORT).profile,
        target_res_debuff=0.3,
        target_res_debuff_duration=40.0,
    )


def test_support_debuff_uptime_is_duration_over_cooldown_cycle() -> None:
    """A Melt Armor-like SUPPORT debuff is up duration / (arcana cast + recharge), like a patch."""
    up = 40.0 / (arcana_cast_time(1.5) + 100.0)
    result = target_debuff_modifier(_ctx(_melt()))
    assert result is not None
    assert result.multiplier == pytest.approx(1.0 + 0.3 * up, rel=1e-5)
    assert f"Melt -30% res (support) at {up:.0%} uptime" in result.assumption


def test_support_self_damage_buff_enters_click_buff() -> None:
    """A Siphon Power-like SUPPORT with a self damage buff is an additive click buff at cooldown uptime."""
    siphon = _attack("Siphon", Role.SUPPORT, self_damage_buff=0.2, self_damage_buff_duration=5.0)
    result = click_buff_modifier(_ctx(siphon))
    assert result is not None
    assert result.additive
    assert result.multiplier == pytest.approx(1.0 + 0.2 * 5.0 / CYCLE, rel=1e-5)
    assert "Siphon" in result.assumption


@pytest.mark.parametrize("stance", list(Stance))
def test_anchored_persistent_debuff_is_always_up(stance: Stance) -> None:
    """An anchored toggle (Enervating Field) holds -Res in every stance."""
    field = _attack("Field", Role.PERSISTENT_DEBUFF, target_res_debuff=0.15)
    result = target_debuff_modifier(_ctx(field, stance=stance))
    assert result is not None
    assert result.multiplier == pytest.approx(1.15, rel=1e-5)
    assert "Field -15% res (persistent_debuff) at 100% uptime" in result.assumption


def test_self_centred_persistent_debuff_follows_the_stance() -> None:
    """A PBAoE toggle (Venomous Gas) is up in melee, down at range, and at the melee share when hybrid."""
    gas = _pbaoe("Gas", Role.PERSISTENT_DEBUFF, target_res_debuff=0.2)
    melee = target_debuff_modifier(_ctx(gas, stance=Stance.MELEE))
    assert melee is not None
    assert melee.multiplier == pytest.approx(1.2, rel=1e-5)
    assert target_debuff_modifier(_ctx(gas, stance=Stance.RANGED)) is None
    ctx = _ctx(gas, stance=Stance.HYBRID, assumptions={"hybrid.melee_share": 0.25})
    hybrid = target_debuff_modifier(ctx)
    assert hybrid is not None
    assert melee_share(ctx) == 0.25
    assert hybrid.multiplier == pytest.approx(1.0 + 0.2 * 0.25, rel=1e-5)
    assert "Gas -20% res (persistent_debuff) at 25% uptime" in hybrid.assumption


def test_hybrid_persistent_debuff_uses_the_derived_melee_share() -> None:
    """With no override, the hybrid uptime is the rotation-derived melee share (zero here: no melee hit)."""
    gas = _pbaoe("Gas", Role.PERSISTENT_DEBUFF, target_res_debuff=0.2)
    ctx = _ctx(gas, _attack("Far", Role.CHAIN), stance=Stance.HYBRID)
    assert melee_share(ctx) == 0.0
    assert target_debuff_modifier(ctx) is None


def test_end_to_end_support_and_persistent_debuff() -> None:
    """The support cast shrinks the chain's timeline; the stacked debuffs scale the chain."""
    hit = _attack("Hit", Role.CHAIN)
    field = _attack("Field", Role.PERSISTENT_DEBUFF, target_res_debuff=0.15)
    ctx = _ctx(hit, _melt(), field, scenario=Scenario(targets=1))
    report = evaluate_damage(
        ctx, sources=[(chain_source, True), (buff_cast_source, False)], modifiers=[target_debuff_modifier]
    )
    cast = arcana_cast_time(1.5)
    share = cast / (cast + 100.0)
    assert report.remainder_context.timeline_available == pytest.approx(1.0 - share, rel=1e-5)
    chain_dps = next(s.dps for s in report.sources if s.source == "chain")
    hit_cycle = CYCLE
    assert chain_dps == pytest.approx(100.0 / hit_cycle * (1.0 - share), rel=1e-4)
    factor = 1.0 + 0.3 * 40.0 / (cast + 100.0) + 0.15
    assert report.scaled_dps["chain"] == pytest.approx(chain_dps * factor, rel=1e-4)
