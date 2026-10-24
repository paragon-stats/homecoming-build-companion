"""Attack-chain sequencer — a repeatable attack rotation and its endurance cost.

Given a build's attacks and their CP6.1 buffed recharge / cast times
(:class:`~coh_engine.statistics.PowerStats`), this orders them into a rotation and
reports whether the chain is gap-free and what it drains per second. It feeds the full
endurance-sustainability check (:mod:`coh_engine.scoring`): a build must sustain its
toggle drain **plus** its attack-chain drain from recovery.

**Novel to the port — no Mids parity reference.** Mids has no attack-chain sequencer in
its calc core (the UI "Rotation Helper" sequences for damage, not endurance). The
per-attack cadence this consumes *is* parity-validated (``statistics`` reproduces Mids'
buffed recharge/cast), but the rotation and gap logic here is the port's own.

The chain model (standard CoH): each attack fires once per cycle, and its recharge runs
concurrently with the other attacks' activations. So an attack ``i`` is ready in time iff
the rest of the chain's cast time covers its recharge — ``cast_sum - cast_i >= recharge_i``,
i.e. ``cast_sum >= cast_i + recharge_i``. The chain is gap-free iff ``cast_sum`` covers the
worst attack's ``cast_i + recharge_i``; otherwise the cycle stretches to that bottleneck
and the difference is idle (gap) time. Ordering does not change the cycle time, the gap, or
the drain — only the presentation order — so the endurance math is order-independent.

Damage / chain DPS are deferred (CP6.2 / #31): identifying an attack needs only that it
deals damage, not the damage value, so the sequencer runs on recharge/cast alone.

:func:`plan_attack_chain` (issue #67) extends this to a chain *planner*: it searches every subset of
the damaging chain attacks on arcana cast times (``Power.ArcanaCastTime``, the one Mids formula
reused here), charges stance-switch movement, and ranks rotations by endurance sustainability then
DPS. The search, the movement cost and the ranking are the port's own.

Spec: .claude/rules/exemplar-10.md; docs/engine/build-types-and-goals.md.
"""

from collections.abc import Iterable, Sequence
from dataclasses import dataclass
from math import ceil, inf

from coh_engine.damage_model import ChainAttack, EnduranceBudget, Movement, Role, Scenario, Stance
from coh_engine.maths import f32
from coh_engine.statistics import PowerStats


@dataclass(frozen=True, slots=True)
class AttackChain:
    """A build's attack rotation and the endurance it costs per second.

    ``rotation`` is the attack full-names ordered longest-recharge first (presentation
    only). ``cast_sum`` is the animation-locked cycle floor (Σ cast times); ``cycle_time``
    is the real cycle including any wait for a slow attack; ``gap`` is the idle time per
    cycle (0 when gap-free). ``end_per_sec`` is the chain's endurance drain, Σ end cost over
    the cycle.
    """

    rotation: tuple[str, ...]
    cast_sum: float
    cycle_time: float
    gap: float
    gap_free: bool
    end_per_sec: float


_EMPTY_CHAIN = AttackChain(rotation=(), cast_sum=0.0, cycle_time=0.0, gap=0.0, gap_free=True, end_per_sec=0.0)


def build_attack_chain(power_stats: Sequence[PowerStats]) -> AttackChain:
    """Sequence a build's attacks into a rotation and derive its endurance drain.

    Attacks are the ``is_attack`` power stats. With none, the chain is empty (no drain,
    trivially gap-free). ``cycle_time`` is ``max(Σ cast, max(cast_i + recharge_i))``; the
    chain is gap-free when the cast total already covers the slowest attack's recharge.
    """
    attacks = [s for s in power_stats if s.is_attack]
    if not attacks:
        return _EMPTY_CHAIN

    cast_sum = _f32_sum(s.cast_time for s in attacks)
    end_sum = _f32_sum(s.end_cost for s in attacks)
    bottleneck = max(f32(s.cast_time + s.recharge_time) for s in attacks)
    cycle_time = max(cast_sum, bottleneck)
    gap = f32(cycle_time - cast_sum)
    rotation = tuple(s.full_name for s in sorted(attacks, key=lambda s: (-s.recharge_time, s.full_name)))
    # A real attack always has a non-zero cast (activation) time, so cycle_time > 0 whenever
    # a genuine attack is present. cycle_time == 0 means every "attack" has zero cast AND zero
    # recharge — a degenerate/malformed stat with no meaningful cadence — so there is no rate
    # to report and the endurance drain is 0 (a divide-by-zero would be the only alternative).
    end_per_sec = f32(end_sum / cycle_time) if cycle_time > 0.0 else 0.0
    return AttackChain(
        rotation=rotation,
        cast_sum=cast_sum,
        cycle_time=cycle_time,
        gap=gap,
        gap_free=gap <= 0.0,
        end_per_sec=end_per_sec,
    )


_ARCANA_TICK = f32(0.132)


def arcana_cast_time(cast_time: float) -> float:
    """Round a raw cast time up to the game's 0.132 s tick, plus one tick (``Power.ArcanaCastTime``)."""
    # Power.cs:603: (float)(Math.Ceiling(CastTimeReal / 0.132f) + 1) * 0.132f
    return f32(f32(ceil(f32(cast_time / _ARCANA_TICK)) + 1) * _ARCANA_TICK)


