"""Exemplar availability (+5 rule) and ForceLevel-gated totals.

Two concerns:

- The +5 availability predicate — a build-authoring helper (which powers/slots a
    character *has* when exemplared), independent of the totals math.
- ForceLevel-gated totals — recomputing at a lowered ForceLevel and matching the
    Mids reference dump (``shield_scrapper_set_bonuses_exemplar25``). Mids gates set
    bonuses on power pick ``Level <= ForceLevel`` and slot values on
    ``Slots.Level < ForceLevel`` with NO -3 retention; the port threads the same
    ForceLevel through ``config.force_level``.
"""

import json
from pathlib import Path
from typing import Any

import pytest

from coh_engine.archetypes import ArchetypeDb, load_archetypes
from coh_engine.attribmod import AttribMods, load_attribmod
from coh_engine.base_totals import (
    EnumMaps,
    ServerData,
    compute_base_totals,
    load_engine_config,
    load_enum_maps,
    load_server_data,
)
from coh_engine.effect import load_powers_effects
from coh_engine.enhancement import EnhancementRecord, load_build_slots, load_enhancement_effects
from coh_engine.exemplar import available_powers, is_power_available, is_slot_available, with_force_level
from coh_engine.maths import MathTables, f32, load_maths
from coh_engine.set_bonuses import SetBonusDb, load_set_bonus_db

FIXTURES = Path(__file__).parent / "fixtures"
MIDS = FIXTURES.parents[3] / "tests" / "fixtures" / "mids"

BASE_BUILD = "shield_scrapper_set_bonuses"
EXEMPLAR_BUILD = "shield_scrapper_set_bonuses_exemplar25"
EXEMPLAR_LEVEL = 25


@pytest.mark.parametrize(
    ("pick_level", "exemplar_level", "available"),
    [
        (1, 25, True),
        (25, 25, True),
        (30, 25, True),  # exactly exemplar + 5
        (31, 25, False),  # one past the +5 window
        (50, 25, False),
        (12, 10, True),  # travel by 12 is available at exemplar 10 (10 + 5 = 15)
    ],
)
def test_power_availability_follows_plus_five_rule(pick_level: int, exemplar_level: int, available: bool) -> None:
    """A power is available iff picked at level <= exemplar + 5."""
    assert is_power_available(pick_level, exemplar_level) is available


def test_slot_availability_follows_plus_five_rule() -> None:
    """A slot is available iff placed at level <= exemplar + 5 (same window as powers)."""
    assert is_slot_available(15, 10) is True
    assert is_slot_available(16, 10) is False


def test_available_powers_filters_by_pick_level() -> None:
    """available_powers keeps only the powers within the exemplar + 5 window."""
    powers = load_powers_effects(MIDS / "builds" / BASE_BUILD / "powers_effects.json")
    kept = available_powers(powers, EXEMPLAR_LEVEL)
    assert kept == [p for p in powers if p.pick_level <= EXEMPLAR_LEVEL + 5]
    assert all(p.pick_level <= EXEMPLAR_LEVEL + 5 for p in kept)
    assert len(kept) < len(powers)  # the build has powers picked past level 30


def test_with_force_level_replaces_only_force_level() -> None:
    """with_force_level returns a config identical but for force_level."""
    config = load_engine_config(MIDS / "config.json")
    lowered = with_force_level(config, EXEMPLAR_LEVEL)
    assert lowered.force_level == EXEMPLAR_LEVEL
    assert lowered.suppression == config.suppression
    assert lowered.disable_pve == config.disable_pve
    assert lowered.scaling_to_hit == config.scaling_to_hit


class TestExemplarTotalsParity:
    """compute_base_totals at ForceLevel 25 reproduces the Mids exemplar dump."""

    @pytest.fixture(scope="class")
    def mods(self) -> AttribMods:
        return load_attribmod(FIXTURES.parents[3] / "tests" / "fixtures" / "attribmod" / "AttribMod.json")

    @pytest.fixture(scope="class")
    def classes(self) -> ArchetypeDb:
        return load_archetypes(MIDS / "archetypes.json")

    @pytest.fixture(scope="class")
    def enums(self) -> EnumMaps:
        return load_enum_maps(MIDS / "enums.json")

    @pytest.fixture(scope="class")
    def server(self) -> ServerData:
        return load_server_data(MIDS / "server_data.json")

    @pytest.fixture(scope="class")
    def tables(self) -> MathTables:
        return load_maths(FIXTURES.parents[3] / "tests" / "fixtures" / "Maths.mhd")

    @pytest.fixture(scope="class")
    def enh_db(self) -> dict[int, EnhancementRecord]:
        return dict(load_enhancement_effects(MIDS / "enhancement_effects.json"))

    @pytest.fixture(scope="class")
    def set_db(self) -> SetBonusDb:
        return load_set_bonus_db(MIDS / "enhancement_sets.json", MIDS / "set_bonus_powers.json")

    def _totals(self, name: str) -> dict[str, Any]:
        with open(MIDS / "builds" / name / "totals.json", encoding="utf-8") as fh:
            data: dict[str, Any] = json.load(fh)
        return data

    def test_exemplar_gate_actually_changes_totals(self) -> None:
        """The exemplar dump differs from the level-50 dump — the gate has an effect."""
        base = self._totals(BASE_BUILD)["Totals"]
        exe = self._totals(EXEMPLAR_BUILD)["Totals"]
        assert exe["BuffHaste"] != base["BuffHaste"]  # bonuses from powers picked > 25 dropped

    def test_totals_at_force_level_25_match_mids(
        self,
        mods: AttribMods,
        classes: ArchetypeDb,
        enums: EnumMaps,
        server: ServerData,
        enh_db: dict[int, EnhancementRecord],
        tables: MathTables,
        set_db: SetBonusDb,
    ) -> None:
        """Recomputing at ForceLevel 25 reproduces the exemplar dump's key totals."""
        powers = load_powers_effects(MIDS / "builds" / BASE_BUILD / "powers_effects.json")
        slots = load_build_slots(MIDS / "builds" / BASE_BUILD / "slots.json")
        config = with_force_level(load_engine_config(MIDS / "config.json"), EXEMPLAR_LEVEL)
        expected = self._totals(EXEMPLAR_BUILD)
        result = compute_base_totals(
            powers,
            class_name=expected["Class"],
            mods=mods,
            classes=classes,
            enums=enums,
            config=config,
            server=server,
            slots=slots,
            enh_db=enh_db,
            tables=tables,
            set_db=set_db,
        )
        want = expected["Totals"]
        assert result.totals.buff_haste == f32(want["BuffHaste"])
        assert result.totals.end_use == f32(want["EndUse"])
        for i, cell in enumerate(want["Def"]):
            assert result.totals.def_[i] == f32(cell), f"Def[{i}]"
        for i, cell in enumerate(want["Res"]):
            assert result.totals.res[i] == f32(cell), f"Res[{i}]"
