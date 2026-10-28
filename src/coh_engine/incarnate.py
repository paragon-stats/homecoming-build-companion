"""Incarnate (Alpha-slot) enhancement — the GrantPower-delivered per-power fold.

An incarnate's real enhancement is **GrantPower-delivered**: the raw
``Incarnate.Alpha.*`` power carries only ``GrantPower``/``RevokePower``/``LevelShift``/
``SetMode``, and the actual Accuracy/RechargeTime/Defense/… enhancement lives on the
hidden granted sub-powers (``Effect.Summon`` / ``nSummon`` → ``db.Power[nSummon]``). The
harness resolves those sub-powers and dumps their ``Enhancement``/``DamageBuff`` effects
plus each sub-power's accept-gate class list as ``incarnates.json`` (see
``tools/mids-dump`` ``DumpBuildIncarnates``); this module applies them per target power,
reproducing ``GBPA_ApplyIncarnateEnhancements`` (clsToonX.cs:1393) **without** porting the
GrantPower absorption subsystem.

The apply, per target power ``powerMath`` and per granted sub-power ``sub``:

- **Accept-gate** (clsToonX.cs:1458): ``powerMath.Enhancements ∩ sub.Enhancements`` must be
    non-empty. The gate is the SUB-power's class list, not the Alpha's (which is empty).
- **Scalar aspects** (clsToonX.cs:1470-1495): ``ETModifies`` of Accuracy / EnduranceDiscount
    / RechargeTime (each gated by the target's ``IgnoreEnhancement``) and InterruptTime /
    Range (always) add the effect's magnitude to that per-power scalar's fold — the pre-ED
    term where ``IgnoreED`` is false, the post-ED term where true.
- **Effect-borne aspects** (``HandleDefaultIncarnateEnh``, clsToonX.cs:1291): every other
    ``ETModifies`` (Defense, Recovery, Endurance, …) adds the magnitude to each Buffable
    target effect whose ``EffectType == ETModifies`` (and ``DamageType`` matches, for
    Damage/Defense), as the same pre-ED / post-ED split folded into that effect's Math_Mag
    multiplier.

The ED-bypass the Alpha headline describes **is** the ``IgnoreED`` split: the DB stores each
aspect twice — an ``IgnoreED == false`` effect crushed together with slotted enhancement
(pre-ED) and an ``IgnoreED == true`` effect added at full value after ED (post-ED).

This module produces two addend maps; the numeric core (``statistics``, ``base_totals``)
looks them up and stays a literal Mids transcription (check-registry.md fidelity boundary):

- ``scalar[(build_index, aspect)] = (pre_ed, post_ed)`` — the recharge/end/interrupt/acc/
    range per-power scalar fold (``pre_ed`` co-EDs with slotted enhancement, ``post_ed`` is
    the ``fold_scalar`` ``post_ed_extra`` seam).
- ``effect[(build_index, effect_index)] = (pre_ed, post_ed)`` — the effect-mag addend, whose
    ``pre_ed`` co-EDs with the effect's slotted aggregate via ``aggregate_and_ed`` and whose
    ``post_ed`` is added after ED (``base_totals._compute_enh_multipliers``).

Both terms stay raw here: the pre-ED term is summed with the slotted aggregate BEFORE the
single ``ApplyED`` the consumer runs, exactly as Mids Pass1 accumulates the incarnate pre-ED
increment into the effect's ``Math_Mag`` alongside slotted enhancement before Pass2's one
``ApplyED`` (``ED(slotted + incarnatePreED)``, not ``ED(slotted) + ED(incarnatePreED)``). So a
target effect may carry both slotted and incarnate enhancement — no separate-ED divergence,
no co-slotting guard.

Power assembly (:mod:`coh_engine.assembly`) also absorbs the granted sub-powers into the Alpha's own
effect list, as Mids does; those absorbed effects are GlobalBoost effects the totals passes skip, so the
enhancement reaches other powers only through this module.

Spec: docs/engine/mids-port-spec.md § gbpa-pass-pipeline; .claude/rules/check-registry.md.
"""

from __future__ import annotations

import json
from collections.abc import Iterable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from coh_engine.archetypes import ArchetypeDb
from coh_engine.attribmod import AttribMods
from coh_engine.effect import Effect, Power, _parse_effect, effect_mag
from coh_engine.maths import f32

# The scalar-aspect switch of GBPA_ApplyIncarnateEnhancements (clsToonX.cs:1470-1495):
# ETModifies -> (the eEnhance handler aspect name, whether the target's IgnoreEnhancement
# gate applies). Accuracy/EnduranceDiscount/RechargeTime are gated (skip when the target
# ignores that enhancement); InterruptTime/Range always apply. Every other ETModifies is
# effect-borne (HandleDefaultIncarnateEnh).
_SCALAR_ROUTES: Mapping[str, tuple[str, bool]] = {
    "Accuracy": ("Accuracy", True),
    "EnduranceDiscount": ("EnduranceDiscount", True),
    "InterruptTime": ("Interrupt", False),
    "Range": ("Range", False),
    "RechargeTime": ("RechargeTime", True),
}

