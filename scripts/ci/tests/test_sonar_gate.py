"""Tests for scripts.ci.sonar_gate."""

from __future__ import annotations

import io
import json
import subprocess
from pathlib import Path
from typing import Any

import pytest

from scripts.ci import sonar_gate
from scripts.ci.sonar_gate import (
    Entry,
    Finding,
    RuleFileError,
    ScannerOutputError,
    fetch_issues,
    main,
    parse_diff,
    parse_rules,
    read_sarif,
)

ROOT = "D:/a/repo/repo"
RULE = "csharpsquid:S3776"
OTHER = "python:S1192"
DIFF = """\
diff --git a/src/A.cs b/src/A.cs
index 1..2 100644
--- a/src/A.cs
+++ b/src/A.cs
@@ -10,3 +10,2 @@ class A
--- removed line that looks like a header
@@ -20 +19 @@
@@ -30,0 +30,4 @@
diff --git a/src/New.cs b/src/New.cs
new file mode 100644
--- /dev/null
+++ b/src/New.cs
@@ -0,0 +1,5 @@
diff --git a/src/Gone.cs b/src/Gone.cs
deleted file mode 100644
--- a/src/Gone.cs
+++ /dev/null
@@ -1,2 +0,0 @@
"""


def _v1(rule: str, path: str, line: int = 1) -> dict[str, Any]:
    uri = f"file:///D:/a/repo/repo/{path}"
    return {"ruleId": rule, "locations": [{"resultFile": {"uri": uri, "region": {"startLine": line}}}]}


def _v2(rule: str, uri: str, line: int = 1) -> dict[str, Any]:
    place = {"artifactLocation": {"uri": uri}, "region": {"startLine": line}}
    return {"ruleId": rule, "locations": [{"physicalLocation": place}]}


def _issues(directory: Path, name: str, results: list[dict[str, Any]], *, runs: bool = True) -> None:
    target = directory / name
    target.mkdir(parents=True)
    document = {"runs": [{"results": results}]} if runs else {"runs": []}
    (target / "ProjectInfo.xml").write_text(
        "<ProjectInfo><IsExcluded>false</IsExcluded></ProjectInfo>", encoding="utf-8"
    )
    (target / "Issues.json").write_text(json.dumps(document), encoding="utf-8-sig")


def _args(tmp_path: Path, rules: str, *extra: str) -> list[str]:
    rules_dir = tmp_path / "rules"
    rules_dir.mkdir(exist_ok=True)
    (rules_dir / "a.txt").write_text(rules, encoding="utf-8")
    return ["--rules-dir", str(rules_dir), "--sarif-root", str(tmp_path / "out"), "--repo-root", ROOT, *extra]


def _api(main_issues: list[dict[str, Any]], pr_issues: list[dict[str, Any]] | None = None, page_size: int = 500) -> Any:
    calls: list[str] = []

    def fetch(url: str) -> dict[str, Any]:
        calls.append(url)
        issues = pr_issues if "pullRequest=" in url and pr_issues is not None else main_issues
        page = int(url.split("&p=")[1].split("&")[0])
        return {"total": len(issues), "issues": issues[(page - 1) * page_size : page * page_size]}

    fetch.calls = calls  # type: ignore[attr-defined]
    return fetch


def _issue(rule: str, path: str, line: int = 0) -> dict[str, Any]:
    issue: dict[str, Any] = {"rule": rule, "component": f"{sonar_gate.PROJECT_KEY}:{path}"}
    if line:
        issue["line"] = line
    return issue


def _no_diff(_base: str) -> str:
    raise AssertionError("git diff must not run")


# --- rules parser ---


def test_rules_parser_reads_entries_and_skips_comments_and_blanks(tmp_path: Path) -> None:
    (tmp_path / "b.txt").write_text(
        "# note\n\ncsharpsquid:S1 src/A/  # trailing\n  python:S2   scripts/x.py\n", encoding="utf-8"
    )
    (tmp_path / "ignored.md").write_text("not a rule\n", encoding="utf-8")

    assert parse_rules(tmp_path) == [Entry("csharpsquid:S1", "src/A/"), Entry("python:S2", "scripts/x.py")]


