"""Chain report: build a :class:`DamageContext` from a Mids dump and evaluate it per stance.

This is the execution surface of the layered damage model (issues #67, #68, #69). It loads a
build dump, classifies its powers, builds the endurance and health budgets and the pet summons,
folds the registered sources and modifiers (:mod:`coh_engine.damage_plugins`) for each requested
stance, and runs the ``@chain_rule`` diagnostics over every result.

Inputs. Damage figures and the buff and debuff riders come from the *clicks-off* dump (the build
with its click buffs unslotted into the totals), so the click-buff modifier is not double counted
against ``TotalsCapped.buff_dam``. With no clicks-off dump the build dump is used and click buffs
count as always on (``P-CHAIN-002``).

Movement speed. ``run_spd``, ``jump_spd`` and ``fly_spd`` are in feet per second already:
``base_totals._gbd_movement`` scales ``ServerData.BaseRunSpeed`` (21) and its siblings, and Mids
converts to miles per hour only for display (``Statistics.cs:141-143``: ``FeetPerSecond => iSpeed``,
``MilesPerHour => iSpeed * 0.6818182f``). No conversion is applied here.

**Novel to the port: no Mids parity reference.**
"""

import dataclasses
import json
from collections.abc import Callable, Iterable, Iterator, Mapping, Sequence
from dataclasses import dataclass, replace
from math import isinf
from pathlib import Path
from types import MappingProxyType
from typing import Any

from coh_engine import damage_plugins
from coh_engine.archetypes import Archetype, load_archetypes
from coh_engine.attack_chain import ChainOption
from coh_engine.attack_profile import category_disagreement, classify_attack, load_canonical_powers
from coh_engine.attribmod import load_attribmod
from coh_engine.base_totals import TotalStatistics
from coh_engine.damage_model import (
    SOURCE_CHAIN,
    ChainAttack,
    DamageContext,
    DamageReport,
    Movement,
    PetSummon,
    Role,
    Scenario,
    Stance,
    evaluate_damage,
)
from coh_engine.damage_reference import PowerDamageRef, load_damage_refs
from coh_engine.damage_sources import chosen_chain
from coh_engine.diagnostics import Diagnostic
from coh_engine.endurance_budget import (
    HealthBudget,
    build_endurance_budget,
    build_health_budget,
    load_canonical_caps,
    load_mids_totals,
)
from coh_engine.maths import f32
from coh_engine.pet_damage import build_pet_summons
from coh_engine.profiles import load_breakpoints

_ = damage_plugins  # imported first so the default registries are populated

REPO_ROOT = Path(__file__).resolve().parents[2]
_DEFENSE_MELEE, _DEFENSE_RANGED = 10, 11  # Mids damage-type vector positions (Melee, Ranged)
# Approximate; verify in MidsReborn: typical level-50 average of an attack's damage enhancement.
DEFAULT_DAMAGE_ENHANCEMENT_AVG = 0.95
MOVES = ("run", "jump", "fly")


def parse_assumptions(items: Iterable[str]) -> dict[str, float]:
    """Parse ``KEY=VALUE`` strings into an assumptions mapping.

    Raises:
        ValueError: ``E67`` for an item without ``=``, an empty key or a non-numeric value.
    """
    out: dict[str, float] = {}
    for item in items:
        key, _, raw = item.partition("=")
        try:
            value = float(raw)
        except ValueError:
            value = None
        if value is None or not key.strip():
            raise ValueError(
                f"E67: assumption {item!r} is not KEY=VALUE with a numeric value; write e.g. --assume Brute.fury=0.5"
            )
        out[key.strip()] = value
    return out


def parse_target(text: str) -> Scenario:
    """Parse ``single`` or ``spawn:N`` into a :class:`Scenario`.

    Raises:
        ValueError: ``E68`` for any other text or ``N`` below 1.
    """
    if text == "single":
        return Scenario(targets=1)
    head, _, count = text.partition(":")
    if head == "spawn" and count.isdigit() and int(count) >= 1:
        return Scenario(targets=int(count))
    raise ValueError(f"E68: target {text!r} is neither 'single' nor 'spawn:N' (N >= 1); write e.g. --target spawn:5")


