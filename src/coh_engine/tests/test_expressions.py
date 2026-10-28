"""Tests for coh_engine.expressions — magnitude keywords and conditional-effect evaluation."""

import math
from collections.abc import Callable

import pytest

from coh_engine.effect import Effect, Power
from coh_engine.expressions import (
    ExpressionContext,
    _float_text,
    conditionals_met,
    effect_included,
    evaluate_magnitude,
)
from coh_engine.maths import f32

MakeEffect = Callable[..., Effect]
MakePower = Callable[..., Power]

_NAME = "Pool.Fighting.Cross_Punch"


def _ctx(**overrides: object) -> ExpressionContext:
    fields: dict[str, object] = {
        "prev_hp_max": f32(1339.35),
        "build_power_names": frozenset({"Pool.Fighting.Boxing"}),
        "active_power_names": frozenset({"Pool.Fighting.Boxing"}),
        "variable_values": {},
    }
    return ExpressionContext(**{**fields, **overrides})  # type: ignore[arg-type]


def _evaluate(
    make_effect: MakeEffect,
    make_power: MakePower,
    expr: str,
    ctx: ExpressionContext | None = None,
    /,
    *,
    scale: float = 1.0,
    modifier: float = 1.0,
    **power: object,
) -> float:
    fx = make_effect(attrib_type="Expression", expr_magnitude=expr)
    return evaluate_magnitude(fx, make_power(fx, full_name=_NAME, **power), ctx, scale=scale, modifier=modifier)


@pytest.mark.parametrize(
    ("value", "text"),
    [(f32(0.1), "0.1"), (f32(1 / 3), "0.33333334"), (0.1, "0.1"), (5.0, "5"), (f32(1339.35), "1339.35")],
)
def test_float_text_is_the_shortest_round_trip(value: float, text: str) -> None:
    assert _float_text(value) == text


@pytest.mark.parametrize(
    ("expr", "power", "want"),
    [
        ("power.base>rechargetime * 2", {"base_recharge_time": f32(0.1)}, f32(0.2)),
        ("power.base>areafactor + 1", {"aoe_modifier": f32(0.75)}, 1.75),
        ("power.base>activateperiod * 10", {"activate_period": f32(0.1)}, 1.0),
        ("power.base>areafactor", {}, 1.0),
        ("power.base>rechargetime", {}, 0.0),
    ],
)
def test_power_base_keywords_read_the_owning_power(
    expr: str, power: dict[str, float], want: float, make_effect: MakeEffect, make_power: MakePower
) -> None:
    assert _evaluate(make_effect, make_power, expr, **power) == f32(want)


def test_hit_point_keyword_reads_the_previous_maximum(make_effect: MakeEffect, make_power: MakePower) -> None:
    got = _evaluate(make_effect, make_power, "source>Max.kHitPoints * @StdResult", _ctx(), scale=f32(0.03))
    assert got == f32(1339.35 * f32(0.03))


def test_hit_point_keyword_needs_the_context(make_effect: MakeEffect, make_power: MakePower) -> None:
    with pytest.raises(NotImplementedError, match=r"E08.*source>Max.kHitPoints"):
        _evaluate(make_effect, make_power, "source>Max.kHitPoints")


def test_constants_are_replaced_before_functions(make_effect: MakeEffect, make_power: MakePower) -> None:
    expr = "minmax(source>Max.kHitPoints, 0, 1000) / power.base>areafactor + modifier>current"
    assert _evaluate(make_effect, make_power, expr, _ctx(), modifier=2.0, aoe_modifier=4.0) == 252.0


@pytest.mark.parametrize(
    ("expr", "want"),
    [
        ("powerGroupIn(Pool.Fighting) * 5", 5.0),
        ("powerGroupIn(Pool.Leaping)", 0.0),
        ("powerGroupNotIn(Pool.Leaping) * 5", 5.0),
        ("powerGroupNotIn(Pool.Fighting)", 0.0),
    ],
)
def test_power_group_functions_test_the_name_prefix(
    expr: str, want: float, make_effect: MakeEffect, make_power: MakePower
) -> None:
    assert _evaluate(make_effect, make_power, expr) == want


