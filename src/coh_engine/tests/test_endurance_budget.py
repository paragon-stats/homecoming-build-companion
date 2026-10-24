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
    load_mids_totals,
)
from coh_engine.maths import f32
from coh_engine.scoring import endurance_recovery_per_sec

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
    for key, attr in (*MIDS_TOTALS_VECTOR_FIELDS.items(), *MIDS_TOTALS_SCALAR_FIELDS.items()):
        assert key in raw
        assert hasattr(totals, attr)
    assert len(MIDS_TOTALS_VECTOR_FIELDS) + len(MIDS_TOTALS_SCALAR_FIELDS) == len(fields(TotalStatistics))


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
