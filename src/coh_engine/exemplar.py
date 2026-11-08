"""Exemplar availability (+5 rule) and ForceLevel-gated recompute.

Two distinct concerns the checkpoint keeps separate:

- **Availability** — real-CoH's +5 rule: exemplared to level N, a character has
    every power (and slot) taken at level ``<= N + 5``. This is a build-authoring
    predicate; it does not enter the totals math.
- **ForceLevel gating** — how Mids recomputes totals at an exemplar level: set
    bonuses are gated on power pick ``Level <= ForceLevel`` and slot values on
    ``Slots.Level < ForceLevel``, with **no** ``set min - 3`` retention. The port
    already threads ``force_level`` through ``EngineConfig``; :func:`with_force_level`
    lowers it for a recompute. Mids retains a power's bonuses purely by pick level,
    so its exemplar totals are optimistic at deep exemplar versus live CoH — the
    port matches Mids (its reference), not live CoH.

Spec: docs/engine/mids-port-spec.md § set-bonuses-ruleof5 (the no--3 resolution);
docs/style-guides/build-rules/exemplar-10.md (the +5 availability rule).
"""

from collections.abc import Sequence
from dataclasses import replace

from coh_engine.base_totals import EngineConfig
from coh_engine.effect import Power

# Real-CoH exemplar window: powers/slots taken at level <= exemplar + 5 remain
# available (Effect availability, docs/style-guides/build-rules/exemplar-10.md § Mechanics summary).
AVAILABILITY_OFFSET = 5


def is_power_available(pick_level: int, exemplar_level: int) -> bool:
    """Whether a power picked at ``pick_level`` is available at ``exemplar_level`` (the +5 rule)."""
    return pick_level <= exemplar_level + AVAILABILITY_OFFSET


def is_slot_available(slot_level: int, exemplar_level: int) -> bool:
    """Whether a slot placed at ``slot_level`` is available at ``exemplar_level`` (same +5 window)."""
    return slot_level <= exemplar_level + AVAILABILITY_OFFSET


def available_powers(powers: Sequence[Power], exemplar_level: int) -> list[Power]:
    """The subset of ``powers`` available at ``exemplar_level`` by the +5 rule.

    Availability follows the build's PICK level (``Power.pick_level``) — when the
    character took the power — in the same convention as the ForceLevel gate, not the
    DB minimum ``level`` (which only says the earliest a power *could* be taken).
    """
    return [power for power in powers if is_power_available(power.pick_level, exemplar_level)]


def with_force_level(config: EngineConfig, force_level: int) -> EngineConfig:
    """A copy of ``config`` with ``force_level`` replaced — the exemplar recompute config.

    Passed to :func:`~coh_engine.base_totals.compute_base_totals`; the lowered
    ForceLevel gates set bonuses (power pick ``Level <= ForceLevel``) and slot values
    (``Slots.Level < ForceLevel``) exactly as Mids does at that exemplar level.
    """
    return replace(config, force_level=force_level)
