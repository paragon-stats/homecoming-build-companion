from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest

from coh_engine.archetypes import Archetype, ArchetypeDb
from coh_engine.assembly_data import AssemblyPower, BuildPowerAssembly
from coh_engine.attribmod import AttribMods, ModifierTable
from coh_engine.effect import Effect, Power
from coh_engine.maths import MathTables, load_maths

FIXTURES = Path(__file__).parent / "fixtures"

MakeEffect = Callable[..., Effect]
MakePower = Callable[..., Power]
MakeAssemblyPower = Callable[..., AssemblyPower]
MakeBuildPower = Callable[..., BuildPowerAssembly]

_EFFECT_DEFAULTS: dict[str, Any] = {
    "index": 0,
    "effect_type": "Defense",
    "damage_type": "Smashing",
    "mez_type": "None",
    "et_modifies": "None",
    "scale": 1.0,
    "n_magnitude": 1.0,
    "n_duration": 0.0,
    "attrib_type": "Magnitude",
    "aspect": "Cur",
    "modifier_table": "Ones",
    "n_modifier_table": 0,
    "to_who": "Self",
    "pv_mode": "Any",
    "stacking": "No",
    "suppression": 0,
    "buffable": True,
    "resistible": False,
    "ignore_ed": False,
    "ignore_scaling": False,
    "variable_modified": False,
    "base_probability": 1.0,
    "probability": 1.0,
    "procs_per_minute": 0.0,
    "ticks": 0,
    "delayed_time": 0.0,
    "effect_class": "Primary",
    "special_case": "None",
    "n_id_class_name": -1,
    "absorbed_effect": False,
    "absorbed_power_type": "Auto_",
    "absorbed_class_n_id": -1,
    "active_conditionals_count": 0,
    "expr_magnitude": "",
    "expr_duration": "",
    "expr_probability": "",
    "display_percentage": False,
}

_POWER_DEFAULTS: dict[str, Any] = {
    "build_index": 0,
    "nid_power": 1,
    "full_name": "Test.Test_Set.Test_Power",
    "static_index": 1,
    "power_type": "Toggle",
    "forced_class": "",
    "click_buff": False,
    "level": 1,
    "end_cost": 0.0,
    "activate_period": 0.0,
    "toggle_cost": 0.0,
    "variable_enabled": False,
    "stat_include": True,
    "variable_value": 0,
}


@pytest.fixture(scope="session")
def maths_path() -> Path:
    """Path to the Homecoming Maths.mhd fixture (verbatim copy from the MidsReborn fork)."""
    return FIXTURES / "Maths.mhd"


@pytest.fixture(scope="session")
def tables(maths_path: Path) -> MathTables:
    """Parsed Maths.mhd tables, shared across the suite (immutable dataclass)."""
    return load_maths(maths_path)


@pytest.fixture
def make_effect() -> MakeEffect:
    """Factory for synthetic :class:`Effect` records with sane defaults."""

    def _make(**overrides: Any) -> Effect:
        return Effect(**{**_EFFECT_DEFAULTS, **overrides})

    return _make


@pytest.fixture
def make_power() -> MakePower:
    """Factory for synthetic :class:`Power` records taking effects positionally."""

    def _make(*effects: Effect, **overrides: Any) -> Power:
        fields = {**_POWER_DEFAULTS, **overrides}
        # Default pick_level to the (possibly overridden) level so the set-bonus
        # exemplar gate (which keys on pick_level) is exercised, not silently bypassed
        # at pick_level 0. Matches test_set_bonuses._power; callers can override.
        fields.setdefault("pick_level", fields["level"])
        return Power(effects=tuple(effects), **fields)

    return _make


_ASSEMBLY_POWER_DEFAULTS: dict[str, Any] = {
    "nid": 1,
    "full_name": "Test.Test_Set.Test_Power",
    "power_type": "Toggle",
    "power_set_type": "Primary",
    "entities_affected": frozenset({"Caster"}),
    "entities_auto_hit": frozenset({"Caster"}),
    "activate_period": 0.0,
    "recharge_time": 0.0,
    "base_recharge_time": 0.0,
    "cast_time": 0.0,
    "aoe_modifier": 1.0,
    "enhancements": frozenset(),
    "has_grant_power_effect": False,
    "has_power_override_effect": False,
    "absorb_summon_effects": False,
    "absorb_summon_attributes": False,
}


@pytest.fixture
def make_assembly_power() -> MakeAssemblyPower:
    """Factory for synthetic :class:`AssemblyPower` records taking effects positionally."""

    def _make(*effects: Effect, **overrides: Any) -> AssemblyPower:
        return AssemblyPower(effects=tuple(effects), **{**_ASSEMBLY_POWER_DEFAULTS, **overrides})

    return _make


@pytest.fixture
def make_build_power(make_assembly_power: MakeAssemblyPower) -> MakeBuildPower:
    """Factory for synthetic :class:`BuildPowerAssembly` records wrapping an :class:`AssemblyPower`."""

    def _make(
        *effects: Effect,
        build_index: int = 0,
        proc_include: bool = False,
        sub_powers: tuple[tuple[int, bool], ...] = (),
        **overrides: Any,
    ) -> BuildPowerAssembly:
        return BuildPowerAssembly(
            build_index=build_index,
            proc_include=proc_include,
            sub_powers=sub_powers,
            power=make_assembly_power(*effects, **overrides),
        )

    return _make


@pytest.fixture(scope="session")
def tiny_mods() -> AttribMods:
    """Two 50x2 tables: ``Ones`` (all 1.0) and ``Twos`` (2.0 col0, 3.0 col1)."""
    ones = ((1.0, 1.0),) * 50
    twos = ((2.0, 3.0),) * 50
    return AttribMods(
        tables=(
            ModifierTable(id="Ones", base_index=0, table=ones),
            ModifierTable(id="Twos", base_index=100, table=twos),
        )
    )


@pytest.fixture(scope="session")
def tiny_classes() -> ArchetypeDb:
    """Two class records with distinct AttribMod columns and small caps."""

    def _at(index: int, name: str, column: int) -> Archetype:
        return Archetype(
            index=index,
            class_name=name,
            display_name=name,
            class_type="Hero",
            column=column,
            playable=True,
            hitpoints=1000,
            hp_cap=1500.0,
            res_cap=0.75,
            damage_cap=4.0,
            recharge_cap=5.0,
            recovery_cap=5.0,
            regen_cap=20.0,
            perception_cap=1153.0,
            base_recovery=1.67,
            base_regen=1.0,
            base_threat=1.0,
        )

    return ArchetypeDb(classes=(_at(0, "Class_Test", 0), _at(1, "Class_Pet", 1)))