def test_player_hit_points_come_from_the_context(make_effect: MakeEffect, make_power: MakePower) -> None:
    expr = "minmax(60 - cfg>player>hp, 0, 100)"
    assert _evaluate(make_effect, make_power, expr, _ctx(player_hp_percent=25)) == 35.0
    assert _evaluate(make_effect, make_power, expr) == 0.0


def test_another_powers_variable_value_comes_from_the_context(make_effect: MakeEffect, make_power: MakePower) -> None:
    ctx = _ctx(variable_values={"Other.Power.Name": 4})
    assert _evaluate(make_effect, make_power, "Other.Power.Name>variableVal * 2", ctx) == 8.0


def test_a_power_missing_from_the_build_has_variable_value_zero(make_effect: MakeEffect, make_power: MakePower) -> None:
    assert _evaluate(make_effect, make_power, "Other.Power.Name>variableVal + 3", _ctx()) == 3.0


def test_own_variable_value_is_the_powers_unless_the_context_has_one(
    make_effect: MakeEffect, make_power: MakePower
) -> None:
    expr = f"{_NAME}>variableVal"
    assert _evaluate(make_effect, make_power, expr, _ctx(), variable_value=7) == 7.0
    assert _evaluate(make_effect, make_power, expr, _ctx(variable_values={_NAME: 9}), variable_value=7) == 9.0


def test_another_powers_variable_value_needs_the_context(make_effect: MakeEffect, make_power: MakePower) -> None:
    with pytest.raises(NotImplementedError, match=r"E08.*Other.Power.Name"):
        _evaluate(make_effect, make_power, "Other.Power.Name>variableVal")


@pytest.mark.parametrize("ctx", [None, _ctx()])
@pytest.mark.parametrize("expr", ["source>cur.kMeter", "powerIs(Pool.Fighting.Cross_Punch)", "@StdResult *"])
def test_other_keywords_are_still_refused(
    expr: str, ctx: ExpressionContext | None, make_effect: MakeEffect, make_power: MakePower
) -> None:
    with pytest.raises(NotImplementedError, match=r"E08"):
        _evaluate(make_effect, make_power, expr, ctx)


@pytest.mark.parametrize(
    ("expr", "check"),
    [
        ("1 / 0", lambda v: v == math.inf),
        ("-1 / 0", lambda v: v == -math.inf),
        ("0 / 0", math.isnan),
    ],
)
def test_division_by_zero_follows_double_arithmetic(
    expr: str, check: Callable[[float], bool], make_effect: MakeEffect, make_power: MakePower
) -> None:
    assert check(_evaluate(make_effect, make_power, expr))


def _conditional(make_effect: MakeEffect, *conditions: tuple[str, str], **overrides: object) -> Effect:
    return make_effect(conditionals=conditions, active_conditionals_count=len(conditions), **overrides)


@pytest.mark.parametrize(
    ("condition", "want"),
    [
        (("Active:Pool.Fighting.Boxing", "True"), True),
        (("Active:Pool.Fighting.Boxing", "False"), False),
        (("Active:Pool.Fighting.Kick", "true"), False),
        (("Active:Pool.Fighting.Kick", "False"), True),
        (("Taken:Pool.Fighting.Boxing", "True"), True),
        (("Taken:Pool.Fighting.Kick", "True"), False),
        (("Taken:Pool.Fighting.Kick", "False"), True),
        (("Stacks:Pool.Fighting.Kick", "= 3"), True),
        (("Stacks:Pool.Fighting.Kick", "= 2"), False),
        (("Stacks:Pool.Fighting.Kick", "> 2"), True),
        (("Stacks:Pool.Fighting.Kick", "> 3"), False),
        (("Stacks:Pool.Fighting.Kick", "< 4"), True),
        (("Stacks:Pool.Fighting.Kick", "< 3"), False),
        (("Stacks:Pool.Fighting.Kick", "~ 3"), False),
        (("Stacks:Pool.Fighting.Boxing", "= 0"), True),
    ],
)
def test_single_conditions(condition: tuple[str, str], want: bool, make_effect: MakeEffect) -> None:
    ctx = _ctx(variable_values={"Pool.Fighting.Kick": 3})
    assert conditionals_met(_conditional(make_effect, condition), ctx) is want


def test_no_conditions_are_met(make_effect: MakeEffect) -> None:
    assert conditionals_met(make_effect(), _ctx()) is True