@pytest.mark.parametrize("line", ["csharpsquid:S1", "csharpsquid:S1 a b", "S1 src/"])
def test_rules_parser_names_the_file_and_line_of_a_malformed_entry(tmp_path: Path, line: str) -> None:
    (tmp_path / "bad.txt").write_text(f"# ok\n{line}\n", encoding="utf-8")

    with pytest.raises(RuleFileError, match=r"bad\.txt:2"):
        parse_rules(tmp_path)


# --- scanner output ---


def test_sarif_1_and_2_results_map_to_repo_relative_sonar_rules(tmp_path: Path) -> None:
    _issues(
        tmp_path,
        "1",
        [
            _v1("S3776", "src/A.cs", 7),
            _v1("S3776", "src/A.cs", 7),
            _v1("MA0003", "src/A.cs"),
            {"ruleId": "S107"},
        ],
    )
    _issues(tmp_path, "2", [_v2("S1192", "src/B%20x.cs", 3), _v2("S1192", "file:///E:/elsewhere/C.cs")])

    scan = read_sarif(tmp_path, ROOT)

    assert (scan.files, scan.projects, scan.total) == (2, 2, 6)
    assert scan.findings == {
        Finding(RULE, "src/A.cs", 7),
        Finding("csharpsquid:S107", "", 0),
        Finding("csharpsquid:S1192", "src/B x.cs", 3),
        Finding("csharpsquid:S1192", "E:/elsewhere/C.cs", 1),
    }


def test_sarif_root_without_issues_json_fails(tmp_path: Path) -> None:
    with pytest.raises(ScannerOutputError, match="missing"):
        read_sarif(tmp_path, ROOT)


@pytest.mark.parametrize("content", ["", "{", "[]", '{"runs": []}'])
def test_a_malformed_issues_json_fails(tmp_path: Path, content: str) -> None:
    _issues(tmp_path, "1", [])
    (tmp_path / "1" / "Issues.json").write_text(content, encoding="utf-8")

    with pytest.raises(ScannerOutputError):
        read_sarif(tmp_path, ROOT)


def test_every_expected_file_present_with_no_results_passes(tmp_path: Path) -> None:
    _issues(tmp_path, "1", [])
    _issues(tmp_path, "2", [])

    scan = read_sarif(tmp_path, ROOT)

    assert (scan.files, scan.projects, scan.total, scan.findings) == (2, 2, 0, set())


def test_a_project_without_its_issues_json_fails(tmp_path: Path) -> None:
    _issues(tmp_path, "1", [_v1("S1", "src/A.cs")])
    (tmp_path / "2").mkdir()
    (tmp_path / "2" / "ProjectInfo.xml").write_text("<ProjectInfo/>", encoding="utf-8")

    with pytest.raises(ScannerOutputError, match=r"missing for 1 of 2 projects: \['2'\]"):
        read_sarif(tmp_path, ROOT)


def test_an_excluded_project_needs_no_issues_json(tmp_path: Path) -> None:
    _issues(tmp_path, "1", [])
    (tmp_path / "2").mkdir()
    (tmp_path / "2" / "ProjectInfo.xml").write_text("<IsExcluded>true</IsExcluded>", encoding="utf-8")

    assert read_sarif(tmp_path, ROOT).projects == 1


def test_issues_json_without_any_project_info_fails(tmp_path: Path) -> None:
    (tmp_path / "1").mkdir()
    (tmp_path / "1" / "Issues.json").write_text("{}", encoding="utf-8")

    with pytest.raises(ScannerOutputError, match="ProjectInfo"):
        read_sarif(tmp_path, ROOT)


