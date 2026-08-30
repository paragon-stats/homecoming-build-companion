"""Hard game-limit validation (the P8 pre-flight) as a rule registry.

The Homecoming engine enforces invariant limits on every build: at most 67 added
enhancement slots, 5 added slots per power, 24 power picks, 4 power pools, and a
level/slot schedule that gates when a power may be picked and when a slot may be
placed (.claude/rules/hard-limits.md). This module checks all of them.

Built as a rule registry from birth (never an ``if/elif`` dispatch), mirroring
:mod:`coh_engine.legality`: a pre-built :class:`HardLimitsContext` walks the build
once, each ``@hard_limit_rule`` is a pure ``(context) -> Iterable[Diagnostic]``
function naming its own ``H-<DOMAIN>-<NN>`` codes, and the runner only composes.
Adding a limit is a new ``@hard_limit_rule``; it never edits the runner or a sibling
rule.

Every violation is an ``error`` (an invalid build), per :mod:`coh_engine.diagnostics`.
AT-cap adherence is *not* here — an over-cap total is game-clamped, not illegal, so it
stays a ``warning`` in :mod:`coh_engine.scoring` (the ``Totals``-vs-``TotalsCapped``
diff). This module owns only the structural invariants.

Level convention: ``Power.pick_level`` and ``SlotRef.level`` are 0-based level indices
(in-game level minus 1); a power's DB minimum ``Power.level`` is the 1-based in-game
level. The first-available check compares ``pick_level + 1`` against ``level``.

Spec: .claude/rules/hard-limits.md; .claude/rules/error-output.md; .claude/rules/check-registry.md.
"""

from collections.abc import Callable, Iterable, Mapping, Sequence
from dataclasses import dataclass, field
from typing import Any

from coh_engine.diagnostics import Diagnostic
from coh_engine.effect import Power
from coh_engine.enhancement import SlotRef
from coh_engine.levels import LevelSchedule

# Group prefix (first dotted segment of a power full name) → build role.
_INHERENT_GROUP = "Inherent"
_INCARNATE_GROUP = "Incarnate"
_POOL_GROUP = "Pool"
_ANCILLARY_GROUP = "Epic"  # Ancillary / Patron pools are the "Epic" group.

# Hard game limits (.claude/rules/hard-limits.md § Character-level limits). MaxSlots is
# read from the server dump; the rest are ruleset invariants.
MAX_POWER_PICKS = 24
MAX_ADDED_SLOTS_PER_POWER = 5
MAX_POWER_POOLS = 4


def _group(power: Power) -> str:
    """The first dotted segment of a power's full name (its group)."""
    return power.full_name.split(".", 1)[0]


def _powerset_key(power: Power) -> str:
    """The ``Group.PowerSet`` prefix — the powerset a power belongs to."""
    return ".".join(power.full_name.split(".")[:2])


def _is_inherent(power: Power) -> bool:
    """Whether a power is auto-granted (the ``Inherent`` group) — excluded from picks.

    Inherent powers (Fitness Health/Stamina/Swift/Hurdle, Brawl/Sprint/Rest) are NOT power
    picks, but they ARE slottable: their added enhancement slots count against the 67-slot
    budget (Mids ``Power.Slottable`` whitelists SetType ``Inherent``). So this predicate
    gates picks only — slot accounting walks all powers regardless.
    """
    return _group(power) == _INHERENT_GROUP


def _is_incarnate(power: Power) -> bool:
    """Whether a power is an incarnate (the ``Incarnate`` group) — excluded from picks.

    Incarnates are a separate crafting system (level-50, XP-unlocked slots, salvage-crafted,
    one power equipped per incarnate slot); they never consume one of the 24 core picks
    (Mids auto-adds them via ``AddPower`` with ``chosen=false``). They also take no
    enhancement slots (``Power.Slottable`` excludes SetType ``Incarnate``; ``PowerEntry``
    refuses a default slot when the group is ``Incarnate``), so they contribute 0 to the
    slot budget on their own — a distinct treatment from inherents, kept as its own
    predicate rather than merged with :func:`_is_inherent`.
    """
    return _group(power) == _INCARNATE_GROUP