_BOXING = "Active:Pool.Fighting.Boxing"
_KICK = "Active:Pool.Fighting.Kick"


@pytest.mark.parametrize(
    ("conditions", "want"),
    [
        # A and B and C: every condition must hold.
        ([(_BOXING, "True"), ("AND " + _KICK, "True")], False),
        ([(_BOXING, "True"), ("AND " + _KICK, "False")], True),
        ([(_BOXING, "True"), (_KICK, "False")], True),
        # A or B.
        ([(_KICK, "True"), ("OR " + _BOXING, "True")], True),
        ([(_KICK, "True"), ("OR " + _KICK, "True")], False),
        # AND binds tighter than OR: kick-and-boxing, or boxing.
        ([(_KICK, "True"), (_BOXING, "True"), ("OR " + _BOXING, "True")], True),
        ([(_KICK, "True"), ("OR " + _BOXING, "False"), (_BOXING, "True")], False),
        ([(_BOXING, "False"), ("OR " + _KICK, "False"), (_BOXING, "True")], True),
        # An OR prefix on the first condition is not an operator.
        ([("OR " + _KICK, "False")], True),
    ],
)
def test_conditions_chain_with_and_before_or(
    conditions: list[tuple[str, str]], want: bool, make_effect: MakeEffect
) -> None:
    assert conditionals_met(_conditional(make_effect, *conditions), _ctx()) is want


@pytest.mark.parametrize("condition", [("Team:Pool.Fighting.Boxing", "= 1"), ("Config:cfg.player.hp", "> 5")])
def test_other_condition_types_are_refused(condition: tuple[str, str], make_effect: MakeEffect) -> None:
    with pytest.raises(NotImplementedError, match=r"E92.*Active, Taken or Stacks"):
        conditionals_met(_conditional(make_effect, condition), _ctx())


def test_a_key_with_the_type_after_the_power_is_refused(make_effect: MakeEffect) -> None:
    with pytest.raises(NotImplementedError, match=r"E92.*'Pool.Fighting.Boxing'"):
        conditionals_met(_conditional(make_effect, ("Pool.Fighting.Boxing:Active", "True")), _ctx())


@pytest.mark.parametrize("condition", [("Active:X", "yes"), ("Taken:X", "")])
def test_a_condition_value_that_is_not_a_boolean_is_refused(
    condition: tuple[str, str], make_effect: MakeEffect
) -> None:
    with pytest.raises(ValueError, match=r"E93.*'True' or 'False'"):
        conditionals_met(_conditional(make_effect, condition), _ctx())


@pytest.mark.parametrize("value", ["", "=", "= two"])
def test_a_stacks_value_without_an_integer_is_refused(value: str, make_effect: MakeEffect) -> None:
    with pytest.raises(ValueError, match=r"E93.*Stacks"):
        conditionals_met(_conditional(make_effect, ("Stacks:X", value)), _ctx())


def test_unconditional_effect_is_included_with_or_without_a_context(make_effect: MakeEffect) -> None:
    assert effect_included(make_effect(), None) is True
    assert effect_included(make_effect(), _ctx()) is True


def test_special_case_effect_is_excluded(make_effect: MakeEffect) -> None:
    assert effect_included(make_effect(special_case="Hidden"), None) is False
    assert effect_included(make_effect(special_case="Hidden"), _ctx()) is False
    assert effect_included(_conditional(make_effect, (_BOXING, "True"), special_case="Hidden"), _ctx()) is False


def test_conditional_effect_needs_a_context_and_met_conditions(make_effect: MakeEffect) -> None:
    fx = _conditional(make_effect, (_BOXING, "True"))
    assert effect_included(fx, None) is False
    assert effect_included(fx, _ctx()) is True
    assert effect_included(_conditional(make_effect, (_KICK, "True")), _ctx()) is False


def test_conditions_known_only_by_count_are_not_evaluated(make_effect: MakeEffect) -> None:
    fx = make_effect(active_conditionals_count=2)
    assert effect_included(fx, None) is False
    with pytest.raises(ValueError, match=r"E94.*counts 2 conditions"):
        effect_included(fx, _ctx())


def test_conditions_without_a_count_still_exclude_without_a_context(make_effect: MakeEffect) -> None:
    assert effect_included(make_effect(conditionals=((_BOXING, "True"),)), None) is False