@dataclass(frozen=True, slots=True)
class ChainSetup:
    """Everything loaded once for a build; stance-specific contexts derive from ``context``.

    ``clicks_always_on`` records that no clicks-off dump was found.
    """

    context: DamageContext
    health: HealthBudget
    capped: TotalStatistics
    breakpoints: Mapping[str, Any]
    clicks_always_on: bool
    source: str


def _creates_entity(effects: Sequence[Mapping[str, Any]]) -> bool:
    return any("Create_Entity" in t["attribs"] for e in effects for t in e.get("templates", ())) or any(
        _creates_entity(e.get("child_effects", ())) for e in effects
    )


def _has_signal(ref: PowerDamageRef) -> bool:
    # Toggle and auto damage buffs are always on, so Mids' totals already hold them; only clicks need uptime.
    click_buff = ref.power_type == "Click" and ref.self_damage_buff
    return bool(ref.damage_per_activation or click_buff or ref.target_res_debuff)


def _candidates(refs: Mapping[str, PowerDamageRef], powers_root: Path) -> dict[str, Mapping[str, Any]]:
    """Canonical files of the powers worth classifying.

    A power with damage, a self damage buff or a target -Res debuff must have a canonical file (``E35``
    propagates). Any other power is kept only if its file exists and creates an entity: virtual powers
    such as ``Inherent.Inherent.Special_Set_Bonuses`` have no canonical file and are skipped.
    """
    # Incarnate summons and damage are out of scope here (#41).
    refs = {n: r for n, r in refs.items() if not n.startswith("Incarnate.")}
    found = dict(load_canonical_powers(powers_root, [n for n, r in refs.items() if _has_signal(r)]))
    for name, ref in refs.items():
        if _has_signal(ref):
            continue
        try:
            power = load_canonical_powers(powers_root, [name])[name]
        except ValueError as error:
            if str(error).startswith("E35"):
                continue
            raise
        if _creates_entity(power["effects"]):
            found[name] = power
    return found


def _attacks(
    refs: Mapping[str, PowerDamageRef], canonical_root: Path, melee_range_ft_max: float
) -> tuple[ChainAttack, ...]:
    powers = _candidates(refs, canonical_root / "powers")
    attacks: list[ChainAttack] = []
    for name, ref in refs.items():
        if name not in powers:
            continue
        try:
            profile = classify_attack(powers[name], melee_range_ft_max=melee_range_ft_max)
        except ValueError as error:
            if str(error).startswith("E37"):
                continue
            raise
        attacks.append(
            ChainAttack(
                full_name=name,
                damage=ref.damage_per_activation,
                cast_time=ref.cast_time,
                recharge_time=ref.recharge_time,
                end_cost=ref.end_cost,
                profile=profile,
                activate_period=ref.activate_period,
                self_damage_buff=ref.self_damage_buff,
                self_damage_buff_duration=ref.self_damage_buff_duration,
                target_res_debuff=ref.target_res_debuff,
                target_res_debuff_duration=ref.target_res_debuff_duration,
            )
        )
    return tuple(attacks)


def _speed(capped: TotalStatistics, move: str) -> float:
    """Feet per second for ``move``; the totals already hold ft/s (see the module docstring)."""
    speed = {"run": capped.run_spd, "jump": capped.jump_spd, "fly": capped.fly_spd}[move]
    if speed <= 0.0:
        raise ValueError(
            f"E69: movement mode {move!r} has speed {speed:g} ft/s in this build; choose another --move "
            "(a build without a flight power has no fly speed)"
        )
    return speed


def _required_file(path: Path, what: str) -> Path:
    if not path.is_file():
        raise ValueError(f"E65: {what} {path} not found; point the argument at a Mids dump directory")
    return path