def _added_slots(power_slots: Sequence[SlotRef]) -> list[SlotRef]:
    """The power's added slots — every slot that draws from the 67-slot budget.

    A power's first slot (index 0) is its free default ``(A)`` slot, placed at the pick
    level; it does not count against the budget. Any further ``is_inherent`` slot is the
    Homecoming inherent-slotting grant (also free, and disabled on Homecoming). Everything
    else is an added slot — one of the 67.
    """
    return [slot for slot in power_slots[1:] if not slot.is_inherent]


@dataclass(frozen=True, slots=True)
class HardLimitsContext:
    """The pre-built inputs every hard-limit rule reads — the build walked once.

    ``picks`` is the real (non-inherent, build-index ≥ 0) picked powers in build order.
    ``added_by_power`` maps a power's build index to its added-slot count. ``added_by_level``
    maps a slot-grant level index to how many added slots the build placed there (the
    per-level capacity check). Building these once means no rule re-walks the slots.
    """

    powers: tuple[Power, ...]
    slots: Mapping[int, tuple[SlotRef, ...]]
    schedule: LevelSchedule
    max_slots: int
    picks: tuple[Power, ...] = field(default_factory=tuple)
    added_by_power: Mapping[int, int] = field(default_factory=dict)
    added_by_level: Mapping[int, int] = field(default_factory=dict)
    # Every (power, added slot) pair, walked once here so the slot-placement rules read it
    # instead of each re-walking the build (check-registry "context walks once").
    added_pairs: tuple[tuple[Power, SlotRef], ...] = field(default_factory=tuple)


def build_context(
    powers: Sequence[Power],
    slots: Mapping[int, Sequence[SlotRef]],
    schedule: LevelSchedule,
    max_slots: int,
) -> HardLimitsContext:
    """Assemble the shared context every rule reads (the single build walk).

    Picks exclude anything the player did not spend a pick on: ``PowerEntry.Chosen`` is
    false for every power Mids auto-adds, and the ``Inherent`` / ``Incarnate`` groups are
    excluded by name as well so dumps predating the ``Chosen`` field still classify right.
    ``Chosen`` is what catches an auto-granted *pool* sub-power — Mystic Flight grants
    Translocation into ``Pool.Sorcery``, which no group check can tell from a real pick.
    Slot accounting walks all powers, so inherent Health/Stamina slots still count against
    the budget and slotless incarnates contribute 0.
    """
    picks = tuple(p for p in powers if p.build_index >= 0 and p.chosen and not _is_inherent(p) and not _is_incarnate(p))
    added_by_power: dict[int, int] = {}
    added_by_level: dict[int, int] = {}
    added_pairs: list[tuple[Power, SlotRef]] = []
    for power in powers:
        added = _added_slots(slots.get(power.build_index, ()))
        if added:
            added_by_power[power.build_index] = len(added)
        for slot in added:
            added_by_level[slot.level] = added_by_level.get(slot.level, 0) + 1
            added_pairs.append((power, slot))
    return HardLimitsContext(
        powers=tuple(powers),
        slots={idx: tuple(s) for idx, s in slots.items()},
        schedule=schedule,
        max_slots=max_slots,
        picks=picks,
        added_by_power=added_by_power,
        added_by_level=added_by_level,
        added_pairs=tuple(added_pairs),
    )


HardLimitRule = Callable[[HardLimitsContext], Iterable[Diagnostic]]
_RULES: list[HardLimitRule] = []


def hard_limit_rule(fn: HardLimitRule) -> HardLimitRule:
    """Register a hard-limit rule — a new limit is a new ``@hard_limit_rule``, no runner edit."""
    _RULES.append(fn)
    return fn


def registered_hard_limit_rules() -> list[str]:
    """The names of the registered hard-limit rules — the introspectable registry."""
    return [rule.__name__ for rule in _RULES]


def _error(rule_id: str, message: str, **ctx: Any) -> Diagnostic:
    return Diagnostic(rule_id=rule_id, severity="error", message=message, **ctx)


