"""Per-aspect enhancement handlers — the ``eEnhance``-keyed registry for the scalar fold.

``GBPA_Pass3_EnhancePostED`` folds a per-power *scalar* aspect (recharge / endurance
discount / interrupt / accuracy / range) as ``base / (( ED(Σ slotted) + global ) + 1)``,
with recharge additionally clamped to the AT recharge cap. Every aspect follows that one
shape; only three things vary per aspect — its ED schedule (already keyed on the aspect
name in :func:`~coh_engine.enh_pipeline.schedule_for_enhance`), which global
``_selfEnhance`` term it adds (a field on :class:`~coh_engine.base_totals.GlobalEnhance`),
and whether it is capped.

This module centralises those facts as a registry keyed on the ``eEnhance`` aspect, so the
fold has a single registry-driven path instead of a hardcoded arm per aspect. The two
consumers — the per-power derived stats (:mod:`coh_engine.statistics`) and the character
toggle-EndUse arm (:mod:`coh_engine.base_totals`) — each already produce the ED-reduced
``Σ slotted`` aggregate their own way (inline vs. precomputed); this handler takes that
aggregate and supplies the per-aspect global term and cap. A new aspect is a new
``@_register`` entry (check-registry.md); the incarnate subsystem (CP5.1) plugs its
per-power enhancement into the same fold via the ``post_ed_extra`` seam (and a matching
pre-ED addend where the aggregate is produced), rather than special-casing each aspect.

Fidelity boundary: this is the *scalar* fold dispatch only. The effect-magnitude
enhancement path (``base_totals._compute_enh_multipliers`` / ``_enhance_aspect``), where
damage and typed defense/resistance are enhanced, stays a literal Mids transcription and
is **not** registry-ised.

Spec: docs/engine/mids-port-spec.md § gbpa-pass-pipeline (Pass 3);
docs/style-guides/engine-architecture.md.
"""

from __future__ import annotations

from collections.abc import Collection
from dataclasses import dataclass
from typing import TYPE_CHECKING

from coh_engine.pass3 import fold_divisor

if TYPE_CHECKING:
    # Type-only import: enh_aspects reads GlobalEnhance's fields via getattr at runtime,
    # so base_totals (which imports this module for the toggle fold) is not imported back
    # at runtime — this breaks what would otherwise be an import cycle.
    from coh_engine.base_totals import GlobalEnhance

# eEnhance aspect names (the scalar aspects the per-power fold divides by).
ASPECT_RECHARGE = "RechargeTime"
ASPECT_END = "EnduranceDiscount"
ASPECT_INTERRUPT = "Interrupt"
ASPECT_ACCURACY = "Accuracy"
ASPECT_RANGE = "Range"


@dataclass(frozen=True, slots=True)
class AspectHandler:
    """How one ``eEnhance`` scalar aspect folds: its global term and whether it is capped.

    ``global_field`` names the :class:`~coh_engine.base_totals.GlobalEnhance` attribute
    holding this aspect's global ``_selfEnhance`` term (``recharge``/``end_discount``/…).
    ``capped`` is true only for recharge, whose fold multiplier is clamped to the AT
    recharge cap (``GBPA_ApplyArchetypeCaps``).
    """

    aspect: str
    global_field: str
    capped: bool


_HANDLERS: dict[str, AspectHandler] = {}


def _register(handler: AspectHandler) -> AspectHandler:
    _HANDLERS[handler.aspect] = handler
    return handler


# The five scalar aspects. Recharge/end/interrupt are folded today (CP6.1 derived stats +
# CP5 toggle EndUse); accuracy/range are registered against the reserved GlobalEnhance
# fields and consumed once the DPS (CP6.2) and range folds land.
RECHARGE = _register(AspectHandler(ASPECT_RECHARGE, "recharge", capped=True))
END = _register(AspectHandler(ASPECT_END, "end_discount", capped=False))
INTERRUPT = _register(AspectHandler(ASPECT_INTERRUPT, "interrupt", capped=False))
ACCURACY = _register(AspectHandler(ASPECT_ACCURACY, "accuracy", capped=False))
RANGE = _register(AspectHandler(ASPECT_RANGE, "range", capped=False))


def registered_aspect_handlers() -> list[str]:
    """The registered scalar-aspect names — the introspectable registry."""
    return sorted(_HANDLERS)


def global_term(handler: AspectHandler, global_enhance: GlobalEnhance) -> float:
    """The global ``_selfEnhance`` term this aspect adds (from :class:`GlobalEnhance`)."""
    return float(getattr(global_enhance, handler.global_field))


def cap_for(handler: AspectHandler, recharge_cap: float) -> float | None:
    """The fold cap for this aspect — the AT recharge cap for recharge, otherwise none."""
    return recharge_cap if handler.capped else None


def fold_scalar(
    handler: AspectHandler,
    ed_aggregate: float,
    global_scalar: float,
    *,
    ignore_enh: Collection[str],
    recharge_cap: float,
    post_ed_extra: float = 0.0,
) -> float:
    """The Pass 3/4 fold multiplier for one scalar aspect, over an already-ED aggregate.

    ``ed_aggregate`` is ``ED(Σ slotted <aspect>)`` (produced by the caller, whose slot set
    differs per consumer). ``global_scalar`` is the aspect's global ``_selfEnhance`` term
    (``global_term`` resolves it from a :class:`GlobalEnhance`, or the caller passes the raw
    value). ``post_ed_extra`` is the post-ED extra source — the incarnate ``IgnoreED`` = true
    term (CP5.1) — defaulting to 0. The global and post-ED terms are passed **separately** to
    :func:`~coh_engine.pass3.fold_divisor`, which adds them sequentially
    (``((agg + global) + post) + 1``) to match Mids' three-term float order — pre-summing
    ``global + post`` would re-associate the float32 add and diverge by up to 1 ULP.
    """
    return fold_divisor(
        ed_aggregate,
        global_scalar,
        aspect=handler.aspect,
        ignored_aspects=ignore_enh,
        cap=cap_for(handler, recharge_cap),
        post_ed_extra=post_ed_extra,
    )
