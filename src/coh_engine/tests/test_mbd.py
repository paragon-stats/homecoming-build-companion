"""Tests for the .mbd (modern JSON) build reader."""

import json
from pathlib import Path

import pytest

from coh_engine.buildfile.mbd import (
    Build,
    dump_mbd,
    load_mbd,
    load_mbd_db,
    parse_mbd,
    powers_from_build,
    slots_from_build,
)

REPO = Path(__file__).resolve().parents[3]
SAMPLES = REPO / "samples" / "builds"
# samples/builds is split into community/ and operator-builds/ subfolders.
DARK_SHIELD = SAMPLES / "community" / "Scrapper - Dark-Shield.mbd"


def test_loads_all_sample_builds() -> None:
    # Recursively discover every .mbd under samples/builds (community/,
    # operator-builds/, and any future subfolders); each must parse.
    files = sorted(SAMPLES.rglob("*.mbd"))
    assert files, "no sample .mbd builds found"
    for f in files:
        build = load_mbd(f)
        assert isinstance(build, Build)
        assert build.power_entries, f"{f.name} has no power entries"


def test_meta_and_header_fields() -> None:
    build = load_mbd(DARK_SHIELD)
    assert build.meta.database == "Homecoming"
    assert build.class_name == "Class_Scrapper"
    assert build.origin == "Technology"
    assert build.alignment == "Hero"
    # Top-level Level is the raw character build level (no offset).
    assert build.level == 49
    # LastPower is assigned raw on .mbd load (no -1), per CharacterBuildData.cs:284.
    assert build.last_power == 25
    assert "Scrapper_Melee.Dark_Melee" in build.power_sets


def test_power_and_slot_level_minus_one_offset() -> None:
    build = load_mbd(DARK_SHIELD)
    smite = build.power_entries[0]
    assert smite.power_name == "Scrapper_Melee.Dark_Melee.Smite"
    # Stored Level 1 -> engine 0-based level 0 (character level 1).
    assert smite.level == 0
    first_slot = smite.slot_entries[0]
    # Stored slot Level 1 -> engine 0.
    assert first_slot.level == 0
    assert first_slot.enhancement is not None
    assert first_slot.enhancement.uid == "Crafted_Hecatomb_A"
    assert first_slot.enhancement.relative_level == "PlusFive"
    assert first_slot.enhancement.io_level == 49


def test_empty_slot_enhancement_is_none(tmp_path: Path) -> None:
    data = {
        "BuiltWith": {
            "App": "Mids Reborn",
            "Version": "3.8.6.0",
            "Database": "Homecoming",
            "DatabaseVersion": "2026.1.1242",
        },
        "Level": "50",
        "Class": "Class_Blaster",
        "Origin": "Magic",
        "Alignment": "Hero",
        "Name": "T",
        "Comment": "",
        "PowerSets": ["A"],
        "LastPower": 0,
        "PowerEntries": [
            {
                "PowerName": "A.B.C",
                "Level": 2,
                "StatInclude": True,
                "ProcInclude": False,
                "VariableValue": 0,
                "InherentSlotsUsed": 0,
                "SubPowerEntries": [],
                "SlotEntries": [{"Level": 2, "IsInherent": False, "Enhancement": None, "FlippedEnhancement": None}],
            }
        ],
    }
    mbd = tmp_path / "empty.mbd"
    mbd.write_text(json.dumps(data), encoding="utf-8")
    build = load_mbd(mbd)
    slot = build.power_entries[0].slot_entries[0]
    assert slot.enhancement is None
    assert slot.flipped_enhancement is None
    assert slot.level == 1  # stored 2 -> engine 1


