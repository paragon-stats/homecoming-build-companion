"""The attack-chain sequencer: rotation, gap detection, and chain endurance/sec.

The cadence inputs (recharge/cast) are parity-validated in test_statistics; here the
rotation and gap logic — the port's own, with no Mids equivalent — is exercised directly.
"""

from math import inf

import pytest

from coh_engine.attack_chain import ChainPlan, arcana_cast_time, build_attack_chain, plan_attack_chain
from coh_engine.damage_model import (
    AttackProfile,
    ChainAttack,
    Delivery,
    EnduranceBudget,
    Movement,
    Role,
    Scenario,
    Shape,
    Stance,
)
from coh_engine.maths import f32
from coh_engine.statistics import PowerStats


def _stat(name: str, *, recharge: float, cast: float, end: float, is_attack: bool = True) -> PowerStats:
    return PowerStats(
        full_name=name,
        recharge_time=recharge,
        end_cost=end,
        interrupt_time=0.0,
        end_per_sec=0.0,
        cast_time=cast,
        is_attack=is_attack,
    )


def test_no_attacks_is_an_empty_chain() -> None:
    """A build with no attacks yields an empty, trivially gap-free chain with no drain."""
    chain = build_attack_chain([_stat("Toggle", recharge=0.0, cast=0.0, end=0.0, is_attack=False)])
    assert chain.rotation == ()
    assert chain.gap_free is True
    assert chain.end_per_sec == 0.0
    assert chain.cycle_time == 0.0


def test_gap_free_chain_cycles_on_cast_time() -> None:
    """When the cast total covers every attack's recharge, the chain is gap-free."""
    # 3 attacks, cast 1.0 each (cast_sum 3.0); each recharge 1.0 -> max(cast+rech)=2.0 <= 3.0.
    attacks = [_stat(f"A{i}", recharge=1.0, cast=1.0, end=6.0) for i in range(3)]
    chain = build_attack_chain(attacks)
    assert chain.gap_free is True
    assert chain.cast_sum == f32(3.0)
    assert chain.cycle_time == f32(3.0)
    assert chain.gap == 0.0
    # end/sec = 18 end over the 3.0s cycle.
    assert chain.end_per_sec == f32(18.0 / 3.0)


def test_slow_attack_creates_a_gap() -> None:
    """An attack whose recharge outlasts the rest of the chain stretches the cycle (gap)."""
    # A: cast 1, recharge 5 -> ready at 6; B: cast 1, recharge 1. cast_sum 2, bottleneck 6.
    chain = build_attack_chain(
        [_stat("A", recharge=5.0, cast=1.0, end=5.0), _stat("B", recharge=1.0, cast=1.0, end=5.0)]
    )
    assert chain.gap_free is False
    assert chain.cast_sum == f32(2.0)
    assert chain.cycle_time == f32(6.0)
    assert chain.gap == f32(4.0)
    assert chain.end_per_sec == f32(10.0 / 6.0)


def test_rotation_orders_longest_recharge_first() -> None:
    """The rotation is presented longest-recharge first (order does not affect the math)."""
    chain = build_attack_chain(
        [
            _stat("Fast", recharge=2.0, cast=1.0, end=5.0),
            _stat("Slow", recharge=8.0, cast=1.0, end=5.0),
            _stat("Mid", recharge=4.0, cast=1.0, end=5.0),
        ]
    )
    assert chain.rotation == ("Slow", "Mid", "Fast")


def test_non_attacks_are_excluded() -> None:
    """Only is_attack power stats enter the chain; toggles/buffs are ignored."""
    chain = build_attack_chain(
        [
            _stat("Attack", recharge=1.0, cast=1.0, end=5.0),
            _stat("Heal", recharge=10.0, cast=2.0, end=20.0, is_attack=False),
        ]
    )
    assert chain.rotation == ("Attack",)
    # Only the attack counts; a lone attack still waits its own recharge (cycle = cast+recharge = 2).
    assert chain.cycle_time == f32(2.0)
    assert chain.end_per_sec == f32(5.0 / 2.0)


# --- plan_attack_chain -------------------------------------------------------------------------


def _atk(
    name: str,
    *,
    damage: float = 100.0,
    cast: float = 1.0,
    recharge: float = 2.0,
    end: float = 5.0,
    melee: bool = True,
    shape: Shape = Shape.SINGLE,
    max_targets: int = 1,
    role: Role = Role.CHAIN,
) -> ChainAttack:
    profile = AttackProfile(
        full_name=name,
        delivery=Delivery.MELEE if melee else Delivery.RANGED,
        shape=shape,
        role=role,
        range_ft=7.0 if melee else 80.0,
        radius_ft=0.0,
        arc_rad=0.0,
        max_targets=max_targets,
    )
    return ChainAttack(
        full_name=name, damage=damage, cast_time=cast, recharge_time=recharge, end_cost=end, profile=profile
    )


_RICH = EnduranceBudget(max_end=100.0, recovery_per_sec=100.0, toggle_drain_per_sec=0.0)
_MOVE = Movement(speed_ft_per_sec=10.0, standoff_ft=20.0)  # switch_time 2.0 s


