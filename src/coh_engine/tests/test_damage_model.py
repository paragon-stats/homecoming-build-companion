"""The layered damage model's contract: types, registries, and the composing runner.

The runner is exercised with injected sources and modifiers so the tests do not depend on
which plugins happen to be registered.
"""

from dataclasses import replace
from math import inf

import pytest

from coh_engine import damage_model
from coh_engine.damage_model import (
    AttackProfile,
    ChainAttack,
    DamageContext,
    Delivery,
    EnduranceBudget,
    ModifierResult,
    Movement,
    Role,
    Scenario,
    Shape,
    SourceResult,
    Stance,
    damage_modifier,
    damage_source,
    evaluate_damage,
    registered_damage_modifiers,
    registered_damage_sources,
)
from coh_engine.maths import f32


def _profile(shape: Shape = Shape.SINGLE, delivery: Delivery = Delivery.RANGED, targets: int = 0) -> AttackProfile:
    return AttackProfile(
        full_name="X",
        delivery=delivery,
        shape=shape,
        role=Role.CHAIN,
        range_ft=80.0,
        radius_ft=0.0,
        arc_rad=0.0,
        max_targets=targets,
    )


def _ctx(**kw: object) -> DamageContext:
    base: dict[str, object] = {
        "attacks": (),
        "budget": EnduranceBudget(max_end=100.0, recovery_per_sec=2.0, toggle_drain_per_sec=0.5),
        "stance": Stance.MELEE,
        "scenario": Scenario(),
        "movement": Movement(speed_ft_per_sec=20.0, standoff_ft=40.0),
        "archetype_class": "Class_Test",
    }
    base.update(kw)
    return DamageContext(**base)  # type: ignore[arg-type]


def test_melee_position_needed_for_melee_delivery_and_pbaoe() -> None:
    """Melee delivery or a self-centred area pins the character in melee; ranged cones do not."""
    assert _profile(delivery=Delivery.MELEE).needs_melee_position
    assert _profile(shape=Shape.PBAOE).needs_melee_position
    assert not _profile(shape=Shape.CONE).needs_melee_position


def test_budget_net_and_time_to_empty() -> None:
    """Net = recovery - toggles - extra; empty time is max_end over the deficit, inf when sustainable."""
    b = EnduranceBudget(max_end=100.0, recovery_per_sec=2.0, toggle_drain_per_sec=0.5)
    assert b.net_per_sec() == f32(1.5)
    assert b.time_to_empty() == inf
    assert b.time_to_empty(extra_drain=2.5) == f32(100.0)


def test_scenario_targets_hit() -> None:
    """Single target hits one; areas hit min(cap, foes); an uncapped area hits every foe."""
    s = Scenario(targets=10)
    assert s.targets_hit(_profile()) == 1
    assert s.targets_hit(_profile(Shape.CONE, targets=5)) == 5
    assert s.targets_hit(_profile(Shape.PBAOE, targets=0)) == 10


def test_movement_switch_time() -> None:
    """Crossing the standoff takes distance / speed; no speed means never."""
    assert Movement(speed_ft_per_sec=20.0, standoff_ft=40.0).switch_time == f32(2.0)
    assert Movement(speed_ft_per_sec=0.0, standoff_ft=40.0).switch_time == inf


def test_context_filters_by_role() -> None:
    """``by_role`` keeps input order and drops other roles."""
    aura = ChainAttack("A", 1.0, 0.0, 0.0, 0.0, replace(_profile(), role=Role.AURA))
    chain = ChainAttack("C", 1.0, 1.0, 1.0, 1.0, _profile())
    assert _ctx(attacks=(chain, aura)).by_role(Role.AURA) == (aura,)


