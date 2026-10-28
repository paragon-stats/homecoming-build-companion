"""Mids-parity tests for the .mxd semantic reader.

Each ``.mxd`` fixture was produced by Mids itself (the Mids dump harness loads a
sample ``.mbd`` and re-saves it via ``MxDBuildSaveString``). Parsing the ``.mxd``
must reproduce the same build the ``.mbd`` describes — validating the reader
against real Mids output, not a hand-authored buffer.
"""

from pathlib import Path

import pytest

from coh_engine.archetypes import load_archetypes
from coh_engine.buildfile.dbindex import EnhIndexEntry, load_enhancement_index, load_power_index
from coh_engine.buildfile.mbd import Build, Enhancement, PowerEntry, load_mbd
from coh_engine.buildfile.mxd import MxdBuild, read_mxd

Indexes = tuple[dict[int, str], dict[int, EnhIndexEntry]]

REPO = Path(__file__).resolve().parents[3]
SAMPLES = REPO / "samples" / "builds"
FIXTURES = Path(__file__).parent / "fixtures"
MXD_DIR = FIXTURES / "mxd"
MIDS = FIXTURES / "mids"
ARCHETYPES = load_archetypes(MIDS / "archetypes.json")


@pytest.fixture(scope="module")
def indexes() -> Indexes:
    return (
        load_power_index(MIDS / "power_static_index.json"),
        load_enhancement_index(MIDS / "enhancements.json"),
    )


def _mbd_for(mxd_path: Path) -> Path:
    matches = list(SAMPLES.rglob(f"{mxd_path.stem}.mbd"))
    assert len(matches) == 1, f"expected one .mbd for {mxd_path.stem}, found {len(matches)}"
    return matches[0]


# Mids injects this virtual power at load time to carry set bonuses; it is not a
# user pick, so a community-authored .mbd may lack it while the re-exported .mxd
# has it. Exclude it so the two are positionally comparable.
_VIRTUAL_POWERS = frozenset({"Inherent.Inherent.Special_Set_Bonuses"})


def _named(entries: tuple[PowerEntry, ...]) -> list[PowerEntry]:
    return [p for p in entries if p.power_name and p.power_name not in _VIRTUAL_POWERS]


def _assert_enh_equal(mx_enh: Enhancement | None, mb_enh: Enhancement | None, ctx: str) -> None:
    assert (mx_enh is None) == (mb_enh is None), f"{ctx}: enhancement presence differs"
    if mx_enh is None or mb_enh is None:
        return
    assert mx_enh.uid == mb_enh.uid, f"{ctx}: uid"
    assert mx_enh.relative_level == mb_enh.relative_level, f"{ctx}: relative_level"
    assert mx_enh.grade == mb_enh.grade, f"{ctx}: grade"
    assert mx_enh.io_level == mb_enh.io_level, f"{ctx}: io_level"


def _assert_build_parity(mx: MxdBuild, mb: Build) -> None:
    assert mx.class_name == mb.class_name
    assert mx.name == mb.name
    assert mx.last_power == mb.last_power
    mxn, mbn = _named(mx.power_entries), _named(mb.power_entries)
    assert len(mxn) == len(mbn), "named power-entry count differs"
    for a, b in zip(mxn, mbn, strict=True):
        assert a.power_name == b.power_name, f"power name {a.power_name} != {b.power_name}"
        assert a.level == b.level, f"{a.power_name}: level"
        assert a.stat_include == b.stat_include, f"{a.power_name}: stat_include"
        assert len(a.slot_entries) == len(b.slot_entries), f"{a.power_name}: slot count"
        for i, (sa, sb) in enumerate(zip(a.slot_entries, b.slot_entries, strict=True)):
            assert sa.level == sb.level, f"{a.power_name} slot {i}: level"
            _assert_enh_equal(sa.enhancement, sb.enhancement, f"{a.power_name} slot {i}")


def test_all_mxd_fixtures_match_their_mbd(indexes: Indexes) -> None:
    # Fixtures are valid builds on the current Homecoming DB. Legacy community
    # builds (older DB / pre-current enhancement UIDs that Mids resolves on load)
    # are excluded — their raw .mbd does not round-trip against the current DB.
    power_index, enh_index = indexes
    mxd_files = sorted(MXD_DIR.glob("*.mxd"))
    assert len(mxd_files) == 5
    for mxd_path in mxd_files:
        mx = read_mxd(mxd_path.read_text(encoding="utf-8"), power_index, enh_index, ARCHETYPES)
        mb = load_mbd(_mbd_for(mxd_path))
        _assert_build_parity(mx, mb)


def test_every_legacy_mxd_reads(indexes: Indexes) -> None:
    # Real single-line files from the community archive: every one must read,
    # and every failure is listed rather than dropped.
    power_index, enh_index = indexes
    legacy = sorted((MXD_DIR / "legacy").rglob("*.mxd"))
    assert len(legacy) == 131
    failures = []
    for path in legacy:
        try:
            read_mxd(path.read_text(encoding="utf-8"), power_index, enh_index, ARCHETYPES)
        except (ValueError, KeyError) as exc:
            failures.append((path.name, str(exc)))
    assert failures == []


def test_scrapper_specifics(indexes: Indexes) -> None:
    power_index, enh_index = indexes
    mx = read_mxd(
        (MXD_DIR / "Scrapper - Dark-Shield.mxd").read_text(encoding="utf-8"), power_index, enh_index, ARCHETYPES
    )
    assert mx.format == "current"
    assert mx.class_name == "Class_Scrapper"
    smite = _named(mx.power_entries)[0]
    assert smite.power_name == "Scrapper_Melee.Dark_Melee.Smite"
    assert smite.level == 0
    enh = smite.slot_entries[0].enhancement
    assert enh is not None
    assert enh.uid == "Crafted_Hecatomb_A"
    assert enh.relative_level == "PlusFive"