def _plan(
    attacks: list[ChainAttack],
    stance: Stance = Stance.MELEE,
    *,
    movement: Movement = _MOVE,
    budget: EnduranceBudget = _RICH,
    scenario: Scenario | None = None,
    timeline_available: float = 1.0,
    other_drain_per_sec: float = 0.0,
    time_to_empty_min_s: float = 120.0,
    max_attacks: int = 12,
) -> ChainPlan:
    return plan_attack_chain(
        attacks,
        stance=stance,
        movement=movement,
        scenario=scenario or Scenario(targets=1),
        budget=budget,
        timeline_available=timeline_available,
        other_drain_per_sec=other_drain_per_sec,
        time_to_empty_min_s=time_to_empty_min_s,
        max_attacks=max_attacks,
    )


def test_arcana_cast_time_matches_mids() -> None:
    """ArcanaCastTime = (ceil(cast / 0.132) + 1) * 0.132 in single precision (Power.cs:603)."""
    assert arcana_cast_time(1.0) == pytest.approx(1.188, abs=1e-6)
    assert arcana_cast_time(1.67) == pytest.approx(1.848, abs=1e-6)
    assert arcana_cast_time(1.0) == f32(arcana_cast_time(1.0))


def test_three_attack_known_optimum() -> None:
    """A weak third attack lengthens the cycle past the bottleneck, so the best chain drops it."""
    plan = _plan([_atk("A", damage=100), _atk("B", damage=60), _atk("C", damage=10)])
    best = plan.best_overall
    assert best is not None
    assert best.rotation == ("A", "B")
    # cast_sum 2 * 1.188; bottleneck 1.188 + 2 = 3.188 -> cycle 3.188, gap 3.188 - 2.376.
    assert best.cycle_time == pytest.approx(3.188, abs=1e-5)
    assert best.gap == pytest.approx(0.812, abs=1e-5)
    assert best.gap_free is False
    assert best.dps == pytest.approx(160.0 / 3.188, rel=1e-5)
    assert best.end_per_sec == pytest.approx(10.0 / 3.188, rel=1e-5)
    assert len(plan.ranked) == 7
    assert plan.ranked[0] == best
    assert plan.dropped == ()


def test_gap_free_option() -> None:
    """Three 1.188 s casts cover a 2 s recharge, so the full chain is gap-free."""
    plan = _plan([_atk("A"), _atk("B"), _atk("C")])
    full = next(o for o in plan.ranked if len(o.rotation) == 3)
    assert full.gap_free is True
    assert full.gap <= 0.0
    assert full.cycle_time == pytest.approx(3.564, abs=1e-5)


def test_best_sustainable_differs_from_best_overall() -> None:
    """An expensive attack tops DPS but drains faster than recovery; the sustainable pick avoids it."""
    budget = EnduranceBudget(max_end=100.0, recovery_per_sec=2.0, toggle_drain_per_sec=0.0)
    plan = _plan([_atk("X", damage=500, end=40.0), _atk("Y", damage=50, end=2.0)], budget=budget)
    assert plan.best_overall is not None
    assert plan.best_sustainable is not None
    assert plan.best_overall.rotation == ("X", "Y")
    assert plan.best_overall.sustainable is False
    assert plan.best_overall.net_end_per_sec < 0.0
    assert plan.best_overall.time_to_empty == pytest.approx(100.0 / 11.17, rel=1e-2)
    assert plan.best_sustainable.rotation == ("Y",)
    assert plan.best_sustainable.sustainable is True
    assert plan.best_sustainable.time_to_empty == inf
    assert plan.ranked[0] == plan.best_sustainable


def test_no_sustainable_option() -> None:
    """With recovery below every option's drain, best_sustainable is None and best_overall still reports."""
    budget = EnduranceBudget(max_end=100.0, recovery_per_sec=0.1, toggle_drain_per_sec=0.0)
    plan = _plan([_atk("X", end=40.0)], budget=budget)
    assert plan.best_sustainable is None
    assert plan.best_overall is not None


def test_time_to_empty_floor_outranks_dps() -> None:
    """Among unsustainable options, one that lasts past the minimum outranks a faster one that does not."""
    budget = EnduranceBudget(max_end=100.0, recovery_per_sec=0.0, toggle_drain_per_sec=0.0)
    plan = _plan([_atk("Hot", damage=500, end=100.0), _atk("Cool", damage=10, end=1.0)], budget=budget)
    assert plan.ranked[0].rotation == ("Cool",)
    assert plan.best_overall is not None
    assert plan.best_overall.rotation == ("Cool", "Hot")


def test_other_drain_makes_options_unsustainable() -> None:
    """Drain from other sources is added to the chain's before the net rate is taken."""
    budget = EnduranceBudget(max_end=100.0, recovery_per_sec=3.0, toggle_drain_per_sec=0.0)
    assert _plan([_atk("A")], budget=budget).best_sustainable is not None
    assert _plan([_atk("A")], budget=budget, other_drain_per_sec=5.0).best_sustainable is None


