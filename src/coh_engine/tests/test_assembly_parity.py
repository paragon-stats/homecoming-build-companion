"""Parity: the engine assembles every StatInclude power of the 12 sample builds as Mids does.

Each build is assembled from its Mids dump and compared with ``assembled_powers.json``, Mids' own assembled
effect lists, effect by effect on the fields totals read (``.claude/plans/assembly-seams.md`` § Seam 2).
"""

import json
from pathlib import Path

import pytest

from coh_engine.archetypes import load_archetypes
from coh_engine.assembly import Conditions, assemble_build
from coh_engine.assembly_data import (
    load_assembled_powers,
    load_assembly_entities,
    load_assembly_powers,
    load_build_power_assembly,
)
from coh_engine.effect import Effect, load_powers_effects
from coh_engine.enhancement import load_build_slots, load_enhancement_effects
from coh_engine.set_bonuses import load_set_bonus_db

MIDS = Path(__file__).parent / "fixtures" / "mids"
BUILDS = sorted(p.parent.name for p in (MIDS / "builds").glob("*/assembled_powers.json"))
FIELDS = (
    "effect_type",
    "et_modifies",
    "damage_type",
    "mez_type",
    "to_who",
    "effect_class",
    "buffable",
    "absorbed_effect",
    "absorbed_power_type",
    "absorbed_class_n_id",
    "is_enhancement_effect",
    "scale",
    "n_magnitude",
    "aspect",
    "modifier_table",
    "delayed_time",
    "pv_mode",
    "ignore_scaling",
    "conditionals",
)
# A slotted Impervious Skin raises this id's chance through the set-bonus virtual power, which assembly is
# not given; its probability is left to the totals step.
_SET_BONUS_CHANCE_IDS = frozenset({"ImperviousSkinRegen"})
# Decoy carries a Soulbound Allegiance proc, which takes the pets-only branch the export has no entity for.
_PETS_ONLY = "Laser - ALT+F4 - Controller (Illusion Control - Time Manipulation)"

POWERS = load_assembly_powers(MIDS / "assembly_powers.json")
ENTITIES = load_assembly_entities(MIDS / "assembly_entities.json")
ENH_DB = dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))
SET_DB = load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json")
ARCHETYPES = load_archetypes(MIDS / "archetypes.json")


def _active_condition(active: frozenset[str]) -> Conditions:
    """A stand-in for seam 3's condition check: the ``Active`` conditions the sample grants carry, AND-ed."""

    def met(fx: Effect) -> bool:
        results = []
        for key, value in fx.conditionals:
            kind, _, name = key.partition(":")
            if kind != "Active":
                raise AssertionError(f"condition {key} on effect {fx.index} needs the full condition check")
            results.append((name in active) == (value == "True"))
        return all(results)

    return met


def _mismatches(build: str) -> list[str]:
    dump = MIDS / "builds" / build
    raw = load_powers_effects(dump / "powers_effects.json")
    entries = load_build_power_assembly(dump / "power_assembly.json", dump / "slots.json", raw)
    class_name = json.loads((dump / "totals.json").read_text(encoding="utf-8"))["Class"]
    got = assemble_build(
        entries,
        load_build_slots(dump / "slots.json"),
        stat_include={p.build_index: p.stat_include for p in raw},
        enh_db=ENH_DB,
        set_db=SET_DB,
        powers=POWERS,
        entities=ENTITIES,
        archetype_index=ARCHETYPES.nid_from_uid_class(class_name),
        conditions=_active_condition(frozenset(p.full_name for p in raw if p.stat_include)),
    )
    # Mids has multiplied a variable power's scale by its variable value; the engine does that when it reads it.
    factor = {p.build_index: p.variable_value if p.variable_enabled else 1 for p in raw}
    out = []
    for index, want in load_assembled_powers(dump / "assembled_powers.json").items():
        name = entries[index].power.full_name
        have = got[index]
        if len(have) != len(want):
            out.append(f"{name}: {len(have)} effects, Mids has {len(want)}")
            continue
        for h, w in zip(have, want, strict=True):
            for field in FIELDS:
                value = getattr(h, field)
                if field == "scale" and h.variable_modified and not h.ignore_scaling:
                    value *= factor[index]
                if value != getattr(w, field):
                    out.append(f"{name} effect {w.index} {field}: {value!r}, Mids has {getattr(w, field)!r}")
            if (h.probability > 0) != (w.probability > 0) and w.effect_id not in _SET_BONUS_CHANCE_IDS:
                out.append(f"{name} effect {w.index} probability: {h.probability}, Mids has {w.probability}")
    return out


@pytest.mark.parametrize("build", [b for b in BUILDS if b != _PETS_ONLY])
def test_assembly_matches_mids(build: str) -> None:
    assert _mismatches(build) == []


def test_pets_only_enhancement_refuses() -> None:
    with pytest.raises(NotImplementedError, match="E89"):
        _mismatches(_PETS_ONLY)


def test_every_sample_build_is_compared() -> None:
    assert len(BUILDS) == 12
