"""Archetype-inherent damage modifiers for the layered damage model.

Expected-value approximations, not a faithful port of MidsReborn: Mids has no model of
conditional inherents (Fury level, Containment share, Scourge chance, team size). Each
inherent with a damage effect becomes one ``@damage_modifier`` returning
``1 + (bonus at the assumed state)``; the state is read from ``ctx.assumptions`` under
``"<ClassName>.<param>"`` with a documented default. Archetypes whose inherent has no damage
effect return an explicit neutral result (1.0) with the reason, so a missing rule can never be
mistaken for neutrality (``tests/test_at_modifiers.py`` fails on an uncovered playable class).

Numbers come from ``data/canonical/powers/inherent/inherent/<power>.json`` where the file
carries them; any other number says "approximate; verify in MidsReborn".

Spec: issue #69 (AT modifiers); contract in ``damage_model``.
"""

import functools
from collections.abc import Callable, Mapping
from types import MappingProxyType

from coh_engine.damage_model import SOURCE_PET, DamageContext, DamageModifier, ModifierResult, damage_modifier
from coh_engine.maths import f32

_CANON = "data/canonical/powers/inherent/inherent"
_APPROX = "approximate; verify in MidsReborn"

_RULES: list[DamageModifier] = []
_CLASS_RULE: dict[str, str] = {}


def _param(ctx: DamageContext, cls: str, name: str, default: float, lo: float, hi: float) -> float:
    """Read ``"<cls>.<name>"`` from the assumptions, defaulting and range-checking it.

    Raises:
        ValueError: ``E50`` if the value lies outside ``[lo, hi]``.
    """
    key = f"{cls}.{name}"
    value = ctx.assumptions.get(key, default)
    if not lo <= value <= hi:
        raise ValueError(f"E50: assumption {key}={value} is outside [{lo}, {hi}]; set it within that range")
    return value


def _register(*classes: str) -> Callable[[DamageModifier], DamageModifier]:
    """Register a rule for ``classes``; the wrapper returns None for any other class."""

    def wrap(fn: DamageModifier) -> DamageModifier:
        @functools.wraps(fn)
        def gated(ctx: DamageContext) -> ModifierResult | None:
            return fn(ctx) if ctx.archetype_class in classes else None

        for c in classes:
            _CLASS_RULE[c] = fn.__name__
        _RULES.append(gated)
        return damage_modifier(gated)

    return wrap


def _neutral(rule: str, cls: str, why: str) -> None:
    """Register an explicit neutral rule for an archetype whose inherent has no damage effect."""

    def fn(ctx: DamageContext) -> ModifierResult | None:
        return ModifierResult(modifier=rule, multiplier=1.0, assumption=f"{cls}: neutral, {why}")

    fn.__name__ = rule
    _register(cls)(fn)


def at_modifier_rules() -> Mapping[str, str]:
    """Archetype class name -> name of the rule that handles it."""
    return MappingProxyType(dict(_CLASS_RULE))


def at_modifier_functions() -> tuple[DamageModifier, ...]:
    """The registered AT rules, in registration order (each returns None off its class)."""
    return tuple(_RULES)


@_register("Class_Brute")
def brute_fury(ctx: DamageContext) -> ModifierResult:
    """Fury: +2% damage per fury point (``rage_buff.json``: ``0.02 * source>kRage``)."""
    level = _param(ctx, "Class_Brute", "fury_level", 75.0, 0.0, 100.0)
    return ModifierResult(
        modifier="brute_fury",
        additive=True,
        multiplier=f32(1.0 + 0.02 * level),
        assumption=(
            f"Class_Brute.fury_level={level} of 0..100 (default 75: {_APPROX}); +2% per point from "
            f"{_CANON}/rage_buff.json, meter range 0..100 assumed"
        ),
    )