def build_chain_setup(
    build_dir: Path,
    *,
    mids_dir: Path,
    clicksoff_dir: Path | None = None,
    canonical: Path | None = None,
    mids_db: Path | None = None,
    scenario: Scenario | None = None,
    move: str = "run",
    standoff_ft: float | None = None,
    assumptions: Mapping[str, float] | None = None,
) -> ChainSetup:
    """Load a dumped build into a melee-stance :class:`DamageContext` plus the budgets reports need.

    Args:
        build_dir: Mids dump build directory; also the source when no clicks-off dump exists.
        mids_dir: Shared dump directory holding ``archetypes.json``.
        clicksoff_dir: The clicks-off dump; default ``<build_dir>_clicksoff`` if it exists.
        canonical: ``data/canonical`` root; default the repository's.
        mids_db: Directory holding ``AttribMod.json``; required only when the build summons pets.
        scenario: Foes in reach; default a single target.
        move: ``run``, ``jump`` or ``fly``.
        standoff_ft: Ranged standoff; default ``ranged_standoff_ft`` from ``strategy/breakpoints.json``.
        assumptions: Overrides for named assumptions (see the modifier and source modules).

    Raises:
        ValueError: ``E65`` for a missing dump file, directory or unknown class; ``E66`` when pets are
            summoned and ``mids_db`` is missing; ``E69`` for a zero movement speed; ``E70`` for a
            standoff that is not positive; ``E71`` when no power deals damage. Loaders raise their own codes.
    """
    breakpoints = load_breakpoints(REPO_ROOT / "strategy" / "breakpoints.json")
    universal = breakpoints["universal"]
    canonical = canonical or REPO_ROOT / "data" / "canonical"
    default_off = build_dir.with_name(f"{build_dir.name}_clicksoff")
    if clicksoff_dir is not None and not clicksoff_dir.is_dir():
        raise ValueError(f"E65: clicks-off directory {clicksoff_dir} not found; fix --clicksoff-dir or omit it")
    source = clicksoff_dir or (default_off if default_off.is_dir() else build_dir)
    standoff = universal["ranged_standoff_ft"] if standoff_ft is None else standoff_ft
    if standoff <= 0.0:
        raise ValueError(f"E70: standoff is {standoff:g} ft but must be positive; pass --standoff with feet > 0")

    totals, capped = load_mids_totals(_required_file(source / "totals.json", "totals file"))
    class_name = json.loads((source / "totals.json").read_text(encoding="utf-8-sig"))["Class"]
    classes = load_archetypes(_required_file(mids_dir / "archetypes.json", "archetype table"))
    index = classes.nid_from_uid_class(class_name)
    if index < 0:
        raise ValueError(f"E65: class {class_name!r} is not in {mids_dir / 'archetypes.json'}; re-dump the build")
    archetype: Archetype = classes.classes[index]

    attacks = _attacks(load_damage_refs(source), canonical, float(universal["melee_range_ft_max"]))
    if not any(a.profile.role is not Role.PET_SUMMON for a in attacks):
        raise ValueError(f"E71: no power in {source} deals damage or buffs damage; point the build at an attacker")
    summons = [a for a in attacks if a.profile.role is Role.PET_SUMMON]
    pets: tuple[PetSummon, ...] = ()
    if summons:
        if mids_db is None:
            raise ValueError(
                f"E66: {[a.full_name for a in summons]} summon pets but --mids-db is not set; "
                "pass --mids-db with the directory holding AttribMod.json"
            )
        pets = build_pet_summons(
            summons,
            canonical_root=canonical,
            attribmod=load_attribmod(mids_db / "AttribMod.json"),
            classes=classes,
        )

    max_end_cap, _ = load_canonical_caps(canonical / "archetypes", class_name)
    merged = {"chain.min_cycles": float(universal["chain_min_cycles"]), **(assumptions or {})}
    enhancement_avg = merged.get("damage.enhancement_avg", DEFAULT_DAMAGE_ENHANCEMENT_AVG)
    context = DamageContext(
        attacks=attacks,
        budget=build_endurance_budget(totals, capped, archetype, max_end_cap=max_end_cap),
        stance=Stance.MELEE,
        scenario=scenario or Scenario(),
        movement=Movement(speed_ft_per_sec=_speed(capped, move), standoff_ft=float(standoff)),
        archetype_class=class_name,
        assumptions=MappingProxyType(merged),
        damage_buff_baseline=f32(1.0 + capped.buff_dam + enhancement_avg),
        pets=pets,
    )
    return ChainSetup(
        context=context,
        health=build_health_budget(capped, archetype),
        capped=capped,
        breakpoints=breakpoints,
        clicks_always_on=source == build_dir,
        source=str(source),
    )


def build_damage_context(build_dir: Path, **kwargs: Any) -> DamageContext:
    """The melee-stance :class:`DamageContext` of a dumped build; keywords as :func:`build_chain_setup`."""
    return build_chain_setup(build_dir, **kwargs).context