# Effect-borne ETModifies that additionally match on DamageType (HandleDefaultIncarnateEnh
# Damage/Defense cases, clsToonX.cs:1309-1318); all other effect-borne aspects match on
# EffectType alone (the default case, clsToonX.cs:1355).
_DAMAGE_TYPED_ASPECTS = frozenset({"Damage", "Defense"})


@dataclass(frozen=True, slots=True)
class IncarnateSubPower:
    """One granted sub-power carrying an incarnate aspect and its accept-gate class list."""

    full_name: str
    enhancements: frozenset[int]
    effects: tuple[Effect, ...]


@dataclass(frozen=True, slots=True)
class Incarnate:
    """One StatInclude ``Incarnate.*`` build power and its granted sub-powers."""

    build_index: int
    full_name: str
    sub_powers: tuple[IncarnateSubPower, ...]


@dataclass(frozen=True, slots=True)
class IncarnateAddends:
    """The raw pre/post-ED incarnate contributions the numeric core co-EDs and folds in.

    Both buckets carry raw ``(pre_ed, post_ed)`` magnitudes. ``scalar`` is keyed by
    ``(build_index, eEnhance aspect name)`` — the per-power recharge/end/interrupt/acc/range
    fold. ``effect`` is keyed by ``(build_index, effect_index)`` — the per-effect
    Defense/Recovery/… enhancement. In both, the consumer feeds ``pre_ed`` through
    ``aggregate_and_ed(pre_ed_addend=…)`` so it is co-ED'd with the slotted aggregate in one
    ``ApplyED`` (Mids Pass1 accumulate → Pass2 ED), then adds ``post_ed`` after ED (the
    ``IgnoreED`` Pass3 term). No ED happens in this module.
    """

    scalar: Mapping[tuple[int, str], tuple[float, float]]
    effect: Mapping[tuple[int, int], tuple[float, float]]


def load_incarnates(path: Path | str) -> tuple[Incarnate, ...]:
    """Parse an ``incarnates.json`` Mids reference dump.

    Raises:
        FileNotFoundError: if ``path`` does not exist.
    """
    with open(path, encoding="utf-8") as stream:
        records = json.load(stream)
    return tuple(
        Incarnate(
            build_index=r["BuildIndex"],
            full_name=r["FullName"],
            sub_powers=tuple(
                IncarnateSubPower(
                    full_name=s["FullName"],
                    enhancements=frozenset(s["Enhancements"]),
                    effects=tuple(_parse_effect(fx) for fx in s["Effects"]),
                )
                for s in r["SubPowers"]
            ),
        )
        for r in records
    )


# A synthetic power context for the sub-power magnitude read: incarnate sub-power effects
# carry no ForcedClass and no variable scaling, so effect_mag routes GetModifier to the
# build's archetype at the fixed math level (the Melee_Ones table these effects use is
# all-ones, so BuffedMag == Scale).
def _sub_power_context(full_name: str) -> Power:
    return Power(
        build_index=-1,
        nid_power=-1,
        full_name=full_name,
        static_index=-1,
        power_type="",
        forced_class="",
        click_buff=False,
        level=0,
        end_cost=0.0,
        activate_period=0.0,
        toggle_cost=0.0,
        variable_enabled=False,
        stat_include=False,
        variable_value=0,
        effects=(),
    )


def _scalar_aspect(effect: Effect, power: Power) -> str | None:
    """The scalar handler aspect an incarnate effect folds into, or ``None`` if effect-borne.

    Returns ``None`` both for a genuinely effect-borne ``ETModifies`` and for a gated
    scalar aspect the target ignores (``IgnoreEnhancement`` false) — in the latter case
    Mids' switch falls through to the effect-mag default, which finds no matching scalar
    effect, so dropping it is faithful.
    """
    route = _SCALAR_ROUTES.get(effect.et_modifies)
    if route is None:
        return None
    aspect, gated = route
    if gated and aspect in power.ignore_enh:
        return None
    return aspect


