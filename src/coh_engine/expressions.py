"""Magnitude expressions and conditional effects.

Mids evaluates an ``Expression`` magnitude by replacing its keywords with text and handing the arithmetic to a
math engine, and decides whether a conditional effect counts by evaluating its ``ActiveConditionals``. This
module reproduces both. The keyword substitution is textual and ordered, so floats are rendered as .NET
renders them and the constants are replaced before the function patterns.
Spec: .claude/plans/assembly-seams.md, seam 3.
"""

import ast
import math
import re
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from typing import TYPE_CHECKING

from coh_engine.maths import f32

if TYPE_CHECKING:
    from coh_engine.effect import Effect, Power

# Mids' default combat setting; the reference dumps are taken at full health and record no other value.
_PLAYER_HP_PERCENT = 100
_VARIABLE_VAL = re.compile(r"([a-zA-Z\-_\.]+)>variableVal")
_POWER_GROUP_IN = re.compile(r"powerGroupIn\(([a-zA-Z0-9_\-\.]+)\)")
_POWER_GROUP_NOT_IN = re.compile(r"powerGroupNotIn\(([a-zA-Z0-9_\-\.]+)\)")
_DOUBLE_EPSILON = 5e-324


def _divide(a: float, b: float) -> float:
    """Double division as .NET does it: a zero divisor gives an infinity or NaN, not an exception."""
    if b:
        return a / b
    return math.nan if a == 0 else math.copysign(math.inf, a)


_EXPR_FUNCTIONS: Mapping[str, Callable[..., float]] = {
    "eq": lambda a, b: 1 if abs(a - b) < _DOUBLE_EPSILON else 0,
    "ne": lambda a, b: 1 if abs(a - b) > _DOUBLE_EPSILON else 0,
    "gt": lambda a, b: 1 if a > b else 0,
    "gte": lambda a, b: 1 if a >= b else 0,
    "lt": lambda a, b: 1 if a < b else 0,
    "lte": lambda a, b: 1 if a <= b else 0,
    "minmax": lambda a, b, c: min(b if b > c else c, max(c if b > c else b, a)),
    "and_": lambda a, b: 1 if a != 0 and b != 0 else 0,
    "or_": lambda a, b: 1 if a != 0 or b != 0 else 0,
}
_BINARY_OPS: Mapping[type, Callable[[float, float], float]] = {
    ast.Add: lambda a, b: a + b,
    ast.Sub: lambda a, b: a - b,
    ast.Mult: lambda a, b: a * b,
    ast.Div: _divide,
}


@dataclass(frozen=True, slots=True)
class ExpressionContext:
    """The build state expressions and conditions read."""

    prev_hp_max: float  # Totals.HPMax of the previous recompute
    build_power_names: frozenset[str]  # full names of every power in the build
    active_power_names: frozenset[str]  # full names of the StatInclude powers
    variable_values: Mapping[str, int]  # full name -> the entry's VariableValue
    player_hp_percent: int = _PLAYER_HP_PERCENT


def _float_text(value: float) -> str:
    """A float32 as .NET formats it into a string: the shortest text that reads back to the same value."""
    value = f32(value)
    return next(text for digits in range(1, 18) if f32(float(text := f"{value:.{digits}g}")) == value)


def evaluate_magnitude(
    effect: Effect, power: Power, ctx: ExpressionContext | None, *, scale: float, modifier: float
) -> float:
    """``Expressions.Parse`` for a magnitude: substitute the keywords as text, then evaluate the arithmetic.

    ``power`` is the power that owns the effect once assembled. Only the keywords and functions the committed
    builds use are substituted; anything else is refused rather than evaluated to the 0 Mids returns on a
    parse failure.

    Raises:
        NotImplementedError: ``E08`` for an expression outside the ported keywords and grammar, or one that
            needs ``ctx`` when none was given.
    """

    def need_context(what: str) -> ExpressionContext:
        if ctx is None:
            raise _unported(effect, power, f"{what}, which needs the build context")
        return ctx

    constants: tuple[tuple[str, Callable[[], str]], ...] = (
        ("power.base>activateperiod", lambda: _float_text(power.activate_period)),
        ("power.base>areafactor", lambda: _float_text(power.aoe_modifier)),
        ("power.base>rechargetime", lambda: _float_text(power.base_recharge_time)),
        ("@StdResult", lambda: _float_text(scale)),
        ("modifier>current", lambda: _float_text(modifier)),
        ("source>Max.kHitPoints", lambda: _float_text(need_context("source>Max.kHitPoints").prev_hp_max)),
        ("cfg>player>hp", lambda: str(_PLAYER_HP_PERCENT if ctx is None else ctx.player_hp_percent)),
    )
    expr = effect.expr_magnitude
    for keyword, text in constants:
        if keyword in expr:
            expr = expr.replace(keyword, text())

    def variable_value(match: re.Match[str]) -> str:
        name = match.group(1)
        if name == power.full_name and (ctx is None or name not in ctx.variable_values):
            return str(power.variable_value)
        return str(need_context(f"the variable value of another power ({name})").variable_values.get(name, 0))

    expr = _VARIABLE_VAL.sub(variable_value, expr)
    expr = _POWER_GROUP_IN.sub(lambda m: "1" if power.full_name.startswith(m.group(1)) else "0", expr)
    expr = _POWER_GROUP_NOT_IN.sub(lambda m: "0" if power.full_name.startswith(m.group(1)) else "1", expr)
    expr = re.sub(r"\b(and|or)\s*\(", r"\1_(", expr)
    try:
        tree = ast.parse(expr, mode="eval")
    except SyntaxError as error:
        raise _unported(effect, power, f"text that is not plain arithmetic ({expr!r})") from error
    return f32(_evaluate(tree.body, effect, power))