def test_legacy_enhancement_key_resolves_to_uid() -> None:
    # The EnhancementDataConverter accepts a legacy "Enhancement" key as the Uid.
    data = {
        "BuiltWith": {"App": "x", "Version": "1.0.0", "Database": "Homecoming", "DatabaseVersion": "1.0.0"},
        "Level": "50",
        "Class": "Class_Blaster",
        "Origin": "Magic",
        "Alignment": "Hero",
        "Name": "T",
        "Comment": "",
        "PowerSets": ["A"],
        "LastPower": 0,
        "PowerEntries": [
            {
                "PowerName": "A.B.C",
                "Level": 1,
                "StatInclude": True,
                "ProcInclude": False,
                "VariableValue": 0,
                "InherentSlotsUsed": 0,
                "SubPowerEntries": [],
                "SlotEntries": [
                    {
                        "Level": 1,
                        "IsInherent": False,
                        "Enhancement": {"Enhancement": "Legacy_Uid_X"},
                        "FlippedEnhancement": None,
                    }
                ],
            }
        ],
    }
    build = parse_mbd(data)
    enh = build.power_entries[0].slot_entries[0].enhancement
    assert enh is not None
    assert enh.uid == "Legacy_Uid_X"
    assert enh.grade == "None"  # default
    assert enh.io_level == 1  # default
    assert enh.relative_level == "Even"  # default


def test_sub_power_entries_parsed(tmp_path: Path) -> None:
    data = {
        "BuiltWith": {"App": "x", "Version": "1.0.0", "Database": "Homecoming", "DatabaseVersion": "1.0.0"},
        "Level": "50",
        "Class": "Class_Blaster",
        "Origin": "Magic",
        "Alignment": "Hero",
        "Name": "T",
        "Comment": "",
        "PowerSets": ["A"],
        "LastPower": 0,
        "PowerEntries": [
            {
                "PowerName": "A.B.C",
                "Level": 1,
                "StatInclude": True,
                "ProcInclude": True,
                "VariableValue": 3,
                "InherentSlotsUsed": 1,
                "SubPowerEntries": [{"PowerName": "Sub1", "StatInclude": True}],
                "SlotEntries": [],
            }
        ],
    }
    build = parse_mbd(data)
    pe = build.power_entries[0]
    assert pe.variable_value == 3
    assert pe.inherent_slots_used == 1
    assert pe.proc_include is True
    assert len(pe.sub_power_entries) == 1
    assert pe.sub_power_entries[0].power_name == "Sub1"
    assert pe.sub_power_entries[0].stat_include is True


def test_enhancement_object_without_uid_is_none() -> None:
    # An enhancement object carrying neither "Uid" nor the legacy "Enhancement"
    # key resolves to an empty slot, not an error.
    data = {
        "BuiltWith": {"App": "x", "Version": "1.0.0", "Database": "Homecoming", "DatabaseVersion": "1.0.0"},
        "Level": "50",
        "Class": "Class_Blaster",
        "Origin": "Magic",
        "Alignment": "Hero",
        "Name": "T",
        "Comment": "",
        "PowerSets": ["A"],
        "LastPower": 0,
        "PowerEntries": [
            {
                "PowerName": "A.B.C",
                "Level": 1,
                "StatInclude": True,
                "ProcInclude": False,
                "VariableValue": 0,
                "InherentSlotsUsed": 0,
                "SubPowerEntries": [],
                "SlotEntries": [
                    {"Level": 1, "IsInherent": False, "Enhancement": {"Grade": "None"}, "FlippedEnhancement": None}
                ],
            }
        ],
    }
    build = parse_mbd(data)
    assert build.power_entries[0].slot_entries[0].enhancement is None


def test_empty_power_entry_keeps_raw_level() -> None:
    # An empty placeholder entry (PowerName="") does not get the -1 offset that a
    # resolved power gets — Mids substitutes Levels_MainPowers for such entries.
    data = {
        "BuiltWith": {"App": "x", "Version": "1.0.0", "Database": "Homecoming", "DatabaseVersion": "1.0.0"},
        "Level": "50",
        "Class": "Class_Blaster",
        "Origin": "Magic",
        "Alignment": "Hero",
        "Name": "T",
        "Comment": "",
        "PowerSets": ["A"],
        "LastPower": 0,
        "PowerEntries": [
            {
                "PowerName": "",
                "Level": 5,
                "StatInclude": False,
                "ProcInclude": False,
                "VariableValue": 0,
                "InherentSlotsUsed": 0,
                "SubPowerEntries": [],
                "SlotEntries": [],
            }
        ],
    }
    entry = parse_mbd(data).power_entries[0]
    assert entry.power_name == ""
    assert entry.level == 5  # raw, no -1