@dataclass(frozen=True, slots=True)
class ChainOption:
    """One candidate rotation: a subset of attacks fired once per cycle, with its cadence and cost.

    ``cast_sum`` totals the arcana cast times; ``move_time`` is the stance-switch time per cycle
    (``switches`` crossings of the standoff); ``gap`` is idle time beyond casting and moving.
    ``dps`` and ``end_per_sec`` are already scaled by the timeline share the chain gets.
    ``time_to_empty`` is ``inf`` when ``sustainable``.
    """

    rotation: tuple[str, ...]
    stance: Stance
    cast_sum: float
    switches: int
    move_time: float
    cycle_time: float
    gap: float
    gap_free: bool
    damage_per_cycle: float
    dps: float
    end_per_sec: float
    net_end_per_sec: float
    time_to_empty: float
    sustainable: bool


@dataclass(frozen=True, slots=True)
class ChainPlan:
    """The planner's result: every usable option ranked, the two headline picks, and what was left out.

    ``dropped`` names the attacks excluded by ``max_attacks`` (not those excluded by stance).
    """

    stance: Stance
    best_sustainable: ChainOption | None
    best_overall: ChainOption | None
    ranked: tuple[ChainOption, ...]
    dropped: tuple[str, ...]


def plan_attack_chain(
    attacks: Sequence[ChainAttack],
    *,
    stance: Stance,
    movement: Movement,
    scenario: Scenario,
    budget: EnduranceBudget,
    timeline_available: float = 1.0,
    other_drain_per_sec: float = 0.0,
    time_to_empty_min_s: float = 120.0,
    max_attacks: int = 12,
) -> ChainPlan:
    """Search every subset of the chain attacks and rank them by sustainability, then damage.

    Each attack fires once per cycle (the :func:`build_attack_chain` model) using arcana cast
    times. A hybrid subset mixing melee-position and other attacks pays two stance switches per
    cycle. Options rank sustainable first, then those lasting ``time_to_empty_min_s``, then higher
    DPS, smaller gap, fewer attacks and rotation names. ``best_overall`` is the highest-DPS option.

    Raises:
        ValueError: ``E45`` if ``timeline_available`` is outside ``(0, 1]``.
    """
    if not 0.0 < timeline_available <= 1.0:
        raise ValueError(
            f"E45: timeline_available is {timeline_available} but must lie in (0, 1]; "
            "pass the timeline share left after non-chain sources"
        )
    eligible = [
        a
        for a in attacks
        if a.profile.role is Role.CHAIN and not (stance is Stance.RANGED and a.profile.needs_melee_position)
    ]
    rows = [(a, arcana_cast_time(a.cast_time)) for a in eligible]
    dropped: tuple[str, ...] = ()
    if len(rows) > max_attacks:
        rows.sort(key=lambda r: (-f32(r[0].damage / r[1]), r[0].full_name))
        dropped = tuple(a.full_name for a, _ in rows[max_attacks:])
        rows = rows[:max_attacks]
    # Melee-position attacks first, then longest recharge, so every subset is already in rotation order.
    rows.sort(key=lambda r: (not r[0].profile.needs_melee_position, -r[0].recharge_time, r[0].full_name))

    switch_time = movement.switch_time
    scaled = f32(timeline_available)
    per_attack = [
        (
            a.full_name,
            a.profile.needs_melee_position,
            cast,
            f32(cast + a.recharge_time),
            f32(a.damage * scenario.targets_hit(a.profile)),
            a.end_cost,
        )
        for a, cast in rows
    ]
    options: list[ChainOption] = []
    for mask in range(1, 1 << len(per_attack)):
        picked = [p for i, p in enumerate(per_attack) if mask >> i & 1]
        mixed = any(p[1] for p in picked) and not all(p[1] for p in picked)
        switches = 2 if stance is Stance.HYBRID and mixed else 0
        if switches and switch_time == inf:
            continue
        move_time = f32(switches * switch_time) if switches else 0.0
        cast_sum = _f32_sum(p[2] for p in picked)
        cycle_time = max(f32(cast_sum + move_time), *(p[3] for p in picked))
        gap = f32(cycle_time - cast_sum - move_time)
        damage = _f32_sum(p[4] for p in picked)
        end_per_sec = f32(f32(_f32_sum(p[5] for p in picked) / cycle_time) * scaled)
        drain = f32(other_drain_per_sec + end_per_sec)
        net = budget.net_per_sec(drain)
        options.append(
            ChainOption(
                rotation=tuple(p[0] for p in picked),
                stance=stance,
                cast_sum=cast_sum,
                switches=switches,
                move_time=move_time,
                cycle_time=cycle_time,
                gap=gap,
                gap_free=gap <= 0.0,
                damage_per_cycle=damage,
                dps=f32(f32(damage / cycle_time) * scaled),
                end_per_sec=end_per_sec,
                net_end_per_sec=net,
                time_to_empty=budget.time_to_empty(drain),
                sustainable=net >= 0.0,
            )
        )

    def rank(o: ChainOption) -> tuple[bool, bool, float, float, int, tuple[str, ...]]:
        return (not o.sustainable, o.time_to_empty < time_to_empty_min_s, -o.dps, o.gap, len(o.rotation), o.rotation)

    ranked = tuple(sorted(options, key=rank))
    best_overall = min(ranked, key=lambda o: (-o.dps, o.gap, len(o.rotation), o.rotation), default=None)
    best_sustainable = next((o for o in ranked if o.sustainable), None)
    return ChainPlan(
        stance=stance,
        best_sustainable=best_sustainable,
        best_overall=best_overall,
        ranked=ranked,
        dropped=dropped,
    )


def _f32_sum(values: Iterable[float]) -> float:
    total = 0.0
    for value in values:
        total = f32(total + value)
    return total
