"""Tests for the AttribMod table loader and the GetModifier lookup."""

from pathlib import Path

import pytest

from coh_engine.archetypes import load_archetypes
from coh_engine.attribmod import AttribMods, get_modifier, load_attribmod
from coh_engine.maths import f32

FIXTURES = Path(__file__).parent / "fixtures"
ATTRIBMOD = FIXTURES.parents[3] / "tests" / "fixtures" / "attribmod" / "AttribMod.json"
ARCHETYPES = FIXTURES.parents[3] / "tests" / "fixtures" / "mids" / "archetypes.json"

# Melee_Damage (table 0), row 49 (character level 50), first ten class columns.
MELEE_DAMAGE_ROW49 = [
    -55.6102,
    -30.5856,
    -30.5856,
    -62.5615,
    -52.8297,
    -47.2687,
    -47.2687,
    -61.1712,
    -41.7077,
    -55.6102,
]


@pytest.fixture(scope="module")
def mods() -> AttribMods:
    return load_attribmod(ATTRIBMOD)


def test_table_shape(mods: AttribMods) -> None:
    assert len(mods.tables) == 102
    melee = mods.tables[0]
    assert melee.id == "Melee_Damage"
    assert len(melee.table) == 55
    assert len(melee.table[0]) == 60


def test_index_from_name_is_case_insensitive(mods: AttribMods) -> None:
    assert mods.nid_from_uid_attribmod("melee_damage") == 0
    assert mods.nid_from_uid_attribmod("Nonexistent_Table") == -1


def test_melee_damage_row49_golden(mods: AttribMods) -> None:
    melee = mods.tables[0]
    got = [melee.table[49][c] for c in range(10)]
    assert got == [f32(v) for v in MELEE_DAMAGE_ROW49]


def test_get_modifier_resolves_column_indirection(mods: AttribMods) -> None:
    archetypes = load_archetypes(ARCHETYPES)
    # Blaster is class 0, Column 0 -> Melee_Damage[49][0].
    val = get_modifier(mods, archetypes.classes, i_class=0, i_table=0, i_level=49)
    assert val == f32(-55.6102)
    # Class index 3 -> its stored Column -> that column of the same row.
    col = archetypes.classes[3].column
    expected = mods.tables[0].table[49][col]
    assert get_modifier(mods, archetypes.classes, i_class=3, i_table=0, i_level=49) == expected


@pytest.mark.parametrize(
    ("i_class", "i_table", "i_level"),
    [
        (-1, 0, 49),  # iClass < 0
        (0, -1, 49),  # iTable < 0
        (0, 0, -1),  # iLevel < 0
        (999, 0, 49),  # iClass past end
        (0, 999, 49),  # iTable past end
        (0, 0, 999),  # iLevel past end
    ],
)
def test_get_modifier_out_of_bounds_returns_zero(mods: AttribMods, i_class: int, i_table: int, i_level: int) -> None:
    archetypes = load_archetypes(ARCHETYPES)
    assert get_modifier(mods, archetypes.classes, i_class, i_table, i_level) == 0.0


def test_get_modifier_negative_column_returns_zero(mods: AttribMods) -> None:
    from coh_engine.archetypes import Archetype

    bad = Archetype(
        index=0,
        class_name="X",
        display_name="X",
        class_type="Hero",
        column=-1,
        playable=True,
        hitpoints=1,
        hp_cap=1.0,
        res_cap=1.0,
        damage_cap=1.0,
        recharge_cap=1.0,
        recovery_cap=1.0,
        regen_cap=1.0,
        perception_cap=1.0,
        base_recovery=1.0,
        base_regen=1.0,
        base_threat=1.0,
    )
    assert get_modifier(mods, (bad,), i_class=0, i_table=0, i_level=49) == 0.0


def test_get_modifier_column_past_row_width_returns_zero(mods: AttribMods) -> None:
    from coh_engine.archetypes import Archetype

    # A class whose Column exceeds the 60-wide row must yield 0, not IndexError.
    wide = Archetype(
        index=0,
        class_name="X",
        display_name="X",
        class_type="Hero",
        column=99,
        playable=True,
        hitpoints=1,
        hp_cap=1.0,
        res_cap=1.0,
        damage_cap=1.0,
        recharge_cap=1.0,
        recovery_cap=1.0,
        regen_cap=1.0,
        perception_cap=1.0,
        base_recovery=1.0,
        base_regen=1.0,
        base_threat=1.0,
    )
    assert get_modifier(mods, (wide,), i_class=0, i_table=0, i_level=49) == 0.0


def test_missing_file_raises(tmp_path: Path) -> None:
    with pytest.raises(FileNotFoundError):
        load_attribmod(tmp_path / "nope.json")