def test_missing_builtwith_raises() -> None:
    with pytest.raises(ValueError, match="BuiltWith"):
        parse_mbd({"Level": "50"})


def test_missing_file_raises(tmp_path: Path) -> None:
    with pytest.raises(FileNotFoundError):
        load_mbd(tmp_path / "nope.mbd")


BANE = (
    SAMPLES / "operator-builds" / "Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training).mbd"
)
DB = load_mbd_db(REPO / "src" / "coh_engine" / "tests" / "fixtures" / "mids")


def test_a_bane_build_resolves_to_24_picks_and_resolved_slots() -> None:
    build = load_mbd(BANE)
    powers = powers_from_build(build, DB)
    slots = slots_from_build(build, DB)
    assert sum(1 for p in powers if not p.full_name.startswith("Inherent.")) == 24
    assert [p.build_index for p in powers] == list(range(len(build.power_entries)))
    unresolved = [
        build.power_entries[idx].power_name
        for idx, refs in slots.items()
        for slot, ref in zip(build.power_entries[idx].slot_entries, refs, strict=True)
        if slot.enhancement and ref.enh < 0
    ]
    assert unresolved == [] and sum(len(refs) for refs in slots.values()) > 50
    first = powers[0]
    assert first.pick_level == build.power_entries[0].level and first.level >= 1 and first.chosen


def test_a_slot_keeps_its_grade_relative_level_and_io_level() -> None:
    build = parse_mbd(
        {
            **dump_mbd(load_mbd(BANE)),
            "PowerEntries": [
                {
                    "PowerName": "Inherent.Inherent.Brawl",
                    "Level": 1,
                    "SlotEntries": [
                        {
                            "Level": 1,
                            "IsInherent": True,
                            "Enhancement": {
                                "Uid": "Crafted_Accuracy",
                                "Grade": "SingleO",
                                "IoLevel": 30,
                                "RelativeLevel": "PlusOne",
                            },
                        },
                        {"Level": 3, "IsInherent": False, "Enhancement": None},
                    ],
                }
            ],
        }
    )
    ref, empty = slots_from_build(build, DB)[0]
    assert (ref.grade, ref.relative_level, ref.io_level, ref.level, ref.is_inherent) == (3, 5, 30, 0, True)
    assert ref.enh == DB.enhancements["Crafted_Accuracy"].nid
    assert (empty.enh, empty.level) == (-1, 2)


def test_an_enhancement_the_dump_lacks_is_refused_with_e78() -> None:
    doc = dump_mbd(load_mbd(BANE))
    entry = next(e for e in doc["PowerEntries"] if any(s["Enhancement"] for s in e["SlotEntries"]))
    next(s for s in entry["SlotEntries"] if s["Enhancement"])["Enhancement"]["Uid"] = "Crafted_No_Such_Thing"
    with pytest.raises(ValueError, match=r"E78: .*Crafted_No_Such_Thing.*enhancements.json"):
        slots_from_build(parse_mbd(doc), DB)


def test_a_power_the_dump_lacks_is_refused_with_e77() -> None:
    doc = dump_mbd(load_mbd(BANE))
    doc["PowerEntries"][0]["PowerName"] = "Nope.Nope.Nope"
    with pytest.raises(ValueError, match=r"E77: .*Nope.Nope.Nope.*powers.json"):
        powers_from_build(parse_mbd(doc), DB)


def test_an_empty_placeholder_entry_resolves_to_no_power() -> None:
    doc = dump_mbd(load_mbd(BANE))
    doc["PowerEntries"].insert(1, {"PowerName": "", "Level": 5, "SlotEntries": []})
    build = parse_mbd(doc)
    assert [p.build_index for p in powers_from_build(build, DB)][:3] == [0, 2, 3]
    assert 1 not in slots_from_build(build, DB)
