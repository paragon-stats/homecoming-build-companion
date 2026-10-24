"""Layered damage model: registered damage sources scaled by registered damage modifiers.

A build's damage is not only its attack chain. Damage auras, location patches and pets run
beside the chain without spending cast time, while click buffs, target debuffs and AT
inherents scale everything. Each layer is one registered function (``.claude/rules/
check-registry.md``): a new source is a ``@damage_source``, a new modifier a
``@damage_modifier``, and :func:`evaluate_damage` folds them without per-kind logic.

Timeline: sources that spend cast time outside the chain (summons, patches, click buffs)
report a ``timeline_share``. Sources registered with ``fills_remainder=True`` (the attack
chain) run last, on the timeline the others left, and see the others' endurance drain.

Modifiers multiply source DPS by ``1 + (multiplier - 1) * uptime``. Damage *buffs* (Fury,
Build Up, Defiance) are additive in the game: they join the enhancement and global buffs
already inside the per-power damage figure, so an ``additive`` modifier scales by
``1 + sum(bonus * uptime) / baseline`` (one shared sum per source), where ``baseline`` is ``1 +``
the damage strength already applied (``DamageContext.damage_buff_baseline``, or a source's own override). A modifier
with ``applies_to`` set scales only the named sources; ``None`` scales every source.

**Novel to the port — no Mids parity reference.** Mids has no layered damage model; its DPS
calculator (``frmDPSCalc.cs``) sums one greedy chain. Per-power damage is an input here
(``damage_reference``) until the ``FXGetDamageValue`` port (#31) lands.

Spec: issues #67 (planner core), #68 (pet source), #69 (AT modifiers).
"""

from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, field, replace
from enum import Enum
from math import inf
from types import MappingProxyType

from coh_engine.maths import f32

# Source names a ModifierResult.applies_to may target; each source reports exactly one of these.
SOURCE_CHAIN = "chain"
SOURCE_AURA = "aura"
SOURCE_PATCH = "patch"
SOURCE_PET = "pet"


class Delivery(Enum):
    """Where an attack is delivered from: within melee reach, or at range."""

    MELEE = "melee"
    RANGED = "ranged"


class Shape(Enum):
    """The attack's area: one target, a cone, a sphere on a target, a sphere on self, a placed location."""

    SINGLE = "single"
    CONE = "cone"
    TARGETED_AOE = "targeted_aoe"
    PBAOE = "pbaoe"
    LOCATION = "location"


class Role(Enum):
    """How a damaging power participates: in the chain, beside it, or as a buff."""

    CHAIN = "chain"
    AURA = "aura"
    PATCH = "patch"
    PET_SUMMON = "pet_summon"
    BUFF_CLICK = "buff_click"


class Stance(Enum):
    """Where the character stands while running the chain."""

    MELEE = "melee"
    RANGED = "ranged"
    HYBRID = "hybrid"


@dataclass(frozen=True, slots=True)
class AttackProfile:
    """One power's classification, computed once (``attack_profile.classify_attack``)."""

    full_name: str
    delivery: Delivery
    shape: Shape
    role: Role
    range_ft: float
    radius_ft: float
    arc_rad: float
    max_targets: int
    set_categories: tuple[str, ...] = ()

    @property
    def needs_melee_position(self) -> bool:
        """Melee-delivered attacks and self-centred areas need the character in melee."""
        return self.delivery is Delivery.MELEE or self.shape is Shape.PBAOE


@dataclass(frozen=True, slots=True)
class EnduranceBudget:
    """The endurance a build has to spend: pool, income, and committed toggle drain (per second)."""

    max_end: float
    recovery_per_sec: float
    toggle_drain_per_sec: float

    def net_per_sec(self, extra_drain: float = 0.0) -> float:
        """Recovery minus toggle drain minus ``extra_drain`` (chain, patches, summons)."""
        return f32(self.recovery_per_sec - self.toggle_drain_per_sec - extra_drain)

    def time_to_empty(self, extra_drain: float = 0.0) -> float:
        """Seconds from full to empty at the net rate; ``inf`` when the build is sustainable."""
        net = self.net_per_sec(extra_drain)
        return inf if net >= 0.0 else f32(self.max_end / -net)


