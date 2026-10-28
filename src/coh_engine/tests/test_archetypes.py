"""Tests for the archetype/class-record loader."""

from pathlib import Path

import pytest

from coh_engine.archetypes import Archetype, ArchetypeDb, load_archetypes

FIXTURES = Path(__file__).parent / "fixtures" / "mids"


@pytest.fixture
def archetypes() -> ArchetypeDb:
    return load_archetypes(FIXTURES / "archetypes.json")


def test_loads_all_classes(archetypes: ArchetypeDb) -> None:
    # The Homecoming DB dump carries every class record, playable and not.
    assert len(archetypes.classes) > 14
    assert all(isinstance(a, Archetype) for a in archetypes.classes)


def test_blaster_record_matches_dump(archetypes: ArchetypeDb) -> None:
    blaster = archetypes.classes[0]
    assert blaster.class_name == "Class_Blaster"
    assert blaster.display_name == "Blaster"
    assert blaster.column == 0
    assert blaster.playable is True
    assert blaster.hitpoints == 1205
    assert blaster.res_cap == 0.75
    assert blaster.damage_cap == 5
    assert blaster.regen_cap == 20


def test_column_is_stored_not_assumed(archetypes: ArchetypeDb) -> None:
    # Column is a stored indirection; for the standard playable ATs it equals
    # the class index, but the loader must read it, never assume it.
    for at in archetypes.classes:
        assert isinstance(at.column, int)


def test_nid_from_uid_class_is_case_insensitive(archetypes: ArchetypeDb) -> None:
    idx = archetypes.nid_from_uid_class("class_blaster")
    assert idx == 0
    assert archetypes.classes[idx].class_name == "Class_Blaster"


def test_nid_from_uid_class_missing_returns_minus_one(archetypes: ArchetypeDb) -> None:
    assert archetypes.nid_from_uid_class("Class_Nonexistent") == -1


def test_missing_file_raises(tmp_path: Path) -> None:
    with pytest.raises(FileNotFoundError):
        load_archetypes(tmp_path / "nope.json")


@pytest.mark.parametrize("class_name", ["Class_Arachnos_Soldier", "Class_Arachnos_Widow"])
def test_the_arachnos_classes_list_the_four_patron_pools(archetypes: ArchetypeDb, class_name: str) -> None:
    at = archetypes.classes[archetypes.nid_from_uid_class(class_name)]
    assert at.ancillary == (
        "Epic.VEAT_Leviathan_Mastery",
        "Epic.VEAT_Mace_Mastery",
        "Epic.VEAT_Mu_Mastery",
        "Epic.VEAT_Soul_Mastery",
    )


def test_every_playable_class_but_the_kheldians_has_an_ancillary_pool(archetypes: ArchetypeDb) -> None:
    kheldians = {"Class_Peacebringer", "Class_Warshade"}
    without = {at.class_name for at in archetypes.classes if at.playable and not at.ancillary}
    assert without == kheldians


def test_the_scrapper_record_lists_the_inherent_powers_mids_adds(archetypes: ArchetypeDb) -> None:
    scrapper = archetypes.classes[archetypes.nid_from_uid_class("Class_Scrapper")]
    levels = {p.full_name: p.level for p in scrapper.inherent_powers}
    assert set(levels) == {
        "Inherent.Inherent.Special_Set_Bonuses",
        "Inherent.Inherent.Brawl",
        "Inherent.Inherent.Critical_Hit",
        "Inherent.Inherent.Sprint",
        "Inherent.Inherent.Rest",
        "Inherent.Fitness.Swift",
        "Inherent.Fitness.Hurdle",
        "Inherent.Fitness.Health",
        "Inherent.Fitness.Stamina",
    }
    assert levels["Inherent.Inherent.Rest"] == 2
