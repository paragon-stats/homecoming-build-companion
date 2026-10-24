"""Endurance budget: recovery/drain/pool from Mids totals, and the totals.json loader."""

import json
from dataclasses import fields, replace
from math import inf
from pathlib import Path
from typing import Any

import pytest

from coh_engine.archetypes import Archetype, load_archetypes
from coh_engine.base_totals import TotalStatistics
from coh_engine.endurance_budget import (
    MIDS_TOTALS_SCALAR_FIELDS,
    MIDS_TOTALS_VECTOR_FIELDS,
    build_endurance_budget,
    build_health_budget,
    load_canonical_caps,
    load_mids_totals,
)
from coh_engine.maths import f32
from coh_engine.scoring import endurance_recovery_per_sec

CANONICAL_ARCHETYPES = Path(__file__).parents[3] / "data" / "canonical" / "archetypes"
MIDS = Path(__file__).parent / "fixtures" / "mids"
TOTALS_JSON = MIDS / "builds" / "shield_scrapper_set_bonuses" / "totals.json"


@pytest.fixture(scope="module")
def scrapper() -> Archetype:
    classes = load_archetypes(MIDS / "archetypes.json")
    return classes.classes[classes.nid_from_uid_class("Class_Scrapper")]


def _raw() -> dict[str, Any]:
    with open(TOTALS_JSON, encoding="utf-8-sig") as fh:
        data: dict[str, Any] = json.load(fh)
    return data


def test_loader_round_trips_endurance_fields() -> None:
    raw = _raw()
    totals, capped = load_mids_totals(TOTALS_JSON)
    assert totals.end_rec == f32(raw["Totals"]["EndRec"])
    assert totals.end_use == f32(raw["Totals"]["EndUse"])
    assert totals.end_max == f32(raw["Totals"]["EndMax"])
    assert capped.end_rec == f32(raw["TotalsCapped"]["EndRec"])
    assert totals.def_[11] == f32(raw["Totals"]["Def"][11])
    assert len(totals.res) == len(raw["Totals"]["Res"])


def test_loader_populates_every_totals_field() -> None:
    """Every TotalStatistics field is mapped from the JSON, so none is silently left at default."""
    totals, _ = load_mids_totals(TOTALS_JSON)
    raw = _raw()["Totals"]
    for key, attr in MIDS_TOTALS_SCALAR_FIELDS.items():
        assert getattr(totals, attr) == f32(raw[key]), key
    for key, attr in MIDS_TOTALS_VECTOR_FIELDS.items():
        assert getattr(totals, attr) == [f32(v) for v in raw[key]], key
    assert len(MIDS_TOTALS_VECTOR_FIELDS) + len(MIDS_TOTALS_SCALAR_FIELDS) == len(fields(TotalStatistics))


@pytest.mark.parametrize("content", ["", '{"Totals": {', "not json"])
def test_corrupt_totals_file_is_e33(tmp_path: Path, content: str) -> None:
    path = tmp_path / "totals.json"
    path.write_text(content, encoding="utf-8")
    with pytest.raises(ValueError, match=r"E33: .*totals\.json is not valid JSON"):
        load_mids_totals(path)


def test_corrupt_archetype_file_is_e33(tmp_path: Path) -> None:
    (tmp_path / "tanker.json").write_text("", encoding="utf-8")
    with pytest.raises(ValueError, match=r"E33: .*tanker\.json is not valid JSON"):
        load_canonical_caps(tmp_path, "Class_Tanker")


def test_loader_accepts_str_path() -> None:
    totals, _ = load_mids_totals(str(TOTALS_JSON))
    assert totals.end_use == f32(_raw()["Totals"]["EndUse"])


def test_loader_missing_key_raises_e31(tmp_path: Path) -> None:
    raw = _raw()
    del raw["TotalsCapped"]["EndMax"]
    path = tmp_path / "totals.json"
    path.write_text(json.dumps(raw), encoding="utf-8")
    with pytest.raises(ValueError, match=r"E31.*TotalsCapped.*EndMax"):
        load_mids_totals(path)