def _matches_effect(target_fx: Effect, inc: Effect) -> bool:
    """Whether an incarnate effect-borne aspect enhances this Buffable target effect.

    ``HandleDefaultIncarnateEnh`` (clsToonX.cs:1300-1318): a ``DamageBuff`` incarnate effect
    (Cardiac resistance, Musculature damage) matches a target ``Resistance`` or ``Damage``
    effect by ``DamageType``; otherwise the target ``EffectType`` must equal the incarnate
    ``ETModifies``, with Damage/Defense additionally matching on ``DamageType`` and every
    other aspect on EffectType alone.
    """
    if not target_fx.buffable:
        return False
    if inc.effect_type == "DamageBuff":
        return target_fx.effect_type in ("Resistance", "Damage") and target_fx.damage_type == inc.damage_type
    if target_fx.effect_type != inc.et_modifies:
        return False
    if inc.et_modifies in _DAMAGE_TYPED_ASPECTS:
        return target_fx.damage_type == inc.damage_type
    return True


def _guard_supported(effect: Effect, sub_full_name: str) -> None:
    """Refuse the incarnate delivery paths this port does not yet apply.

    Raises:
        NotImplementedError: ``E20`` for a GrantPower / GlobalChanceMod sub-effect (nested
            GrantPower, Hybrid GlobalChanceMod) or a Mez aspect (the duration/magnitude split
            is unported) — each needs its own validated fixture before it is applied, so fail
            loud rather than under-report. ``DamageBuff`` (Cardiac/Destiny resistance,
            Musculature damage) is applied: resistance folds into Totals; the damage half is
            Totals-inert until the DPS port (CP6.2/#31).
    """
    if effect.effect_type not in ("Enhancement", "DamageBuff"):
        raise NotImplementedError(
            f"E20: incarnate sub-power {sub_full_name!r} carries a {effect.effect_type!r} effect; only "
            "Enhancement- and DamageBuff-delivered incarnate aspects are ported (GrantPower/GlobalChanceMod "
            "need a nested-GrantPower / Hybrid fixture first)"
        )
    if effect.et_modifies == "Mez":
        raise NotImplementedError(
            f"E20: incarnate sub-power {sub_full_name!r} enhances Mez; the magnitude/duration split of an "
            "incarnate Mez aspect is unported — add a Nerve-style fixture before applying it"
        )


def _route_effect(
    inc: Effect,
    mag: float,
    sub_full_name: str,
    power: Power,
    scalar: dict[tuple[int, str], tuple[float, float]],
    effect_pre: dict[tuple[int, int], tuple[float, float]],
) -> None:
    """Route one accepted incarnate effect (with its precomputed magnitude) into the buckets."""
    aspect = _scalar_aspect(inc, power)
    if aspect is not None:
        _add_split(scalar, (power.build_index, aspect), mag, inc.ignore_ed)
        return
    _guard_supported(inc, sub_full_name)
    for target_fx in power.effects:
        if _matches_effect(target_fx, inc):
            _add_split(effect_pre, (power.build_index, target_fx.index), mag, inc.ignore_ed)


def _add_split(bucket: dict[Any, tuple[float, float]], key: Any, mag: float, ignore_ed: bool) -> None:
    """Accumulate ``mag`` into the pre-ED (``IgnoreED`` false) or post-ED (true) slot of ``key``."""
    pre, post = bucket.get(key, (0.0, 0.0))
    if ignore_ed:
        bucket[key] = (pre, f32(post + mag))
    else:
        bucket[key] = (f32(pre + mag), post)


def resolve_incarnates(
    incarnates: Iterable[Incarnate],
    powers: Sequence[Power],
    *,
    mods: AttribMods,
    classes: ArchetypeDb,
    archetype_index: int,
) -> IncarnateAddends:
    """Fold every accepted incarnate aspect over every build power into the raw addend maps.

    Both buckets carry raw ``(pre_ed, post_ed)`` magnitudes — the consumer co-EDs the pre-ED
    term with the power's slotted aggregate (``aggregate_and_ed(pre_ed_addend=…)``) and adds
    the post-ED term after ED. No ED or co-slotting guard is needed here: the co-ED happens in
    one ``ApplyED`` downstream exactly as Mids does, so a target effect may carry both slotted
    and incarnate enhancement.

    Raises:
        NotImplementedError: ``E20`` via :func:`_guard_supported` for an unported delivery path.
    """
    scalar: dict[tuple[int, str], tuple[float, float]] = {}
    effect_pre: dict[tuple[int, int], tuple[float, float]] = {}

    for incarnate in incarnates:
        for sub in incarnate.sub_powers:
            # Magnitude is power-independent (the granted sub-power effect's base Mag), so
            # read it once per sub effect rather than per accepting target power.
            sub_power = _sub_power_context(sub.full_name)
            mags = [effect_mag(inc, sub_power, mods, classes, archetype_index) for inc in sub.effects]
            for power in powers:
                if sub.enhancements.isdisjoint(power.enhancements):
                    continue
                for inc, mag in zip(sub.effects, mags, strict=True):
                    _route_effect(inc, mag, sub.full_name, power, scalar, effect_pre)

    return IncarnateAddends(scalar=scalar, effect=effect_pre)