@_register("Class_Controller")
def controller_containment(ctx: DamageContext) -> ModifierResult:
    """Containment: double damage on controlled foes (``containment.json`` carries no magnitude)."""
    share = _param(ctx, "Class_Controller", "contained_share", 0.5, 0.0, 1.0)
    return ModifierResult(
        modifier="controller_containment",
        multiplier=f32(1.0 + share),
        assumption=(
            f"Class_Controller.contained_share={share} of damage lands on controlled foes (default 0.5, {_APPROX}); "
            f"containment adds +100% ({_APPROX}; {_CANON}/containment.json has no magnitude)"
        ),
    )


@_register("Class_Corruptor")
def corruptor_scourge(ctx: DamageContext) -> ModifierResult:
    """Scourge: chance of double damage, rising as foe health falls."""
    chance = _param(ctx, "Class_Corruptor", "scourge_chance", 0.10, 0.0, 1.0)
    return ModifierResult(
        modifier="corruptor_scourge",
        multiplier=f32(1.0 + chance),
        assumption=(
            f"Class_Corruptor.scourge_chance={chance} averaged over the fight (default 0.10, {_APPROX}); "
            f"scourge hits deal up to double damage per {_CANON}/scourge.json help text"
        ),
    )


@_register("Class_Blaster")
def blaster_defiance(ctx: DamageContext) -> ModifierResult:
    """Defiance: stacking damage bonus from attacking (``defiance.json`` carries no magnitude)."""
    bonus = _param(ctx, "Class_Blaster", "damage_bonus", 0.30, 0.0, 5.0)
    return ModifierResult(
        modifier="blaster_defiance",
        additive=True,
        multiplier=f32(1.0 + bonus),
        assumption=(
            f"Class_Blaster.damage_bonus={bonus} average stacked bonus (default 0.30, {_APPROX}); "
            f"{_CANON}/defiance.json gives only the 7.5 s stacking description"
        ),
    )


@_register("Class_Defender")
def defender_vigilance(ctx: DamageContext) -> ModifierResult:
    """Vigilance: +30% damage solo, -10% per teammate beyond the first, floor 0 (``vigilance.json``)."""
    team = _param(ctx, "Class_Defender", "team_size", 1.0, 1.0, 8.0)
    bonus = max(0.0, 0.3 - 0.1 * min(team - 1.0, 3.0))
    return ModifierResult(
        modifier="defender_vigilance",
        additive=True,
        multiplier=f32(1.0 + bonus),
        assumption=(
            f"Class_Defender.team_size={team} (default 1, solo); +30% less 10% per extra teammate up to 3, "
            f"from {_CANON}/vigilance.json"
        ),
    )


@_register("Class_Scrapper")
def scrapper_critical_hit(ctx: DamageContext) -> ModifierResult:
    """Critical hits: ``1 + chance * (crit_mult - 1)``; chance scales with target rank."""
    chance = _param(ctx, "Class_Scrapper", "crit_chance", 0.10, 0.0, 1.0)
    mult = _param(ctx, "Class_Scrapper", "crit_damage_mult", 2.0, 1.0, 4.0)
    return ModifierResult(
        modifier="scrapper_critical_hit",
        multiplier=f32(1.0 + chance * (mult - 1.0)),
        assumption=(
            f"Class_Scrapper.crit_chance={chance} (default 0.10, {_APPROX}; rank-dependent), "
            f"crit_damage_mult={mult} (double damage per {_CANON}/critical_hit.json help text)"
        ),
    )


@_register("Class_Stalker")
def stalker_assassination(ctx: DamageContext) -> ModifierResult:
    """Stalker criticals: 10% base chance plus 3% per team member (``assassination.json`` help text)."""
    team = _param(ctx, "Class_Stalker", "team_size", 1.0, 1.0, 8.0)
    mult = _param(ctx, "Class_Stalker", "crit_damage_mult", 2.0, 1.0, 4.0)
    chance = min(1.0, 0.10 + 0.03 * team)
    return ModifierResult(
        modifier="stalker_assassination",
        multiplier=f32(1.0 + chance * (mult - 1.0)),
        assumption=(
            f"Class_Stalker.team_size={team} (default 1) gives crit chance {chance:.2f} = 0.10 + 0.03 per member "
            f"from {_CANON}/assassination.json; crit_damage_mult={mult} (default 2.0, {_APPROX}); "
            "Hide guarantees crits and is not modelled"
        ),
    )