def test_an_issues_json_that_cannot_be_read_fails(tmp_path: Path) -> None:
    _issues(tmp_path, "1", [])
    (tmp_path / "1" / "sub" / "Issues.json").mkdir(parents=True)

    with pytest.raises(ScannerOutputError, match="unreadable"):
        read_sarif(tmp_path, ROOT)


# --- diff parser ---


def test_diff_parser_reads_multi_line_single_line_deletion_added_and_deleted_forms() -> None:
    diff = parse_diff(DIFF)

    assert diff.files == {"src/A.cs", "src/New.cs", "src/Gone.cs"}
    assert diff.old_lines["src/A.cs"] == {10, 11, 12, 20}
    assert diff.old_lines["src/Gone.cs"] == {1, 2}
    assert "src/New.cs" not in diff.old_lines
    assert 30 not in diff.old_lines["src/A.cs"]


# --- API paging ---


def test_fetch_issues_pages_until_every_issue_is_read() -> None:
    issues = [_issue("python:S1", f"f{n}.py", n + 1) for n in range(5)]
    fetch = _api(issues, page_size=2)

    found = fetch_issues(fetch, ["python:S1", "python:S2"], {"branch": "main"})

    assert found == [Finding("python:S1", f"f{n}.py", n + 1) for n in range(5)]
    assert len(fetch.calls) == 3
    assert "rules=python%3AS1%2Cpython%3AS2" in fetch.calls[0]
    assert "branch=main" in fetch.calls[0]


def test_fetch_issues_stops_on_an_empty_page_even_if_total_is_larger() -> None:
    def fetch(_url: str) -> dict[str, Any]:
        return {"total": 9, "issues": []}

    assert fetch_issues(fetch, ["python:S1"], {}) == []


# --- the gate ---