def test_runner_gives_the_chain_the_leftover_timeline_and_drain() -> None:
    """Non-chain sources run first; the remainder source sees 1 - their share and their drain."""
    seen: list[tuple[float, float]] = []

    def patch(ctx: DamageContext) -> SourceResult:
        return SourceResult(source="patch", dps=10.0, timeline_share=0.25, end_per_sec=0.5)

    def chain(ctx: DamageContext) -> SourceResult:
        seen.append((ctx.timeline_available, ctx.other_drain_per_sec))
        return SourceResult(source="chain", dps=100.0, end_per_sec=1.0)

    def nothing(ctx: DamageContext) -> None:
        return None

    report = evaluate_damage(_ctx(), sources=[(chain, True), (patch, False), (nothing, False)], modifiers=[])
    assert seen == [(f32(0.75), f32(0.5))]
    assert [r.source for r in report.sources] == ["patch", "chain"]
    assert report.total_dps == f32(110.0)
    assert report.total_drain_per_sec == f32(1.5)
    assert report.time_to_empty == inf


def test_runner_applies_uptime_weighted_modifiers_to_their_targets() -> None:
    """A scoped modifier scales only its sources; an unscoped one scales all; None is skipped."""

    def chain(ctx: DamageContext) -> SourceResult:
        return SourceResult(source="chain", dps=100.0)

    def pet(ctx: DamageContext) -> SourceResult:
        return SourceResult(source="pet", dps=50.0)

    def debuff(ctx: DamageContext) -> ModifierResult:
        return ModifierResult(modifier="debuff", multiplier=1.2, uptime=0.5)

    def fury(ctx: DamageContext) -> ModifierResult:
        return ModifierResult(modifier="fury", multiplier=2.0, applies_to=frozenset({"chain"}))

    def off(ctx: DamageContext) -> None:
        return None

    report = evaluate_damage(_ctx(), sources=[(chain, True), (pet, False)], modifiers=[debuff, fury, off])
    assert report.scaled_dps["chain"] == f32(100.0 * 1.1 * 2.0)
    assert report.scaled_dps["pet"] == f32(50.0 * 1.1)
    assert [m.modifier for m in report.modifiers] == ["debuff", "fury"]


def test_additive_buffs_divide_by_the_strength_already_applied() -> None:
    """A +100% buff on damage already at +100% strength adds half; a true doubling stays x2."""
    buff = ModifierResult(modifier="fury", multiplier=2.0, additive=True)
    double = ModifierResult(modifier="containment", multiplier=2.0)
    assert buff.scale(2.0) == f32(1.5)
    assert double.scale(2.0) == f32(2.0)


def test_source_baseline_overrides_the_context_baseline() -> None:
    """A source with its own enhancement (pets) is scaled against its own baseline."""

    def chain(ctx: DamageContext) -> SourceResult:
        return SourceResult(source="chain", dps=100.0)

    def pet(ctx: DamageContext) -> SourceResult:
        return SourceResult(source="pet", dps=100.0, damage_buff_baseline=1.0)

    def buff(ctx: DamageContext) -> ModifierResult:
        return ModifierResult(modifier="buff", multiplier=1.5, additive=True)

    report = evaluate_damage(_ctx(damage_buff_baseline=2.0), sources=[(chain, True), (pet, False)], modifiers=[buff])
    assert report.scaled_dps["chain"] == f32(125.0)
    assert report.scaled_dps["pet"] == f32(150.0)


def test_runner_rejects_an_overcommitted_timeline() -> None:
    """Non-chain shares summing past 1.0 are a modelling error and fail loud (E30)."""

    def greedy(ctx: DamageContext) -> SourceResult:
        return SourceResult(source="greedy", dps=1.0, timeline_share=1.5)

    with pytest.raises(ValueError, match="E30"):
        evaluate_damage(_ctx(), sources=[(greedy, False)], modifiers=[])


def test_registries_record_decorated_functions() -> None:
    """The decorators append to the default registries the runner folds."""

    @damage_source(fills_remainder=True)
    def _probe_source(ctx: DamageContext) -> None:
        return None

    @damage_modifier
    def _probe_modifier(ctx: DamageContext) -> None:
        return None

    try:
        assert "_probe_source" in registered_damage_sources()
        assert "_probe_modifier" in registered_damage_modifiers()
        report = evaluate_damage(_ctx())
        assert "_probe_source" not in report.scaled_dps
    finally:
        damage_model._SOURCES.remove((_probe_source, True))
        damage_model._MODIFIERS.remove(_probe_modifier)