@dataclass(frozen=True, slots=True)
class RuleContext:
    """What a ``@chain_rule`` reads: one stance's evaluation and the setup it came from."""

    setup: ChainSetup
    context: DamageContext
    report: DamageReport
    chain: ChainOption | None
    path: str


ChainRule = Callable[[RuleContext], Iterable[Diagnostic]]
_CHAIN_RULES: list[ChainRule] = []


def chain_rule(fn: ChainRule) -> ChainRule:
    """Register a chain diagnostic rule; a new check is a new ``@chain_rule``, not a runner edit."""
    _CHAIN_RULES.append(fn)
    return fn


def registered_chain_rules() -> list[str]:
    """Names of the registered chain rules, in registration order."""
    return [fn.__name__ for fn in _CHAIN_RULES]


@chain_rule
def _rule_end_002(rc: RuleContext) -> Iterator[Diagnostic]:
    cycles = rc.context.assumptions["chain.min_cycles"]
    chain = rc.chain
    if chain is None or chain.sustainable:
        return
    minimum = f32(cycles * chain.cycle_time)
    if chain.time_to_empty < minimum:
        yield Diagnostic(
            rule_id="P-END-002",
            severity="warning",
            message=(
                f"{rc.context.stance.value} stance: no affordable chain; the best chain empties endurance in "
                f"{chain.time_to_empty:.1f} s, under {cycles:g} cycles of {chain.cycle_time:.2f} s ({minimum:.1f} s)"
            ),
            path=rc.path,
            fix="add recovery or endurance reduction, drop toggles, or drop the costliest attacks from the chain",
            expected=f">= {cycles:g} cycles ({minimum:.1f} s)",
            actual=f"{chain.time_to_empty:.1f} s",
        )


@chain_rule
def _rule_chain_001(rc: RuleContext) -> Iterator[Diagnostic]:
    stance = rc.context.stance
    position = _DEFENSE_RANGED if stance is Stance.RANGED else _DEFENSE_MELEE
    soft_cap = float(rc.setup.breakpoints["universal"]["soft_cap_defense_pve"])
    actual = f32(rc.setup.capped.def_[position] * 100.0)
    if actual < soft_cap:
        kind = "ranged" if stance is Stance.RANGED else "melee"
        yield Diagnostic(
            rule_id="P-CHAIN-001",
            severity="info",
            message=f"{stance.value} stance: {kind} defense is {actual:.1f}%, under the {soft_cap:g}% soft cap",
            path=rc.path,
            expected=f">= {soft_cap:g}%",
            actual=f"{actual:.1f}%",
        )


@chain_rule
def _rule_chain_002(rc: RuleContext) -> Iterator[Diagnostic]:
    if rc.setup.clicks_always_on:
        yield Diagnostic(
            rule_id="P-CHAIN-002",
            severity="info",
            message="no clicks-off dump found; click buffs are counted always-on",
            path=rc.path,
            fix="dump the build with its click buffs off and pass it as --clicksoff-dir",
        )
    for attack in rc.context.attacks:
        message = category_disagreement(attack.profile)
        if message is not None:
            yield Diagnostic(rule_id="P-CHAIN-002", severity="info", message=message, path=rc.path)


@dataclass(frozen=True, slots=True)
class StanceReport:
    """One stance's evaluated model, its rotation, and its diagnostics."""

    stance: Stance
    report: DamageReport
    chain: ChainOption | None
    diagnostics: tuple[Diagnostic, ...]


def run_chain(setup: ChainSetup, stances: Sequence[Stance], *, path: str = "build") -> list[StanceReport]:
    """Evaluate the damage model for each stance and fold the registered chain rules over it."""
    results: list[StanceReport] = []
    for stance in stances:
        ctx = replace(setup.context, stance=stance)
        report = evaluate_damage(ctx)
        beside = [s for s in report.sources if s.source != SOURCE_CHAIN]
        rest = replace(
            ctx,
            timeline_available=f32(ctx.timeline_available - sum(s.timeline_share for s in beside)),
            other_drain_per_sec=f32(sum(s.end_per_sec for s in beside)),
        )
        chain = chosen_chain(rest)
        rc = RuleContext(setup=setup, context=ctx, report=report, chain=chain, path=path)
        results.append(StanceReport(stance, report, chain, tuple(d for rule in _CHAIN_RULES for d in rule(rc))))
    return results