def test_no_entries_passes_after_reading_the_scanner_output(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    _issues(tmp_path / "out", "1", [_v1("S1", "src/A.cs")])

    assert main(_args(tmp_path, "# nothing\n"), fetch=_api([]), git_diff=_no_diff) == 0
    assert "read 1 Issues.json files for 1 expected projects: 1 results, 1 csharpsquid" in capsys.readouterr().out


def test_a_malformed_rules_file_exits_2(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert main(_args(tmp_path, "oops\n"), fetch=_api([]), git_diff=_no_diff) == 2
    assert "a.txt:1" in capsys.readouterr().err


def test_missing_scanner_output_fails_closed(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert main(_args(tmp_path, "# nothing\n"), fetch=_api([]), git_diff=_no_diff) == 1
    assert "FAIL scanner output missing" in capsys.readouterr().out


def test_a_gated_c_sharp_result_fails_and_is_listed(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    _issues(tmp_path / "out", "1", [_v1("S3776", "src/A/x.cs", 9), _v1("S3776", "src/B/y.cs", 2)])

    assert main(_args(tmp_path, f"{RULE} src/A/\n"), fetch=_api([]), git_diff=_no_diff) == 1

    out = capsys.readouterr().out
    assert "FAIL scanner: csharpsquid:S3776 src/A/x.cs:9" in out
    assert "y.cs" not in out
    assert f"{RULE} src/A/ scanner=1 main=0" in out


def test_a_clean_c_sharp_rule_passes_on_main(tmp_path: Path) -> None:
    _issues(tmp_path / "out", "1", [_v1("S3776", "src/B/y.cs")])

    assert main(_args(tmp_path, f"{RULE} src/A/\n"), fetch=_api([]), git_diff=_no_diff) == 0


def test_main_findings_under_a_prefix_fail_a_push_to_main(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    _issues(tmp_path / "out", "1", [_v1("S1", "src/A.cs")])
    api = _api([_issue(OTHER, "scripts/a.py", 4), _issue(OTHER, "scripts/other/b.py", 1)])

    assert main(_args(tmp_path, f"{OTHER} scripts/a.py\n"), fetch=api, git_diff=_no_diff) == 1
    assert "FAIL main: python:S1192 scripts/a.py:4" in capsys.readouterr().out


def test_pull_request_passes_when_findings_are_on_changed_lines_and_the_analysis_is_clean(tmp_path: Path) -> None:
    _issues(tmp_path / "out", "1", [_v1("S1", "src/A.cs")])
    api = _api(
        [
            _issue(OTHER, "src/A.cs", 20),
            _issue(OTHER, "src/A.cs", 11),
            _issue("python:S9", "src/Gone.cs"),
            _issue(OTHER, "x", 5),
        ],
        [],
    )
    rules = f"{OTHER} src/A.cs\npython:S9 src/Gone.cs\n"

    assert main(_args(tmp_path, rules, "--pull-request", "7"), fetch=api, git_diff=lambda _b: DIFF) == 0
    assert any("pullRequest=7" in url for url in api.calls)


def test_pull_request_fails_on_untouched_findings_and_on_pr_analysis_findings(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    _issues(tmp_path / "out", "1", [_v1("S1", "src/A.cs")])
    main_issues = [
        _issue(OTHER, "src/A.cs", 99),
        _issue(OTHER, "src/Untouched.cs", 1),
        _issue("python:S3776", "src/A.cs", 99),
        _issue("python:S3776", "src/Untouched.cs", 1),
        _issue(OTHER, "src/A.cs"),
        _issue(OTHER, "src/Untouched.cs"),
        _issue(RULE, "src/A.cs", 99),
        _issue(RULE, "src/Untouched.cs", 1),
    ]
    api = _api(main_issues, [_issue(OTHER, "src/A.cs", 5)])
    rules = f"{OTHER} src/\npython:S3776 src/\n{RULE} src/\n"

    assert main(_args(tmp_path, rules, "--pull-request", "7"), fetch=api, git_diff=lambda _b: DIFF) == 1

    out = capsys.readouterr().out
    assert "FAIL baseline: python:S1192 src/A.cs:99" in out
    assert "FAIL baseline: python:S1192 src/Untouched.cs:1" in out
    assert "FAIL baseline: python:S1192 src/Untouched.cs:0" in out
    assert "FAIL baseline: python:S3776 src/Untouched.cs:1" in out
    assert "FAIL baseline: csharpsquid:S3776 src/Untouched.cs:1" in out
    assert "FAIL pull-request: python:S1192 src/A.cs:5" in out
    assert "baseline: python:S3776 src/A.cs" not in out
    assert "baseline: csharpsquid:S3776 src/A.cs" not in out
    assert "FAIL baseline: python:S1192 src/A.cs:0" not in out


def test_the_default_git_diff_runs_three_dot_diff_against_the_base(monkeypatch: pytest.MonkeyPatch) -> None:
    seen: list[list[str]] = []

    def fake_run(command: list[str], **_kwargs: Any) -> subprocess.CompletedProcess[str]:
        seen.append(command)
        return subprocess.CompletedProcess(command, 0, stdout="diff text")

    monkeypatch.setattr(subprocess, "run", fake_run)

    assert sonar_gate._git_diff("origin/main") == "diff text"
    assert seen == [["git", "diff", "-U0", "--no-renames", "origin/main...HEAD"]]


def test_the_default_fetch_reads_json_from_the_url(monkeypatch: pytest.MonkeyPatch) -> None:
    urls: list[str] = []

    def fake_urlopen(url: str, timeout: int) -> io.BytesIO:
        urls.append(url)
        return io.BytesIO(b'{"issues": []}')

    monkeypatch.setattr(sonar_gate.urllib.request, "urlopen", fake_urlopen)

    assert sonar_gate._http_json("https://example.test/x") == {"issues": []}
    assert urls == ["https://example.test/x"]


def test_a_base_that_could_be_an_option_is_refused(tmp_path: Path) -> None:
    with pytest.raises(SystemExit):
        main(_args(tmp_path, "# x\n", "--base", "--output=x"))
