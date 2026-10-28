"""Parity: the engine assembles every StatInclude power of the 12 sample builds as Mids does.

Each build is assembled from its Mids dump and compared with ``assembled_powers.json``, Mids' own assembled
effect lists, effect by effect on the fields totals read (``.claude/plans/assembly-seams.md`` § Seam 2).
"""

import json
from pathlib import Path

import pytest

from coh_engine.archetypes import load_archetypes
from coh_engine.assembly import assemble_build
from coh_engine.assembly_data import (
    load_assembled_powers,
    load_assembly_entities,
    load_assembly_powers,
    load_build_power_assembly,
)
from coh_engine.effect import load_powers_effects
from coh_engine.enhancement import load_build_slots, load_enhancement_effects
from coh_engine.expressions import ExpressionContext, conditionals_met
from coh_engine.set_bonuses import build_set_bonus_power, load_set_bonus_db

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

POWERS = load_assembly_powers(MIDS / "assembly_powers.json")
ENTITIES = load_assembly_entities(MIDS / "assembly_entities.json")
ENH_DB = dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))
SET_DB = load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json")
ARCHETYPES = load_archetypes(MIDS / "archetypes.json")


def _mismatches(build: str) -> list[str]:
    dump = MIDS / "builds" / build
    raw = load_powers_effects(dump / "powers_effects.json")
    entries = load_build_power_assembly(dump / "power_assembly.json", dump / "slots.json", raw)
    class_name = json.loads((dump / "totals.json").read_text(encoding="utf-8"))["Class"]
    slots = load_build_slots(dump / "slots.json")
    ctx = ExpressionContext(
        prev_hp_max=0.0,
        build_power_names=frozenset(p.full_name for p in raw),
        active_power_names=frozenset(p.full_name for p in raw if p.stat_include),
        variable_values={p.full_name: p.variable_value for p in raw},
    )
    bonus = build_set_bonus_power(raw, slots, ENH_DB, SET_DB, force_level=50, disable_pve=False)
    got = assemble_build(
        entries,
        slots,
        stat_include={p.build_index: p.stat_include for p in raw},
        enh_db=ENH_DB,
        set_db=SET_DB,
        powers=POWERS,
        entities=ENTITIES,
        archetype_index=ARCHETYPES.nid_from_uid_class(class_name),
        conditions=lambda fx: conditionals_met(fx, ctx),
        set_bonus_effects=bonus.effects,
    )
    # Mids has multiplied a variable power's scale by its variable value; the engine does that when it reads it.
    factor = {p.build_index: p.variable_value if p.variable_enabled else 1 for p in raw}
    out = []
    for index, want in load_assembled_powers(dump / "assembled_powers.json").items():
        name = entries[index].power.full_name
        have = got[index].effects
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
            if (h.probability > 0) != (w.probability > 0):
                out.append(f"{name} effect {w.index} probability: {h.probability}, Mids has {w.probability}")
    return out


@pytest.mark.parametrize("build", BUILDS)
def test_assembly_matches_mids(build: str) -> None:
    assert _mismatches(build) == []


def test_every_sample_build_is_compared() -> None:
    assert len(BUILDS) == 12
