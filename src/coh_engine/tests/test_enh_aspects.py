"""The per-aspect enhancement-handler registry (the scalar-fold dispatch)."""

from pathlib import Path

import pytest

from coh_engine.base_totals import GlobalEnhance
from coh_engine.enh_aspects import (
    END,
    RANGE,
    RECHARGE,
    cap_for,
    fold_scalar,
    global_term,
    registered_aspect_handlers,
)
from coh_engine.maths import MathTables, f32, load_maths

FIXTURES = Path(__file__).parent / "fixtures"


@pytest.fixture(scope="module")
def tables() -> MathTables:
    return load_maths(FIXTURES.parents[3] / "tests" / "fixtures" / "Maths.mhd")


@pytest.fixture
def ge() -> GlobalEnhance:
    # Stored as f32, as compute_base_totals builds it (from f32 _selfEnhance values).
    return GlobalEnhance(
        recharge=f32(0.425), end_discount=f32(0.0375), accuracy=f32(0.1), interrupt=f32(0.2), range=f32(0.3)
    )


def test_registry_lists_the_five_scalar_aspects() -> None:
    """All five scalar aspects are registered."""
    assert registered_aspect_handlers() == ["Accuracy", "EnduranceDiscount", "Interrupt", "Range", "RechargeTime"]


def test_global_term_reads_the_handlers_field(ge: GlobalEnhance) -> None:
    """Each handler resolves its own GlobalEnhance field."""
    assert global_term(RECHARGE, ge) == f32(0.425)
    assert global_term(END, ge) == f32(0.0375)
    assert global_term(RANGE, ge) == f32(0.3)


def test_only_recharge_is_capped() -> None:
    """cap_for returns the recharge cap for recharge, None for the uncapped aspects."""
    assert cap_for(RECHARGE, 5.0) == 5.0
    assert cap_for(END, 5.0) is None


def test_fold_scalar_reproduces_the_pass3_divisor(tables: MathTables) -> None:
    """(ED aggregate + global) + 1, uncapped for EnduranceDiscount."""
    divisor = fold_scalar(END, f32(0.2), f32(0.0375), ignore_enh=(), recharge_cap=5.0)
    assert divisor == f32(f32(f32(0.2 + 0.0375) + 1.0))


def test_fold_scalar_clamps_recharge_to_cap(tables: MathTables) -> None:
    """A recharge fold above the AT recharge cap is clamped to it."""
    divisor = fold_scalar(RECHARGE, f32(9.0), f32(0.0), ignore_enh=(), recharge_cap=5.0)
    assert divisor == 5.0


def test_fold_scalar_ignored_aspect_returns_one(tables: MathTables) -> None:
    """A power that ignores the aspect gets no fold (divisor 1.0)."""
    divisor = fold_scalar(RECHARGE, f32(9.0), f32(0.0), ignore_enh=("RechargeTime",), recharge_cap=5.0)
    assert divisor == 1.0


def test_fold_scalar_post_ed_extra_adds_after_ed(tables: MathTables) -> None:
    """The post-ED extra source (the incarnate IgnoreED=true seam) adds to the global term."""
    base = fold_scalar(END, f32(0.2), f32(0.0375), ignore_enh=(), recharge_cap=5.0)
    with_extra = fold_scalar(END, f32(0.2), f32(0.0375), ignore_enh=(), recharge_cap=5.0, post_ed_extra=f32(0.5))
    assert with_extra == f32(base + 0.5)