def _evaluate(node: ast.expr, effect: Effect, power: Power) -> float:
    if isinstance(node, ast.Constant) and isinstance(node.value, int | float):
        return float(node.value)
    if isinstance(node, ast.BinOp) and type(node.op) in _BINARY_OPS:
        return _BINARY_OPS[type(node.op)](_evaluate(node.left, effect, power), _evaluate(node.right, effect, power))
    if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.USub | ast.UAdd):
        operand = _evaluate(node.operand, effect, power)
        return -operand if isinstance(node.op, ast.USub) else operand
    if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in _EXPR_FUNCTIONS:
        return float(_EXPR_FUNCTIONS[node.func.id](*(_evaluate(arg, effect, power) for arg in node.args)))
    raise _unported(effect, power, f"the term {ast.unparse(node)!r}")


def _unported(effect: Effect, power: Power, what: str) -> NotImplementedError:
    return NotImplementedError(
        f"E08: cannot compute Mag for {power.full_name} effect {effect.index}: the magnitude expression "
        f"{effect.expr_magnitude!r} uses {what}, which the expression evaluator does not port; "
        "port that keyword or function against a Mids reference dump that exercises it"
    )


def _boolean(effect: Effect, key: str, value: str) -> bool:
    """``Convert.ToBoolean`` on a condition value: ``True`` or ``False`` in any case."""
    text = value.strip().lower()
    if text not in ("true", "false"):
        raise ValueError(
            f"E93: effect {effect.index} condition {key!r}: expected the value 'True' or 'False', "
            f"found {value!r}; fix the condition record"
        )
    return text == "true"


def _condition_holds(effect: Effect, key: str, value: str, ctx: ExpressionContext) -> bool:
    """One ``ActiveConditionals`` entry (``Effect.ValidateConditional(int)``).

    The key reads ``<type>:<power full name>``, optionally led by ``AND `` or ``OR ``.
    """
    kind, _, power_name = key.replace("AND ", "").replace("OR ", "").partition(":")
    if kind == "Active":
        return (power_name in ctx.active_power_names) == _boolean(effect, key, value)
    if kind == "Taken":
        return (power_name in ctx.build_power_names) == _boolean(effect, key, value)
    if kind == "Stacks":
        operator, _, count = value.partition(" ")
        try:
            wanted = int(count)
        except ValueError:
            raise ValueError(
                f"E93: effect {effect.index} condition {key!r}: expected a Stacks value of the form "
                f"'<op> <integer>', found {value!r}; fix the condition record"
            ) from None
        stacks = ctx.variable_values.get(power_name, 0)
        return {"=": stacks == wanted, ">": stacks > wanted, "<": stacks < wanted}.get(operator, False)
    raise NotImplementedError(
        f"E92: effect {effect.index} condition {key!r}: expected a condition type of Active, Taken or Stacks, "
        f"found {kind!r}; port that condition type against a Mids reference dump that exercises it"
    )


def conditionals_met(effect: Effect, ctx: ExpressionContext) -> bool:
    """``Effect.ValidateConditional()``: the conditions chained left to right, ``AND`` binding tighter than ``OR``.

    Only a condition led by ``OR ``, and not the first, starts a new group; every other one is ANDed on.

    Raises:
        NotImplementedError: ``E92`` for a condition type other than Active, Taken and Stacks.
        ValueError: ``E93`` for a condition value that does not read as that type's value.
    """
    groups: list[list[bool]] = []
    for position, (key, value) in enumerate(effect.conditionals):
        held = _condition_holds(effect, key, value, ctx)
        if groups and not (position > 0 and key.startswith("OR ")):
            groups[-1].append(held)
        else:
            groups.append([held])
    return not groups or any(all(group) for group in groups)


def effect_included(effect: Effect, ctx: ExpressionContext | None) -> bool:
    """``Effect.CanInclude``: an effect with a special case is out; one with conditions needs them met.

    Without a context only an unconditional effect is in, as before conditions were ported.

    Raises:
        ValueError: ``E94`` for an effect that counts conditions but carries none to evaluate, given a context.
    """
    if effect.special_case != "None":
        return False
    if ctx is None:
        return effect.active_conditionals_count == 0 and not effect.conditionals
    if effect.active_conditionals_count and not effect.conditionals:
        raise ValueError(
            f"E94: effect {effect.index} counts {effect.active_conditionals_count} conditions but carries none: "
            "expected the conditions loaded with the effect, found an empty list; "
            "load the effect from a record that holds ActiveConditionals"
        )
    return conditionals_met(effect, ctx)
