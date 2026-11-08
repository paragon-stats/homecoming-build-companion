"""Goal scoring: turn "is it optimized?" into numbers against a selected profile.

Reads the engine's outputs — character ``Totals`` (CP5) and per-power recharge times
(CP6.1) — and evaluates a build against a resolved :class:`~coh_engine.profiles.BuildProfile`.
Alongside the profile targets, two adherence checks run on every score:

- **AT cap adherence** — an uncapped total above its AT cap is game-clamped (wasted,
    not illegal), so it is a ``warning`` (``P-CAP-001``), never an error.
- **Endurance sustainability** — recovery/sec (``Statistics.cs``: ``(EndRec + 1) *
    BaseRecovery * BaseMagic * (EndMax/100 + 1)``) versus toggle drain/sec
    (``Totals.EndUse``). Drain over recovery is a ``warning`` (``P-END-001``). This is
    the toggle half; the attack-chain drain is added when the chain sequencer lands.

Illegal placements and hard game-limit violations stay errors (legality.py); over-cap
and unmet goals are warnings. The exemplar-10 rule is a universal gate applied across
every profile (via :mod:`coh_engine.exemplar`), not a selectable target.

Spec: docs/build-types-and-goals.md; docs/style-guides/build-rules/hard-limits.md; error-output.md.
"""

from collections.abc import Callable, Sequence
from dataclasses import dataclass

from coh_engine.archetypes import Archetype
from coh_engine.attack_chain import build_attack_chain
from coh_engine.base_totals import TotalStatistics
from coh_engine.diagnostics import Diagnostic
from coh_engine.maths import f32
from coh_engine.profiles import BuildProfile, Target
from coh_engine.statistics import PowerStats

# eDamage ordinals (enums.json): positional Melee/Ranged/AoE and the typed resist span.
_POSITIONAL = (10, 11, 12)
_TYPED = range(1, 9)  # Smashing .. Psionic

# Self-buff durations (s) the perma check compares a power's buffed recharge against.
HASTEN_DURATION = 120.0
DOMINATION_DURATION = 90.0

# Statistics.cs:18 recovery multiplier.
_BASE_MAGIC = f32(1.666667)


@dataclass(frozen=True, slots=True)
class ScoreResult:
    """A profile score: the fraction of targets met, the per-metric verdicts, diagnostics."""

    score: float
    met: dict[str, bool]
    diagnostics: list[Diagnostic]


def defense_positional_min(totals: TotalStatistics) -> float:
    """Lowest of Melee/Ranged/AoE defense as a percentage (the positional softcap metric)."""
    return f32(min(totals.def_[i] for i in _POSITIONAL) * 100.0)


def resist_min(totals: TotalStatistics) -> float:
    """Lowest typed resistance (Smashing..Psionic) as a percentage."""
    return f32(min(totals.res[i] for i in _TYPED) * 100.0)


def _perma_recharge(power_stats: Sequence[PowerStats], suffix: str, duration: float) -> bool:
    """Whether the named power's buffed recharge is at or under ``duration`` (perma)."""
    for stats in power_stats:
        if stats.full_name.endswith(suffix):
            return stats.recharge_time <= duration
    return False


def perma_hasten(power_stats: Sequence[PowerStats]) -> bool:
    """Perma-Hasten: Hasten's buffed recharge <= its 120s duration."""
    return _perma_recharge(power_stats, ".Hasten", HASTEN_DURATION)


def perma_dom(power_stats: Sequence[PowerStats]) -> bool:
    """Perma-Domination: Domination's buffed recharge <= its 90s duration."""
    return _perma_recharge(power_stats, ".Domination", DOMINATION_DURATION)


def endurance_recovery_per_sec(totals_capped: TotalStatistics, archetype: Archetype) -> float:
    """Recovery/sec — ``Statistics.cs`` ``EnduranceRecoveryNumeric`` over the capped totals."""
    factor = f32((totals_capped.end_rec + 1.0) * f32(archetype.base_recovery * _BASE_MAGIC))
    return f32(factor * f32(totals_capped.end_max / 100.0 + 1.0))


def endurance_diagnostic(
    totals: TotalStatistics, totals_capped: TotalStatistics, archetype: Archetype, chain_drain: float = 0.0
) -> Diagnostic | None:
    """Warn (``P-END-001``) when total drain/sec (toggles + attack chain) exceeds recovery/sec.

    ``Totals.EndUse`` is the running toggle drain (CP5); ``chain_drain`` is the attack
    chain's endurance/sec (:func:`~coh_engine.attack_chain.build_attack_chain`, CP7.1).
    Their sum is the full sustained drain a running build pays. Drain over recovery is a
    ``warning`` — the game does not fail a build on endurance.
    """
    recovery = endurance_recovery_per_sec(totals_capped, archetype)
    drain = f32(totals.end_use + chain_drain)
    if drain <= recovery:
        return None
    detail = f"toggles {totals.end_use:.3f} + chain {chain_drain:.3f}" if chain_drain > 0.0 else "toggles"
    return Diagnostic(
        rule_id="P-END-001",
        severity="warning",
        message=(
            f"endurance drain {drain:.3f}/s ({detail}) exceeds recovery {recovery:.3f}/s; "
            "the build is not endurance-sustainable"
        ),
        fix="add +Recovery/+MaxEnd, reduce toggle EndRdx cost, drop a toggle, or lengthen the attack chain",
        expected=round(recovery, 3),
        actual=round(drain, 3),
    )


