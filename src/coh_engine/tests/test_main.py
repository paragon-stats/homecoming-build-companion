"""CLI entry point — registry introspection and the ``validate`` subcommand."""

import json
from pathlib import Path

import pytest

from coh_engine.__main__ import _emit, main, run_validate

FIXTURES = Path(__file__).parent / "fixtures"
BUILD_DIR = FIXTURES / "mids" / "builds" / "shield_scrapper_set_bonuses"


def test_rules_lists_all_three_registries(capsys: pytest.CaptureFixture[str]) -> None:
    """`rules` prints the legality rules, hard-limit rules, and scoring metrics."""
    assert main(["rules"]) == 0
    out = capsys.readouterr().out
    assert "legality rules" in out
    assert "_rule_placement" in out  # a registered legality rule
    assert "hard-limit rules" in out
    assert "_rule_total_added_slots" in out  # a registered hard-limit rule
    assert "enhancement aspect handlers" in out
    assert "RechargeTime" in out  # a registered aspect handler
    assert "scoring metrics" in out
    assert "perma_hasten" in out  # a registered metric


def test_no_command_prints_status(capsys: pytest.CaptureFixture[str]) -> None:
    """No subcommand prints the engine status line."""
    assert main([]) == 0
    assert "coh_engine: MidsReborn build-math port." in capsys.readouterr().out


def test_run_validate_returns_diagnostics() -> None:
    """run_validate loads the dumped build and folds both registries over it."""
    diags = run_validate(BUILD_DIR, FIXTURES / "mids")
    # The authored totals-fixture is not slot-schedule-legal, so it has findings.
    assert any(d.rule_id.startswith("H-") for d in diags)


def test_run_validate_arms_the_pure_proc_exemption() -> None:
    """The CLI must pass ``enhancement_effects.json`` into the legality registry.

    P-SET-001's pure-proc exemption is keyed on a piece having no ``Enhancement``-mode
    effects. When ``run_validate`` omits that argument the rule still runs, but degrades
    silently: it warns on every pure proc a real build slots alone. Comparing against a
    fully-armed direct call pins the wiring without depending on which procs a given
    fixture happens to contain.
    """
    from coh_engine.effect import load_powers_effects
    from coh_engine.enhancement import load_build_slots, load_enhancement_effects
    from coh_engine.legality import check_build_legality, load_enhancement_legality
    from coh_engine.set_bonuses import load_set_bonus_db

    mids = FIXTURES / "mids"
    powers = load_powers_effects(BUILD_DIR / "powers_effects.json")
    slots = dict(load_build_slots(BUILD_DIR / "slots.json"))
    armed = check_build_legality(
        powers,
        slots,
        load_enhancement_legality(mids / "enhancements.json"),
        load_set_bonus_db(mids / "enhancement_sets.json", mids / "set_bonus_powers.json"),
        dict(load_enhancement_effects(mids / "enhancement_effects.json")),
    )
    via_cli = run_validate(BUILD_DIR, mids)
    lone = "P-SET-001"
    assert sum(1 for d in via_cli if d.rule_id == lone) == sum(1 for d in armed if d.rule_id == lone)


def test_validate_subcommand_text(capsys: pytest.CaptureFixture[str]) -> None:
    """`validate <build>` runs both registries and exits 1 when findings exist (text)."""
    code = main(["validate", str(BUILD_DIR)])
    out = capsys.readouterr().out
    assert code == 1
    assert "H-SLOT" in out


def test_validate_subcommand_json(capsys: pytest.CaptureFixture[str]) -> None:
    """`validate --format json` emits a parseable diagnostics array."""
    code = main(["validate", str(BUILD_DIR), "--format", "json"])
    payload = json.loads(capsys.readouterr().out)
    assert code == 1
    assert isinstance(payload, list) and payload and "rule_id" in payload[0]


def test_emit_no_violations(capsys: pytest.CaptureFixture[str]) -> None:
    """An empty diagnostics list prints the clean-build message and exits 0 (text)."""
    assert _emit([], as_json=False) == 0
    assert "no violations" in capsys.readouterr().out


def test_emit_empty_json(capsys: pytest.CaptureFixture[str]) -> None:
    """An empty diagnostics list emits an empty JSON array and exits 0."""
    assert _emit([], as_json=True) == 0
    assert json.loads(capsys.readouterr().out) == []


def test_chain_subcommand_text_covers_every_stance(capsys: pytest.CaptureFixture[str]) -> None:
    """`chain` prints a block per stance and exits 0 when only info diagnostics are raised."""
    assert main(["chain", str(BUILD_DIR)]) == 0
    out = capsys.readouterr().out
    for stance in ("melee", "ranged", "hybrid"):
        assert f"== {stance} stance ==" in out


def test_chain_subcommand_json_single_stance(capsys: pytest.CaptureFixture[str]) -> None:
    """`chain --stance melee --format json --target spawn:3 --assume ...` emits one structured report."""
    argv = ["chain", str(BUILD_DIR), "--stance", "melee", "--format", "json", "--target", "spawn:3"]
    assert main([*argv, "--assume", "damage.enhancement_avg=0.5", "--move", "jump", "--standoff", "30"]) == 0
    (report,) = json.loads(capsys.readouterr().out)
    assert report["stance"] == "melee"
    assert report["dps"]["total"] > 0.0


@pytest.mark.parametrize(
    ("extra", "code"), [(["--assume", "oops"], "E67"), (["--target", "crowd"], "E68"), (["--move", "fly"], "E69")]
)
def test_chain_subcommand_reports_errors_on_stderr(
    capsys: pytest.CaptureFixture[str], extra: list[str], code: str
) -> None:
    """A ValueError carrying an E-code is printed to stderr and the exit code is 1."""
    assert main(["chain", str(BUILD_DIR), *extra]) == 1
    captured = capsys.readouterr()
    assert code in captured.err
    assert captured.out == ""


def test_chain_subcommand_exit_2_on_warning(
    capsys: pytest.CaptureFixture[str], monkeypatch: pytest.MonkeyPatch
) -> None:
    """A build whose pool empties before any chain's 4 cycles raises P-END-002 (warning) and exits 2."""
    from coh_engine import chain_report
    from coh_engine.damage_model import EnduranceBudget

    monkeypatch.setattr(chain_report, "build_endurance_budget", lambda *a, **k: EnduranceBudget(100.0, 0.0, 50.0))
    assert main(["chain", str(BUILD_DIR), "--stance", "melee"]) == 2
    assert "P-END-002" in capsys.readouterr().out