@hard_limit_rule
def _rule_total_added_slots(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-SLOT-001: total added slots across the build must not exceed the cap (67)."""
    total = sum(ctx.added_by_power.values())
    if total > ctx.max_slots:
        yield _error(
            "H-SLOT-001",
            f"total added slots ({total}) exceeds the cap ({ctx.max_slots})",
            fix=f"remove {total - ctx.max_slots} slot(s) from one or more powers",
            expected=ctx.max_slots,
            actual=total,
        )


@hard_limit_rule
def _rule_per_power_added_slots(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-SLOT-002: a power may hold at most 5 added slots (1 inherent + 5 = 6 total)."""
    for power in ctx.powers:
        count = ctx.added_by_power.get(power.build_index, 0)
        if count > MAX_ADDED_SLOTS_PER_POWER:
            yield _error(
                "H-SLOT-002",
                f"{power.full_name!r} has {count} added slots; a power may hold at most "
                f"{MAX_ADDED_SLOTS_PER_POWER} (plus its inherent slot)",
                fix=f"remove {count - MAX_ADDED_SLOTS_PER_POWER} slot(s) from this power",
                expected=MAX_ADDED_SLOTS_PER_POWER,
                actual=count,
            )


@hard_limit_rule
def _rule_slot_after_power(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-SLOT-003: an added slot cannot be placed before its power is available (its pick level)."""
    for power, slot in ctx.added_pairs:
        if slot.level < power.pick_level:
            yield _error(
                "H-SLOT-003",
                f"a slot in {power.full_name!r} is placed at level {slot.level + 1}, before the "
                f"power was picked (level {power.pick_level + 1})",
                fix="place the slot at or after the power's pick level",
                expected=power.pick_level + 1,
                actual=slot.level + 1,
            )


@hard_limit_rule
def _rule_slot_on_grant_level(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-SLOT-004: an added slot must land on a real slot-grant level in the schedule."""
    for power, slot in ctx.added_pairs:
        if slot.level not in ctx.schedule.slot_grant_levels:
            yield _error(
                "H-SLOT-004",
                f"a slot in {power.full_name!r} is placed at level {slot.level + 1}, which grants no "
                "enhancement slot in the level schedule",
                fix="move the slot to a level that grants a slot",
                actual=slot.level + 1,
            )


@hard_limit_rule
def _rule_slot_budget_over_time(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-SLOT-005: a slot cannot be placed before it is earned (cumulative budget).

    Slots are earned at grant levels and may be *deferred* to a later level (Mids carries
    the surplus forward — ``GetSlotCounts`` accumulates granted minus placed), so this is a
    cumulative check, not per-level: walking levels in order, the running count of added
    slots placed must never exceed the running count granted. Reports the first level where
    the build has placed more slots than it has earned.
    """
    running_granted = 0
    running_placed = 0
    for level in sorted(ctx.schedule.slot_grant_levels | set(ctx.added_by_level)):
        running_granted += ctx.schedule.slots_per_level.get(level, 0)
        running_placed += ctx.added_by_level.get(level, 0)
        if running_placed > running_granted:
            yield _error(
                "H-SLOT-005",
                f"by level {level + 1}, {running_placed} added slots are placed but only "
                f"{running_granted} have been granted (a slot cannot be placed before it is earned)",
                fix="move the excess slot(s) to a later grant level, or remove them",
                expected=running_granted,
                actual=running_placed,
            )
            return


@hard_limit_rule
def _rule_power_pick_count(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-POWER-001: a build may pick at most 24 powers (inherents excluded)."""
    if len(ctx.picks) > MAX_POWER_PICKS:
        yield _error(
            "H-POWER-001",
            f"the build picks {len(ctx.picks)} powers; the game grants only {MAX_POWER_PICKS} picks "
            "(inherents excluded)",
            fix=f"remove {len(ctx.picks) - MAX_POWER_PICKS} power pick(s)",
            expected=MAX_POWER_PICKS,
            actual=len(ctx.picks),
        )


@hard_limit_rule
def _rule_first_available_level(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-POWER-002: a power is picked at or after its earliest level (DB ``Level``).

    Generic first-availability gate: a power's DB minimum ``level`` (1-based) encodes its
    own earliest pick level, so this one rule covers primary/secondary tier unlocks (T8/T9
    at 26/32) and the Ancillary/Patron level-35 gate — no per-tier constants.
    """
    for power in ctx.picks:
        if power.level > 0 and power.pick_level + 1 < power.level:
            yield _error(
                "H-POWER-002",
                f"{power.full_name!r} is picked at level {power.pick_level + 1} but is not available until "
                f"level {power.level}",
                fix="pick this power at or after its earliest available level",
                expected=power.level,
                actual=power.pick_level + 1,
            )


@hard_limit_rule
def _rule_pick_at_grant_level(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-POWER-003: a power is picked at a real power-pick level in the schedule."""
    for power in ctx.picks:
        if power.pick_level not in ctx.schedule.power_pick_levels:
            yield _error(
                "H-POWER-003",
                f"{power.full_name!r} is picked at level {power.pick_level + 1}, which is not a "
                "power-pick level in the schedule",
                fix="pick this power at a level the schedule grants a power",
                actual=power.pick_level + 1,
            )


@hard_limit_rule
def _rule_pool_count(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-POOL-001: a build may draw from at most 4 power pools (Ancillary/Patron excluded)."""
    pools = {_powerset_key(p) for p in ctx.picks if _group(p) == _POOL_GROUP}
    if len(pools) > MAX_POWER_POOLS:
        yield _error(
            "H-POOL-001",
            f"the build draws from {len(pools)} power pools ({sorted(pools)}); the limit is {MAX_POWER_POOLS}",
            fix=f"drop a pool so at most {MAX_POWER_POOLS} remain",
            expected=MAX_POWER_POOLS,
            actual=len(pools),
        )


@hard_limit_rule
def _rule_prerequisites(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """Power prerequisites (``Build.MeetsRequirement`` steps C/D): required and forbidden picks.

    Ports the structured (no-evaluator) prerequisite check: a power's ``requires_powers``
    is an OR list of AND-groups, satisfied when *some* group has *all* its members already
    in the build, each taken at a level at or below this pick (H-POWER-004). This is exactly
    how pool and Ancillary/Patron higher-tier unlocks are encoded ("take 2 of the first 3"
    = three two-power groups). ``requires_powers_not`` is the forbidden/mutex list — any
    listed power present in the build is a conflict (H-POWER-005).
    """
    history = {p.full_name: p.pick_level for p in ctx.powers if p.build_index >= 0}
    for power in ctx.picks:
        yield from _prerequisite_diagnostics(power, history)


def _prerequisite_diagnostics(power: Power, history: Mapping[str, int]) -> Iterable[Diagnostic]:
    """The required/forbidden prerequisite diagnostics for one picked power."""
    if power.requires_powers and not any(
        all(req in history and history[req] <= power.pick_level for req in group) for group in power.requires_powers
    ):
        options = " or ".join(" + ".join(group) for group in power.requires_powers)
        yield _error(
            "H-POWER-004",
            f"{power.full_name!r} requires a prerequisite power ({options}) picked at or before it, "
            "but none is satisfied",
            fix="pick the prerequisite power(s) at an earlier level, or drop this power",
        )
    for forbidden in {name for group in power.requires_powers_not for name in group}:
        if forbidden in history:
            yield _error(
                "H-POWER-005",
                f"{power.full_name!r} is mutually exclusive with {forbidden!r}, which is also in the build",
                fix="remove one of the two mutually exclusive powers",
            )


@hard_limit_rule
def _rule_single_ancillary_pool(ctx: HardLimitsContext) -> Iterable[Diagnostic]:
    """H-POOL-002: at most one Ancillary/Patron (Epic) pool may be selected.

    A build commits to a single Ancillary or Patron Power Pool; drawing powers from two
    distinct ``Epic`` powersets is illegal (changing the chosen pool requires a respec).
    """
    ancillary = {_powerset_key(p) for p in ctx.picks if _group(p) == _ANCILLARY_GROUP}
    if len(ancillary) > 1:
        yield _error(
            "H-POOL-002",
            f"the build draws from {len(ancillary)} Ancillary/Patron pools ({sorted(ancillary)}); "
            "only one may be selected (changing it requires a respec)",
            fix="keep powers from a single Ancillary/Patron pool",
            expected=1,
            actual=len(ancillary),
        )


def check_hard_limits(
    powers: Sequence[Power],
    slots: Mapping[int, Sequence[SlotRef]],
    schedule: LevelSchedule,
    max_slots: int,
) -> list[Diagnostic]:
    """Every hard-limit violation, by running the registered rules over a pre-built context.

    The runner holds no per-dimension logic: it builds the :class:`HardLimitsContext` once
    and folds every ``@hard_limit_rule`` over it. A new hard limit is a new
    ``@hard_limit_rule`` — never an edit here. An empty list means the build is legal.
    """
    ctx = build_context(powers, slots, schedule, max_slots)
    return [diagnostic for rule in _RULES for diagnostic in rule(ctx)]