def _cap_overshoots(totals: TotalStatistics, totals_capped: TotalStatistics) -> list[tuple[str, float, float]]:
    """(label, uncapped, capped) for each field the engine clamped — ``Totals`` above ``TotalsCapped``.

    Diffs the engine's own capped output instead of re-deriving cap thresholds, so the
    check can never drift from ``_gbd_apply_caps`` (check-registry.md: assert against
    engine output, do not re-derive).
    """
    out: list[tuple[str, float, float]] = []
    for i in _TYPED:
        if totals.res[i] > totals_capped.res[i]:
            out.append((f"Res[{i}]", totals.res[i], totals_capped.res[i]))
    for label, uncapped, capped in (
        ("BuffHaste", totals.buff_haste, totals_capped.buff_haste),
        ("BuffDam", totals.buff_dam, totals_capped.buff_dam),
        ("HPRegen", totals.hp_regen, totals_capped.hp_regen),
        ("EndRec", totals.end_rec, totals_capped.end_rec),
    ):
        if uncapped > capped:
            out.append((label, uncapped, capped))
    return out


def cap_adherence_diagnostics(totals: TotalStatistics, totals_capped: TotalStatistics) -> list[Diagnostic]:
    """Warn (``P-CAP-001``) for each field the engine clamped (uncapped > capped, so wasted)."""
    return [
        Diagnostic(
            rule_id="P-CAP-001",
            severity="warning",
            message=f"{label} {uncapped:.3f} exceeds the AT cap {capped:.3f}; the excess is clamped and wasted",
            fix="re-budget the over-cap slotting toward an unmet stat",
            expected=round(capped, 3),
            actual=round(uncapped, 3),
        )
        for label, uncapped, capped in _cap_overshoots(totals, totals_capped)
    ]


# The metric registry: a scoring metric is a name -> (Totals, power stats) -> value. A new
# metric is a new @metric(name); build_profiles.json references the name (check-registry.md).
Metric = Callable[[TotalStatistics, Sequence[PowerStats]], float | bool]
_METRICS: dict[str, Metric] = {}


def metric(name: str) -> Callable[[Metric], Metric]:
    """Register a scoring metric under the name a profile target references."""

    def register(fn: Metric) -> Metric:
        _METRICS[name] = fn
        return fn

    return register


def registered_metrics() -> list[str]:
    """The scoring metric names the registry can evaluate — the introspectable registry."""
    return sorted(_METRICS)


@metric("defense_positional_min")
def _metric_defense_positional(totals: TotalStatistics, power_stats: Sequence[PowerStats]) -> float | bool:
    return defense_positional_min(totals)


@metric("resist_min")
def _metric_resist_min(totals: TotalStatistics, power_stats: Sequence[PowerStats]) -> float | bool:
    return resist_min(totals)


@metric("perma_hasten")
def _metric_perma_hasten(totals: TotalStatistics, power_stats: Sequence[PowerStats]) -> float | bool:
    return perma_hasten(power_stats)


@metric("perma_dom")
def _metric_perma_dom(totals: TotalStatistics, power_stats: Sequence[PowerStats]) -> float | bool:
    return perma_dom(power_stats)


def _metric_value(name: str, totals: TotalStatistics, power_stats: Sequence[PowerStats]) -> float | bool:
    """Resolve a target's metric name to the build's computed value via the registry."""
    fn = _METRICS.get(name)
    if fn is None:
        raise ValueError(
            f"E17: unknown scoring metric {name!r}; register it with @metric({name!r}) before using it in a "
            f"profile (known metrics: {sorted(_METRICS)})"
        )
    return fn(totals, power_stats)


def _meets(actual: float | bool, target: Target) -> bool:
    """Whether ``actual`` satisfies the target's comparison op."""
    if target.op == "==":
        return actual == target.value
    if target.op == ">=":
        return actual >= target.value
    if target.op == "<=":
        return actual <= target.value
    raise ValueError(
        f"E18: unknown comparison op {target.op!r} in a profile target; use one of '==', '>=', '<=' in "
        "build_profiles.json (or add the op to _meets before using it)"
    )


def score_build(
    profile: BuildProfile,
    totals: TotalStatistics,
    totals_capped: TotalStatistics,
    power_stats: Sequence[PowerStats],
    archetype: Archetype,
) -> ScoreResult:
    """Score a build against ``profile`` and collect the adherence warnings.

    ``score`` is the fraction of the profile's targets met (1.0 when it has none). Each
    unmet target adds a ``P-GOAL-002`` warning; over-cap totals add ``P-CAP-001``; an
    endurance shortfall adds ``P-END-001``. All are warnings — the build stays valid.
    """
    met: dict[str, bool] = {}
    met_count = 0
    diagnostics: list[Diagnostic] = []
    for target in profile.targets:
        actual = _metric_value(target.metric, totals, power_stats)
        ok = _meets(actual, target)
        met[target.metric] = ok
        met_count += int(ok)
        if not ok:
            diagnostics.append(
                Diagnostic(
                    rule_id="P-GOAL-002",
                    severity="warning",
                    message=(
                        f"{profile.display_name}: {target.metric} = {actual} does not meet {target.op} {target.value}"
                    ),
                    fix=f"raise {target.metric} to satisfy the {profile.display_name} target",
                    expected=target.value,
                    actual=actual,
                )
            )
    # Count met TARGETS, not met dict entries: two targets on one metric collide in
    # `met` (last wins), but each still counts toward the score independently.
    score = 1.0 if not profile.targets else f32(met_count / len(profile.targets))
    diagnostics.extend(cap_adherence_diagnostics(totals, totals_capped))
    chain = build_attack_chain(power_stats)
    end_diag = endurance_diagnostic(totals, totals_capped, archetype, chain.end_per_sec)
    if end_diag is not None:
        diagnostics.append(end_diag)
    return ScoreResult(score=score, met=met, diagnostics=diagnostics)
