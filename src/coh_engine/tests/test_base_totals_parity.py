"""Mids-parity goldens: base per-attribute totals, no enhancements.

Each fixture under ``fixtures/builds/`` is an empty-slot build (zero
enhancements, clicks toggled off). The Mids dump harness loaded it, ran
``GenerateBuffedPowerArray()``, and dumped ``Totals``/``TotalsCapped`` plus the
build's resolved power/effect records. The Python port must reproduce every
field of both structures at IEEE-754 single precision from the same records.

Re-baseline (re-run ``tools/mids-dump`` with the builds dir) after any
Homecoming DB update.
"""

import json
from pathlib import Path
from typing import Any

import pytest

from coh_engine import base_totals
from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.attribmod import AttribMods, load_attribmod
from coh_engine.base_totals import (
    EngineConfig,
    EnumMaps,
    ServerData,
    compute_base_totals,
    load_engine_config,
    load_enum_maps,
    load_server_data,
)
from coh_engine.buildfile.mbd import load_mbd
from coh_engine.effect import load_powers_effects
from coh_engine.endurance_budget import MIDS_TOTALS_SCALAR_FIELDS, MIDS_TOTALS_VECTOR_FIELDS
from coh_engine.maths import f32

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES / "mids"
BUILDS = ["shield_scrapper_noslots", "arsenal_dominator_noslots"]

VECTOR_FIELDS = MIDS_TOTALS_VECTOR_FIELDS
SCALAR_FIELDS = MIDS_TOTALS_SCALAR_FIELDS


@pytest.fixture(scope="module")
def mods() -> AttribMods:
    return load_attribmod(FIXTURES / "attribmod" / "AttribMod.json")


@pytest.fixture(scope="module")
def classes() -> ArchetypeDb:
    return load_archetypes(MIDS / "archetypes.json")


@pytest.fixture(scope="module")
def enums() -> EnumMaps:
    return load_enum_maps(MIDS / "enums.json")


@pytest.fixture(scope="module")
def config() -> EngineConfig:
    return load_engine_config(MIDS / "config.json")


@pytest.fixture(scope="module")
def server() -> ServerData:
    return load_server_data(MIDS / "server_data.json")


def _expected(name: str) -> dict[str, Any]:
    with open(MIDS / "builds" / name / "totals.json", encoding="utf-8") as fh:
        data: dict[str, Any] = json.load(fh)
    return data


@pytest.mark.parametrize("name", BUILDS)
def test_base_totals_match_mids(
    name: str,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
) -> None:
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    expected = _expected(name)
    result = compute_base_totals(
        powers,
        class_name=expected["Class"],
        mods=mods,
        classes=classes,
        enums=enums,
        config=config,
        server=server,
    )
    for side, got in (("Totals", result.totals), ("TotalsCapped", result.totals_capped)):
        want = expected[side]
        for json_key, attr in VECTOR_FIELDS.items():
            got_vec = getattr(got, attr)
            assert len(got_vec) == len(want[json_key]), f"{name}.{side}.{json_key} length"
            for i, cell in enumerate(want[json_key]):
                assert f32(cell) == got_vec[i], f"{name}.{side}.{json_key}[{i}]"
        for json_key, attr in SCALAR_FIELDS.items():
            assert f32(want[json_key]) == getattr(got, attr), f"{name}.{side}.{json_key}"


@pytest.mark.parametrize("name", BUILDS)
def test_dump_stat_include_matches_mbd_fixture(name: str) -> None:
    """The harness dump and the .mbd fixture agree on which powers are active."""
    build = load_mbd(FIXTURES / "builds" / f"{name}.mbd")
    mbd_on = {p.power_name for p in build.power_entries if p.stat_include and p.power_name}
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    dump_on = {p.full_name for p in powers if p.stat_include}
    # Mids injects the set-bonus virtual power on load; the authored Dominator
    # fixture doesn't carry it, the derived Scrapper fixture does.
    dump_on.discard("Inherent.Inherent.Special_Set_Bonuses")
    mbd_on.discard("Inherent.Inherent.Special_Set_Bonuses")
    assert dump_on == mbd_on


@pytest.mark.parametrize("name", BUILDS)
def test_fixture_builds_have_no_enhancements(name: str) -> None:
    """Scope guard: the parity fixtures are genuinely empty-slot builds."""
    build = load_mbd(FIXTURES / "builds" / f"{name}.mbd")
    slotted = [
        (p.power_name, s.enhancement.uid)
        for p in build.power_entries
        for s in p.slot_entries
        if s is not None and s.enhancement is not None
    ]
    assert slotted == []


def _excluded_effect_count(name: str) -> int:
    """Effects on stat-included powers that _can_include drops."""
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    return sum(
        1
        for p in powers
        if p.stat_include
        for fx in p.effects
        if fx.active_conditionals_count > 0 or fx.special_case != "None"
    )


def test_hazard_gate_is_non_vacuous() -> None:
    """At least one fixture must carry excluded conditional effects, or the gate
    below would prove nothing. The Shield Scrapper's Critical Hit inherent has
    nine conditional GlobalChanceMod effects."""
    assert _excluded_effect_count("shield_scrapper_noslots") > 0


@pytest.mark.parametrize("name", BUILDS)
def test_excluded_conditionals_do_not_affect_totals(
    name: str,
    mods: AttribMods,
    classes: ArchetypeDb,
    enums: EnumMaps,
    config: EngineConfig,
    server: ServerData,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """The hazard gate: forcing every ActiveConditionals/SpecialCase effect
    back into the sum (bypassing _can_include) must not change any Totals field.

    This is the check the module docstring cites. It proves the CanInclude
    simplification — which drops those effects rather than evaluating them —
    changes no reported total for the committed fixtures, so the known
    divergence from Mids' CanInclude does not affect these builds.
    """
    powers = load_powers_effects(MIDS / "builds" / name / "powers_effects.json")
    class_name = _expected(name)["Class"]
    call = {
        "class_name": class_name,
        "mods": mods,
        "classes": classes,
        "enums": enums,
        "config": config,
        "server": server,
    }
    excluded = compute_base_totals(powers, **call)
    monkeypatch.setattr(base_totals, "_can_include", lambda fx, ctx: True)
    forced = compute_base_totals(powers, **call)
    assert forced.totals == excluded.totals
    assert forced.totals_capped == excluded.totals_capped
