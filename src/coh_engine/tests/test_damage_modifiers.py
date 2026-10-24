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
)
from coh_engine.damage_modifiers import click_buff_modifier, target_debuff_modifier
from coh_engine.damage_sources import chosen_chain


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