def test_loader_missing_section_raises_e31(tmp_path: Path) -> None:
    path = tmp_path / "totals.json"
    path.write_text("{}", encoding="utf-8")
    with pytest.raises(ValueError, match=r"E31.*Totals"):
        load_mids_totals(path)


def test_fixture_build_is_sustainable(scrapper: Archetype) -> None:
    totals, capped = load_mids_totals(TOTALS_JSON)
    budget = build_endurance_budget(totals, capped, scrapper)
    assert budget.recovery_per_sec == endurance_recovery_per_sec(capped, scrapper)
    assert budget.toggle_drain_per_sec == totals.end_use
    assert budget.max_end == 100.0  # EndMax is 0 in this fixture
    assert budget.time_to_empty() == inf


def test_max_end_is_100_plus_end_max(scrapper: Archetype) -> None:
    """Statistics.cs:31 ``EnduranceMaxEnd => Totals.EndMax + 100f`` (uncapped totals)."""
    totals, capped = load_mids_totals(TOTALS_JSON)
    budget = build_endurance_budget(replace(totals, end_max=25.0), replace(capped, end_max=0.0), scrapper)
    assert budget.max_end == 125.0


def test_draining_case_has_exact_time_to_empty(scrapper: Archetype) -> None:
    totals, capped = load_mids_totals(TOTALS_JSON)
    recovery = endurance_recovery_per_sec(capped, scrapper)
    drain = f32(recovery + 2.0)
    budget = build_endurance_budget(replace(totals, end_use=drain), capped, scrapper)
    net = f32(recovery - drain)
    assert budget.time_to_empty() == f32(100.0 / -net)


def test_canonical_caps_are_read_per_archetype() -> None:
    """``attrib_max_max`` at level 50: 365 endurance for every AT; HP cap per AT."""
    end_cap, hp_cap = load_canonical_caps(CANONICAL_ARCHETYPES, "Class_Arachnos_Soldier")
    assert end_cap == f32(365.0)
    assert hp_cap == f32(2409.5176)
    assert load_canonical_caps(CANONICAL_ARCHETYPES, "Class_Tanker")[1] == f32(3533.958984375)


def test_unknown_class_has_no_canonical_caps() -> None:
    with pytest.raises(ValueError, match="E32"):
        load_canonical_caps(CANONICAL_ARCHETYPES, "Class_Nobody")


def test_max_end_cap_clamps_the_pool_and_the_recovery_scaling(scrapper: Archetype) -> None:
    """Recovery scales with max endurance, so the clamp applies to both the pool and the income."""
    totals, capped = load_mids_totals(TOTALS_JSON)
    over = build_endurance_budget(
        replace(totals, end_max=300.0), replace(capped, end_max=300.0), scrapper, max_end_cap=365.0
    )
    assert over.max_end == f32(365.0)
    assert over.recovery_per_sec == endurance_recovery_per_sec(replace(capped, end_max=265.0), scrapper)
    under = build_endurance_budget(
        replace(totals, end_max=20.0), replace(capped, end_max=20.0), scrapper, max_end_cap=365.0
    )
    assert under.max_end == f32(120.0)
    assert under.recovery_per_sec == endurance_recovery_per_sec(replace(capped, end_max=20.0), scrapper)


def test_hp_regen_scales_with_capped_max_hp(scrapper: Archetype) -> None:
    """Statistics.cs:51: (HPRegen + 1) * BaseRegen * 1.6666666 * HPMax(capped) / 100."""
    _, capped = load_mids_totals(TOTALS_JSON)
    health = build_health_budget(capped, scrapper)
    expected = f32(f32(f32(capped.hp_regen + 1.0) * f32(scrapper.base_regen * 1.6666666)) * capped.hp_max / 100.0)
    assert health.max_hp == capped.hp_max
    assert health.regen_per_sec == expected
    bigger = build_health_budget(replace(capped, hp_max=f32(capped.hp_max * 2.0)), scrapper)
    assert bigger.regen_per_sec == pytest.approx(health.regen_per_sec * 2.0, rel=1e-6)