@dataclass(frozen=True, slots=True)
class ChainAttack:
    """A damaging or damage-buffing power: cadence, damage per activation, profile, buff/debuff riders.

    ``cast_time`` is the raw Mids cast time; the chain planner applies ArcanaTime itself.
    Buff and debuff fields are fractions (0.25 = +25% damage, 0.2 = -20% resistance on the target).
    """

    full_name: str
    damage: float
    cast_time: float
    recharge_time: float
    end_cost: float
    profile: AttackProfile
    activate_period: float = 0.0
    self_damage_buff: float = 0.0
    self_damage_buff_duration: float = 0.0
    target_res_debuff: float = 0.0
    target_res_debuff_duration: float = 0.0


@dataclass(frozen=True, slots=True)
class Scenario:
    """The fight being modelled: how many foes stand in reach of an area attack."""

    targets: int = 1

    def targets_hit(self, profile: AttackProfile) -> int:
        """Single-target powers hit one foe; area powers hit up to their cap."""
        if profile.shape is Shape.SINGLE:
            return 1
        return max(1, min(profile.max_targets or self.targets, self.targets))


@dataclass(frozen=True, slots=True)
class Movement:
    """How fast the character closes or opens range, and the standoff distance it keeps."""

    speed_ft_per_sec: float
    standoff_ft: float

    @property
    def switch_time(self) -> float:
        """Seconds to cross the standoff once (melee to range, or back)."""
        return f32(self.standoff_ft / self.speed_ft_per_sec) if self.speed_ft_per_sec > 0.0 else inf


@dataclass(frozen=True, slots=True)
class PetSummon:
    """A summon power with its pets' attacks precomputed (``pet_damage.build_pet_summons``).

    ``pet_attacks`` hold one pet's attacks, damage already at the pet's level and enhancement;
    ``duration`` is ``inf`` for a permanent pet. ``damage_buff_baseline`` is ``1 +`` the pet's
    damage strength, which additive modifiers divide by.
    """

    summon_name: str
    pet_name: str
    pet_attacks: tuple[ChainAttack, ...]
    pet_count: int
    duration: float
    summon_cast_time: float
    summon_recharge: float
    summon_end_cost: float
    damage_buff_baseline: float = 1.0
    label: str = ""


@dataclass(frozen=True, slots=True)
class DamageContext:
    """Everything a source or modifier reads, built once per evaluation."""

    attacks: tuple[ChainAttack, ...]
    budget: EnduranceBudget
    stance: Stance
    scenario: Scenario
    movement: Movement
    archetype_class: str
    assumptions: Mapping[str, float] = field(default_factory=lambda: MappingProxyType({}))
    damage_buff_baseline: float = 1.0
    pets: tuple[PetSummon, ...] = ()
    timeline_available: float = 1.0
    other_drain_per_sec: float = 0.0

    def by_role(self, role: Role) -> tuple[ChainAttack, ...]:
        """The context's attacks with the given role, in input order."""
        return tuple(a for a in self.attacks if a.profile.role is role)


@dataclass(frozen=True, slots=True)
class SourceResult:
    """One source's contribution: DPS, cast-time share, endurance drain, and a per-item breakdown."""

    source: str
    dps: float
    timeline_share: float = 0.0
    end_per_sec: float = 0.0
    detail: tuple[tuple[str, float], ...] = ()
    label: str = ""
    damage_buff_baseline: float | None = None


@dataclass(frozen=True, slots=True)
class ModifierResult:
    """One modifier: its multiplier at full uptime, its uptime, and the assumption behind it.

    ``additive`` marks a damage buff (it adds to the strength already in the damage figure);
    otherwise the multiplier is a true damage multiplier (Containment, critical hits).
    """

    modifier: str
    multiplier: float
    uptime: float = 1.0
    applies_to: frozenset[str] | None = None
    assumption: str = ""
    additive: bool = False

    def scale(self, baseline: float = 1.0) -> float:
        """The uptime-weighted factor; an additive buff is divided by ``baseline`` (1 + strength already applied)."""
        bonus = f32((self.multiplier - 1.0) * self.uptime)
        return f32(1.0 + bonus / baseline) if self.additive else f32(1.0 + bonus)


