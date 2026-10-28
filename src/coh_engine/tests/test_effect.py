"""Tests for coh_engine.effect — Effect.Mag and the GetModifier class routing."""

from collections.abc import Callable

import pytest

from coh_engine.archetypes import ArchetypeDb
from coh_engine.attribmod import AttribMods
from coh_engine.effect import Effect, Power, _parse_requirement_groups, effect_mag, get_modifier_for_effect
from coh_engine.maths import f32


def test_parse_requirement_groups_drops_blanks_and_empty_groups() -> None:
    """A ``Requires`` array: blank second slots (single-power reqs) and empty groups are dropped."""
    raw = [["A", "B"], ["C", ""], ["", ""], []]
    assert _parse_requirement_groups(raw) == (("A", "B"), ("C",))


def test_parse_requirement_groups_empty_is_empty() -> None:
    """No requirement data parses to an empty tuple."""
    assert _parse_requirement_groups(()) == ()


MakeEffect = Callable[..., Effect]
MakePower = Callable[..., Power]


def test_magnitude_is_scale_times_nmag_times_modifier(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(scale=0.5, n_magnitude=3.0, modifier_table="Twos", n_modifier_table=1)
    power = make_power(fx)
    # 0.5 * 3.0 * Twos[49][column 0] = 0.5 * 3.0 * 2.0
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(3.0)


def test_damage_effect_reports_negative_mag(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(effect_type="Damage", scale=2.0)
    power = make_power(fx)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(-2.0)


def test_duration_attrib_type_returns_nmagnitude(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(attrib_type="Duration", scale=99.0, n_magnitude=4.0)
    power = make_power(fx)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(4.0)


def test_blank_expression_falls_back_to_scale_times_nmag(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(attrib_type="Expression", scale=2.0, n_magnitude=3.0)
    power = make_power(fx)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(6.0)


@pytest.mark.parametrize(
    ("expr", "want"),
    [
        # Fury: the effect's Scale times the power's own variable value.
        ("@StdResult*Inherent.Inherent.Rage_Buff>variableVal", f32(0.1) * 7),
        # Super Reflexes' scaling resistance at the default full health is zero.
        ("0.003 * minmax(60 - cfg>player>hp, 0, 100)", 0.0),
        # A set bonus that scales with missing health, at full health.
        ("-0.001 * cfg>player>hp + 0.129", 0.029),
        ("modifier>current * (lt(3, 10) + lte(10, 10) + gt(1, 2) + gte(2, 2))", 6.0),
        ("eq(1, 1) + ne(1, 2) + and(1, 0) + or(0, 1) + 4 / 2 - +1", 4.0),
        ("minmax(500, 100, 0)", 100.0),
    ],
)
def test_magnitude_expression_is_evaluated(
    expr: str,
    want: float,
    make_effect: MakeEffect,
    make_power: MakePower,
    tiny_mods: AttribMods,
    tiny_classes: ArchetypeDb,
) -> None:
    fx = make_effect(
        attrib_type="Expression", expr_magnitude=expr, scale=f32(0.1), modifier_table="Twos", n_modifier_table=1
    )
    power = make_power(fx, full_name="Inherent.Inherent.Rage_Buff", variable_value=7)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(want)


def test_damage_expression_is_negated(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(effect_type="Damage", attrib_type="Expression", expr_magnitude="@StdResult * 2", scale=1.5)
    assert effect_mag(fx, make_power(fx), tiny_mods, tiny_classes, 0) == f32(-3.0)


@pytest.mark.parametrize(
    "expr",
    [
        "@StdResult * source>cur.kToHit",  # a keyword the evaluator does not port
        "Other.Power.Name>variableVal * 2",  # another power's variable value
        "max(1, 2)",  # a function Mids does not register
        "2 ** 3",  # an operator outside Mids' infix grammar
        "@StdResult *",  # not parseable
    ],
)
def test_unported_expression_is_refused(
    expr: str, make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(attrib_type="Expression", expr_magnitude=expr)
    with pytest.raises(NotImplementedError, match="E08"):
        effect_mag(fx, make_power(fx), tiny_mods, tiny_classes, 0)


def test_unknown_attrib_type_yields_zero(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(attrib_type="Unknown")
    power = make_power(fx)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == 0.0


def test_forced_class_overrides_archetype_column(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(modifier_table="Twos", n_modifier_table=1)
    power = make_power(fx, forced_class="Class_Pet")
    # Class_Pet has Column 1 -> Twos[49][1] = 3.0, not the archetype's 2.0.
    assert get_modifier_for_effect(fx, power, tiny_mods, tiny_classes, 0) == f32(3.0)


def test_absorbed_class_beats_archetype_but_not_forced_class(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(modifier_table="Twos", n_modifier_table=1, absorbed_class_n_id=1)
    assert get_modifier_for_effect(fx, make_power(fx), tiny_mods, tiny_classes, 0) == f32(3.0)
    forced = make_power(fx, forced_class="Class_Test")
    assert get_modifier_for_effect(fx, forced, tiny_mods, tiny_classes, 0) == f32(2.0)


def test_variable_scaling_zeroes_variable_modified_effects(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(scale=0.3, variable_modified=True)
    power = make_power(fx, variable_enabled=True, variable_value=0)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == 0.0


def test_variable_scaling_multiplies_by_variable_value(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(scale=0.25, variable_modified=True)
    power = make_power(fx, variable_enabled=True, variable_value=4)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(1.0)


def test_variable_scaling_respects_ignore_scaling(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(scale=0.5, variable_modified=True, ignore_scaling=True)
    power = make_power(fx, variable_enabled=True, variable_value=0)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(0.5)


def test_variable_scaling_needs_variable_enabled_power(
    make_effect: MakeEffect, make_power: MakePower, tiny_mods: AttribMods, tiny_classes: ArchetypeDb
) -> None:
    fx = make_effect(scale=0.5, variable_modified=True)
    power = make_power(fx, variable_enabled=False, variable_value=0)
    assert effect_mag(fx, power, tiny_mods, tiny_classes, 0) == f32(0.5)
