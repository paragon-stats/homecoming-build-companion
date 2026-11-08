"""Tests for the StaticIndex resolution maps used by the .mxd reader."""

from pathlib import Path

import pytest

from coh_engine.buildfile.dbindex import (
    EnhIndexEntry,
    load_enhancement_index,
    load_power_index,
)

FIXTURES = Path(__file__).parents[3] / "tests" / "fixtures" / "mids"


def test_power_index_resolves_static_index_to_fullname() -> None:
    idx = load_power_index(FIXTURES / "power_static_index.json")
    # Every entry maps a StaticIndex int to a FullName string.
    assert idx
    sample_sid = next(iter(idx))
    assert isinstance(sample_sid, int)
    assert isinstance(idx[sample_sid], str)


def test_power_index_missing_static_index_is_absent() -> None:
    idx = load_power_index(FIXTURES / "power_static_index.json")
    assert -999999 not in idx


def test_enhancement_index_resolves_static_index() -> None:
    idx = load_enhancement_index(FIXTURES / "enhancements.json")
    assert idx
    sid = next(iter(idx))
    entry = idx[sid]
    assert isinstance(entry, EnhIndexEntry)
    assert isinstance(entry.uid, str)
    assert entry.type_id in {"None", "Normal", "InventO", "SpecialO", "SetO"}


def test_indexes_missing_file_raise(tmp_path: Path) -> None:
    with pytest.raises(FileNotFoundError):
        load_power_index(tmp_path / "nope.json")
    with pytest.raises(FileNotFoundError):
        load_enhancement_index(tmp_path / "nope.json")