def test_ranged_stance_drops_melee_attacks() -> None:
    """Under the ranged stance, attacks that need melee position are not candidates (and not 'dropped')."""
    attacks = [_atk("Melee"), _atk("Bolt", melee=False), _atk("Burst", melee=False, shape=Shape.PBAOE)]
    plan = _plan(attacks, Stance.RANGED)
    assert {o.rotation for o in plan.ranked} == {("Bolt",)}
    assert plan.dropped == ()


def test_hybrid_mixing_pays_two_switches() -> None:
    """A hybrid subset mixing melee and ranged attacks pays 2 * switch_time; a pure subset pays none."""
    plan = _plan([_atk("M"), _atk("R", melee=False, recharge=9.0)], Stance.HYBRID)
    by_rotation = {o.rotation: o for o in plan.ranked}
    mixed = by_rotation[("M", "R")]  # melee-position first, despite R's longer recharge
    assert mixed.switches == 2
    assert mixed.move_time == pytest.approx(4.0)
    assert mixed.cycle_time == pytest.approx(max(2.376 + 4.0, 1.188 + 9.0), abs=1e-5)
    assert mixed.stance is Stance.HYBRID
    assert by_rotation[("R",)].switches == 0
    assert by_rotation[("R",)].move_time == 0.0
    assert by_rotation[("M",)].switches == 0


def test_melee_stance_never_pays_switches() -> None:
    """The melee stance stays put, so even mixed subsets cost no movement."""
    plan = _plan([_atk("M"), _atk("R", melee=False)], Stance.MELEE)
    assert all(o.switches == 0 and o.move_time == 0.0 for o in plan.ranked)


def test_infinite_switch_time_excludes_mixed_options() -> None:
    """A stationary character cannot switch, so hybrid subsets that mix positions are unusable."""
    stuck = Movement(speed_ft_per_sec=0.0, standoff_ft=20.0)
    attacks = [_atk("M"), _atk("R", melee=False)]
    assert {o.rotation for o in _plan(attacks, Stance.HYBRID, movement=stuck).ranked} == {("M",), ("R",)}
    # A melee-stance plan never consults the infinite switch time.
    assert len(_plan(attacks, Stance.MELEE, movement=stuck).ranked) == 3


def test_aoe_damage_scales_with_targets_hit() -> None:
    """Area attacks multiply damage by the foes in reach, capped by max_targets."""
    aoe = _atk("Nova", damage=100.0, shape=Shape.TARGETED_AOE, max_targets=10)
    single = _plan([aoe], scenario=Scenario(targets=1)).best_overall
    crowd = _plan([aoe], scenario=Scenario(targets=5)).best_overall
    assert single is not None
    assert crowd is not None
    assert single.damage_per_cycle == pytest.approx(100.0)
    assert crowd.damage_per_cycle == pytest.approx(500.0)


def test_timeline_available_scales_dps_and_drain() -> None:
    """Half the timeline halves both the chain's DPS and its endurance drain."""
    full = _plan([_atk("A")]).best_overall
    half = _plan([_atk("A")], timeline_available=0.5).best_overall
    assert full is not None
    assert half is not None
    assert half.dps == pytest.approx(full.dps / 2.0)
    assert half.end_per_sec == pytest.approx(full.end_per_sec / 2.0)
    assert half.cycle_time == full.cycle_time


@pytest.mark.parametrize("bad", [0.0, -0.1, 1.5])
def test_timeline_out_of_range_is_e45(bad: float) -> None:
    """timeline_available must lie in (0, 1]."""
    with pytest.raises(ValueError, match="E45"):
        _plan([_atk("A")], timeline_available=bad)


def test_max_attacks_keeps_best_damage_per_cast() -> None:
    """Above max_attacks, the attacks with the lowest damage per arcana cast are dropped."""
    plan = _plan([_atk("Lo", damage=10), _atk("Hi", damage=90), _atk("Mid", damage=50)], max_attacks=2)
    assert plan.dropped == ("Lo",)
    assert {n for o in plan.ranked for n in o.rotation} == {"Hi", "Mid"}
    assert len(plan.ranked) == 3


def test_no_eligible_attacks_is_an_empty_plan() -> None:
    """Non-chain roles are ignored; with nothing left the plan is empty, not an error."""
    plan = _plan([_atk("Aura", role=Role.AURA)], Stance.HYBRID)
    assert plan.best_sustainable is None
    assert plan.best_overall is None
    assert plan.ranked == ()
    assert plan.dropped == ()
    assert plan.stance is Stance.HYBRID


def test_equal_dps_ties_break_on_gap_then_size_then_name() -> None:
    """Zero-damage attacks tie on DPS; the smaller gap, then fewer attacks, then names decide."""
    plan = _plan([_atk("B", damage=0.0), _atk("A", damage=0.0)])
    assert [o.rotation for o in plan.ranked] == [("A", "B"), ("A",), ("B",)]
    assert plan.best_overall == plan.ranked[0]


def test_twelve_attacks_search_all_subsets() -> None:
    """12 attacks (4095 subsets) are all searched."""
    plan = _plan([_atk(f"P{i:02d}", damage=10.0 + i, recharge=2.0 + i) for i in range(12)])
    assert len(plan.ranked) == 4095