@dataclass(frozen=True, slots=True)
class DamageReport:
    """The evaluated model: every source and modifier result, and the scaled total."""

    sources: tuple[SourceResult, ...]
    modifiers: tuple[ModifierResult, ...]
    scaled_dps: Mapping[str, float]
    total_dps: float
    total_drain_per_sec: float
    time_to_empty: float


DamageSource = Callable[[DamageContext], SourceResult | None]
DamageModifier = Callable[[DamageContext], ModifierResult | None]

_SOURCES: list[tuple[DamageSource, bool]] = []
_MODIFIERS: list[DamageModifier] = []


def damage_source(*, fills_remainder: bool = False) -> Callable[[DamageSource], DamageSource]:
    """Register a damage source; ``fills_remainder`` sources run last on the leftover timeline."""

    def register(fn: DamageSource) -> DamageSource:
        _SOURCES.append((fn, fills_remainder))
        return fn

    return register


def damage_modifier(fn: DamageModifier) -> DamageModifier:
    """Register a damage modifier."""
    _MODIFIERS.append(fn)
    return fn


def registered_damage_sources() -> list[str]:
    """Names of the registered sources, in registration order."""
    return [fn.__name__ for fn, _ in _SOURCES]


def registered_damage_modifiers() -> list[str]:
    """Names of the registered modifiers, in registration order."""
    return [fn.__name__ for fn in _MODIFIERS]


def evaluate_damage(
    ctx: DamageContext,
    sources: Sequence[tuple[DamageSource, bool]] | None = None,
    modifiers: Sequence[DamageModifier] | None = None,
) -> DamageReport:
    """Fold every source and modifier over ``ctx``; the registries are the default set.

    Raises:
        ValueError: ``E30`` if the non-chain sources claim more than the whole timeline.
    """
    sources = _SOURCES if sources is None else sources
    modifiers = _MODIFIERS if modifiers is None else modifiers

    first = [r for fn, rest in sources if not rest and (r := fn(ctx)) is not None]
    share = f32(sum(r.timeline_share for r in first))
    if share > 1.0:
        raise ValueError(
            f"E30: non-chain damage sources claim {share:.3f} of the timeline (> 1.0): "
            f"{[(r.source, r.timeline_share) for r in first]}; a summon, patch or click buff reports "
            "a cast share above its recharge cadence"
        )
    drain = f32(sum(r.end_per_sec for r in first))
    rest_ctx = replace(ctx, timeline_available=f32(ctx.timeline_available - share), other_drain_per_sec=drain)
    results = (*first, *(r for fn, rest in sources if rest and (r := fn(rest_ctx)) is not None))

    # Same context as the chain source, so a modifier asking for the chosen chain gets that exact pick.
    mods = tuple(m for fn in modifiers if (m := fn(rest_ctx)) is not None)
    scaled: dict[str, float] = {}
    for r in results:
        baseline = ctx.damage_buff_baseline if r.damage_buff_baseline is None else r.damage_buff_baseline
        hit = [m for m in mods if m.applies_to is None or r.source in m.applies_to]
        # Damage buffs share one strength sum; only true multipliers compound.
        bonus = f32(sum(f32((m.multiplier - 1.0) * m.uptime) for m in hit if m.additive))
        factor = f32(1.0 + bonus / baseline)
        for m in hit:
            if not m.additive:
                factor = f32(factor * m.scale())
        scaled[r.source] = f32(r.dps * factor)
    total_drain = f32(sum(r.end_per_sec for r in results))
    return DamageReport(
        sources=results,
        modifiers=mods,
        scaled_dps=MappingProxyType(scaled),
        total_dps=f32(sum(scaled.values())),
        total_drain_per_sec=total_drain,
        time_to_empty=ctx.budget.time_to_empty(total_drain),
    )
