"""Tests for the release bump-policy drift check."""

from __future__ import annotations

from pathlib import Path

import pytest

from scripts.testing.hooks import check_commit_message
from scripts.testing.hooks.check_release_policy import load_fact_set, main, policy_errors

FACT_SET = """# type      bump
feat        minor
fix         patch
perf        patch
security    patch
revert      patch
docs        none
chore       none
ci          none
refactor    none
test        none
style       none
build       none
"""

PYPROJECT = """[tool.semantic_release.commit_parser_options]
allowed_tags = ["feat", "fix", "perf", "security", "revert", "docs", "test", "ci", "chore", "style", "refactor", "build"]
minor_tags = ["feat"]
patch_tags = ["fix", "perf", "security", "revert"]
"""


def _write(tmp_path: Path, facts: str = FACT_SET, pyproject: str = PYPROJECT) -> tuple[Path, Path]:
    tmp_path.mkdir(exist_ok=True)
    f, p = tmp_path / "commit-types.txt", tmp_path / "pyproject.toml"
    f.write_text(facts, encoding="utf-8")
    p.write_text(pyproject, encoding="utf-8")
    return f, p


def test_load_fact_set_skips_comments_and_blank_lines(tmp_path: Path) -> None:
    f, _ = _write(tmp_path, facts="# header\n\nfeat minor\nfix patch  # trailing\n")
    assert load_fact_set(f) == {"feat": "minor", "fix": "patch"}


def test_load_fact_set_rejects_a_row_without_a_bump(tmp_path: Path) -> None:
    f, _ = _write(tmp_path, facts="feat minor\nrevert\n")
    with pytest.raises(ValueError, match="revert"):
        load_fact_set(f)


def test_load_fact_set_rejects_an_unknown_bump(tmp_path: Path) -> None:
    """A fact-set fault is reported as such, not as pyproject drift (which would point at the wrong file)."""
    f, _ = _write(tmp_path, facts="feat majr\n")
    with pytest.raises(ValueError, match="majr"):
        load_fact_set(f)


def test_load_fact_set_rejects_a_repeated_type(tmp_path: Path) -> None:
    f, _ = _write(tmp_path, facts="feat minor\nfeat patch\n")
    with pytest.raises(ValueError, match="feat"):
        load_fact_set(f)


def test_pyproject_missing_the_section_or_a_key_is_e81(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    f, p = _write(tmp_path / "nosection", pyproject="[tool.other]\nx = 1\n")
    assert main([str(f), str(p)]) == 2
    assert "E81" in capsys.readouterr().err
    f2, p2 = _write(
        tmp_path / "nokey", pyproject=PYPROJECT.replace('patch_tags = ["fix", "perf", "security", "revert"]\n', "")
    )
    assert main([str(f2), str(p2)]) == 2
    err = capsys.readouterr().err
    assert "E81" in err and "patch_tags" in err


def test_matching_policy_has_no_errors(tmp_path: Path) -> None:
    f, p = _write(tmp_path)
    assert policy_errors(f, p) == []


def test_each_drift_is_named(tmp_path: Path) -> None:
    drifted = PYPROJECT.replace('"security", ', "").replace('minor_tags = ["feat"]', 'minor_tags = ["feat", "perf"]')
    f, p = _write(tmp_path, pyproject=drifted)
    errors = policy_errors(f, p)
    assert len(errors) == 3  # allowed_tags lost security; minor gained perf; patch lost security
    assert any("allowed_tags" in e and "security" in e for e in errors)
    assert any("minor_tags" in e and "perf" in e for e in errors)
    assert any("patch_tags" in e and "security" in e for e in errors)


def test_the_local_hook_allowlist_is_checked_too(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    f, p = _write(tmp_path)
    monkeypatch.setattr(check_commit_message, "_ALLOWED_TYPES", ("feat", "fix"))
    errors = policy_errors(f, p)
    assert len(errors) == 1
    assert "check_commit_message" in errors[0]


def test_main_exit_codes_and_output(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    f, p = _write(tmp_path)
    assert main([str(f), str(p)]) == 0
    assert "matches" in capsys.readouterr().out
    f2, p2 = _write(tmp_path / "bad", pyproject=PYPROJECT.replace('"build"', '"bld"'))
    assert main([str(f2), str(p2)]) == 1
    err = capsys.readouterr().err
    assert "H-REL-001" in err and "allowed_tags" in err


def test_main_reports_a_bad_fact_set_as_e_code(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    f, p = _write(tmp_path, facts="feat\n")
    assert main([str(f), str(p)]) == 2
    assert "E80" in capsys.readouterr().err


def test_main_requires_two_paths() -> None:
    with pytest.raises(SystemExit):
        main([])
