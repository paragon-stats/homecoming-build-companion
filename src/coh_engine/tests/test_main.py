"""CLI entry point — registry introspection and the ``validate`` subcommand."""

import json
from pathlib import Path
from typing import Any

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


def _chain_json(capsys: pytest.CaptureFixture[str], *flags: str) -> dict[str, Any]:
    assert main(["chain", str(BUILD_DIR), "--stance", "melee", "--format", "json", *flags]) == 0
    (report,) = json.loads(capsys.readouterr().out)
    return dict(report)


def _crit_factor(report: dict[str, Any]) -> float:
    (crit,) = [m for m in report["modifiers"] if m["name"] == "scrapper_critical_hit"]
    return float(crit["factor"])


def test_chain_subcommand_json_single_stance(capsys: pytest.CaptureFixture[str]) -> None:
    """`chain --stance melee --format json` emits one structured report."""
    report = _chain_json(capsys, "--target", "spawn:3", "--move", "jump", "--standoff", "30")
    assert report["stance"] == "melee"
    assert report["dps"]["total"] > 0.0


def test_chain_assume_flag_reaches_the_model(capsys: pytest.CaptureFixture[str]) -> None:
    """--assume overrides an AT modifier: the fixture has no additive buff, so the Scrapper crit chance shows it.

    ``damage.enhancement_avg`` only divides additive buffs, and this dump has none, so it cannot show here.
    """
    low = _chain_json(capsys, "--assume", "Class_Scrapper.crit_chance=0")
    high = _chain_json(capsys, "--assume", "Class_Scrapper.crit_chance=0.5")
    assert _crit_factor(low) == 1.0
    assert _crit_factor(high) > 1.0
    assert high["dps"]["total"] > low["dps"]["total"]


def test_chain_target_flag_changes_an_area_chain(capsys: pytest.CaptureFixture[str]) -> None:
    """The fixture's chain has an area attack, so a spawn of five out-damages a single target."""
    single = _chain_json(capsys, "--target", "single")
    spawn = _chain_json(capsys, "--target", "spawn:5")
    assert spawn["dps"]["unscaled"]["chain"] > single["dps"]["unscaled"]["chain"]


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


def test_chain_error_raised_after_setup_is_reported_not_a_traceback(capsys: pytest.CaptureFixture[str]) -> None:
    """E50 fires while the model runs, after the setup is built: still an E-code on stderr and exit 1.

    The fixture has no target debuff, so E57 cannot fire here; the Scrapper crit chance range check does.
    """
    assert main(["chain", str(BUILD_DIR), "--assume", "Class_Scrapper.crit_chance=2"]) == 1
    captured = capsys.readouterr()
    assert "E50" in captured.err
    assert "Traceback" not in captured.err
    assert captured.out == ""


def test_chain_key_error_from_a_loader_is_e72(
    capsys: pytest.CaptureFixture[str], monkeypatch: pytest.MonkeyPatch
) -> None:
    """A dump missing an expected key surfaces as E72 with the key named, not a traceback."""

    def broken(*args: object, **kwargs: object) -> None:
        raise KeyError("Class")

    monkeypatch.setattr("coh_engine.__main__.build_chain_setup", broken)
    assert main(["chain", str(BUILD_DIR)]) == 1
    captured = capsys.readouterr()
    assert captured.err.startswith("error: E72: ")
    assert "'Class'" in captured.err
    assert "Traceback" not in captured.err
    assert captured.out == ""


def test_validate_os_error_from_a_loader_is_e72(
    capsys: pytest.CaptureFixture[str], monkeypatch: pytest.MonkeyPatch
) -> None:
    """An unreadable input file in `validate` names the file and exits 1."""

    def unreadable(*args: object, **kwargs: object) -> None:
        raise OSError(2, "No such file or directory", "slots.json")

    monkeypatch.setattr("coh_engine.__main__.run_validate", unreadable)
    assert main(["validate", str(BUILD_DIR)]) == 1
    captured = capsys.readouterr()
    assert captured.err.startswith("error: E72: ")
    assert "slots.json" in captured.err
    assert captured.out == ""


def test_validate_missing_dump_files_are_e72_not_a_traceback(
    capsys: pytest.CaptureFixture[str], tmp_path: Path
) -> None:
    """A dump directory without the expected files exits 1, naming the missing file."""
    assert main(["validate", str(tmp_path)]) == 1
    captured = capsys.readouterr()
    assert captured.err.startswith("error: E72: ")
    assert "powers_effects.json" in captured.err
    assert "Traceback" not in captured.err


def test_validate_value_error_is_reported_on_stderr(
    capsys: pytest.CaptureFixture[str], monkeypatch: pytest.MonkeyPatch
) -> None:
    """A loader's E-coded ValueError in `validate` prints on stderr and exits 1."""

    def rejected(*args: object, **kwargs: object) -> None:
        raise ValueError("E99: rejected input")

    monkeypatch.setattr("coh_engine.__main__.run_validate", rejected)
    assert main(["validate", str(BUILD_DIR)]) == 1
    assert capsys.readouterr().err == "error: E99: rejected input\n"