def _finite(value: float) -> float | None:
    return None if isinf(value) else value


def stance_to_json(setup: ChainSetup, result: StanceReport) -> dict[str, Any]:
    """One stance's report as JSON-safe data (``inf`` becomes ``null``)."""
    report, chain, budget = result.report, result.chain, setup.context.budget
    chain_drain = sum(s.end_per_sec for s in report.sources if s.source == SOURCE_CHAIN)
    return {
        "stance": result.stance.value,
        "rotation": None
        if chain is None
        else {
            "attacks": list(chain.rotation),
            "cycle_time_s": chain.cycle_time,
            "gap_s": chain.gap,
            "move_time_s": chain.move_time,
            "sustainable": chain.sustainable,
        },
        "dps": {
            "total": report.total_dps,
            "layers": dict(report.scaled_dps),
            "unscaled": {s.source: s.dps for s in report.sources},
            # Per attack (chain) or per pet, before modifiers; summing a layer gives its unscaled DPS.
            "detail": {s.source: [{"name": n, "dps": v} for n, v in s.detail] for s in report.sources if s.detail},
            "labels": {s.source: s.label for s in report.sources if s.label},
        },
        "modifiers": [
            {
                "name": m.modifier,
                "multiplier": m.multiplier,
                "uptime": m.uptime,
                "factor": m.scale(setup.context.damage_buff_baseline),
                "assumption": m.assumption,
                "applies_to": None if m.applies_to is None else sorted(m.applies_to),
            }
            for m in report.modifiers
        ],
        "endurance": {
            "max": budget.max_end,
            "recovery_per_s": budget.recovery_per_sec,
            "toggle_drain_per_s": budget.toggle_drain_per_sec,
            "chain_drain_per_s": chain_drain,
            "other_drain_per_s": f32(report.total_drain_per_sec - chain_drain),
            "net_per_s": budget.net_per_sec(report.total_drain_per_sec),
            "time_to_empty_s": _finite(report.time_to_empty),
        },
        "health": dataclasses.asdict(setup.health),
        "diagnostics": [d.to_json() for d in result.diagnostics],
    }


def stance_to_text(setup: ChainSetup, result: StanceReport) -> str:
    """One stance's report as the human-readable block."""
    data = stance_to_json(setup, result)
    rot, dps, end, hp = data["rotation"], data["dps"], data["endurance"], data["health"]
    lines = [f"== {data['stance']} stance =="]
    if rot is None:
        lines.append("rotation: none (no chain attack is usable in this stance)")
    else:
        lines.append(f"rotation: {' > '.join(rot['attacks'])}")
        lines.append(
            f"  cycle {rot['cycle_time_s']:.2f} s, gap {rot['gap_s']:.2f} s, move {rot['move_time_s']:.2f} s, "
            f"{'sustainable' if rot['sustainable'] else 'not sustainable'}"
        )
    lines.append("dps (scaled; items below each layer are before modifiers):")
    for name, value in dps["layers"].items():
        label = dps["labels"].get(name)
        lines.append(f"  {name}: {value:.1f}" + (f"  [{label}]" if label else ""))
        lines += [f"    {item['name']}: {item['dps']:.1f}" for item in dps["detail"].get(name, ())]
    lines.append(f"  total: {dps['total']:.1f}")
    lines.append("modifiers:")
    lines += [
        f"  {m['name']}: x{m['factor']:.3f} ({m['assumption'] or 'no assumption'})" for m in data["modifiers"]
    ] or ["  none"]
    tte = end["time_to_empty_s"]
    lines.append(
        f"endurance: max {end['max']:.1f}, recovery {end['recovery_per_s']:.2f}/s, toggles {end['toggle_drain_per_s']:.2f}/s, "
        f"chain {end['chain_drain_per_s']:.2f}/s, other {end['other_drain_per_s']:.2f}/s, net {end['net_per_s']:+.2f}/s, "
        f"time to empty {'sustainable' if tte is None else f'{tte:.1f} s'}"
    )
    lines.append(f"health: max {hp['max_hp']:.1f}, regen {hp['regen_per_sec']:.2f}/s")
    lines += [d.format_text() for d in result.diagnostics]
    return "\n".join(lines)