@_register("Class_Sentinel")
def sentinel_opportunity(ctx: DamageContext) -> ModifierResult:
    """Sentinel Scrutiny/Opportunity: the focused foe takes 5% more damage per Sentinel."""
    sentinels = _param(ctx, "Class_Sentinel", "sentinels", 1.0, 0.0, 8.0)
    return ModifierResult(
        modifier="sentinel_opportunity",
        additive=True,
        multiplier=f32(1.0 + 0.05 * sentinels),
        assumption=(
            f"Class_Sentinel.sentinels={sentinels} focusing the target (default 1); +5% each from "
            f"{_CANON}/scrutiny.json help text, full uptime assumed"
        ),
    )


@_register("Class_Mastermind")
def mastermind_supremacy(ctx: DamageContext) -> ModifierResult:
    """Supremacy: +25% henchmen damage (``supremacy.json``), pets only."""
    share = _param(ctx, "Class_Mastermind", "pets_in_range", 1.0, 0.0, 1.0)
    return ModifierResult(
        modifier="mastermind_supremacy",
        additive=True,
        multiplier=f32(1.0 + 0.25 * share),
        applies_to=frozenset({SOURCE_PET}),
        assumption=(
            f"Class_Mastermind.pets_in_range={share} share of pets within range (default 1); "
            f"+25% damage from {_CANON}/supremacy.json"
        ),
    )


@_register("Class_Peacebringer")
def peacebringer_cosmic_balance(ctx: DamageContext) -> ModifierResult:
    """Cosmic Balance: +20% damage per nearby Tanker, Mastermind, Corruptor or Defender (``cosmic_balance.json``)."""
    allies = _param(ctx, "Class_Peacebringer", "damage_allies", 0.0, 0.0, 7.0)
    return ModifierResult(
        modifier="peacebringer_cosmic_balance",
        additive=True,
        multiplier=f32(1.0 + 0.2 * allies),
        assumption=(
            f"Class_Peacebringer.damage_allies={allies} nearby Tanker/Mastermind/Corruptor/Defender (default 0, solo); "
            f"+20% each from {_CANON}/cosmic_balance.json"
        ),
    )


@_register("Class_Warshade")
def warshade_dark_sustenance(ctx: DamageContext) -> ModifierResult:
    """Dark Sustenance: +20% damage per nearby Scrapper, Sentinel, Stalker, Brute or Blaster (``dark_sustenance.json``)."""
    allies = _param(ctx, "Class_Warshade", "damage_allies", 0.0, 0.0, 7.0)
    return ModifierResult(
        modifier="warshade_dark_sustenance",
        additive=True,
        multiplier=f32(1.0 + 0.2 * allies),
        assumption=(
            f"Class_Warshade.damage_allies={allies} nearby Scrapper/Sentinel/Stalker/Brute/Blaster (default 0, solo); "
            f"+20% each from {_CANON}/dark_sustenance.json (the Warshade's Cosmic Balance counterpart)"
        ),
    )


_neutral(
    "dominator_domination",
    "Class_Dominator",
    "Domination grants +25% to-hit, endurance refill and control magnitude, no damage effect "
    f"({_CANON}/domination.json); to-hit and control are not modelled",
)
_neutral(
    "tanker_gauntlet",
    "Class_Tanker",
    f"Gauntlet only grants/revokes the taunt power, no damage effect ({_CANON}/gauntlet.json)",
)
_neutral(
    "arachnos_soldier_conditioning",
    "Class_Arachnos_Soldier",
    f"Conditioning adds regeneration and endurance, no damage effect ({_CANON}/spider_conditioning.json)",
)
_neutral(
    "arachnos_widow_conditioning",
    "Class_Arachnos_Widow",
    f"Conditioning adds regeneration and endurance, no damage effect ({_CANON}/widow_conditioning.json)",
)
