"""Tests for scripts.dev.bootstrap_venv."""

from __future__ import annotations

import subprocess
from pathlib import Path
from typing import Any
from unittest.mock import patch

import pytest

from scripts.dev.bootstrap_venv import (
    EXIT_FAILED,
    EXIT_OK,
    LOCAL_GIT_SETTINGS,
    _check_pip_version,
    _configure_local_git,
    _create_venv,
    _describe_local_bootstrap,
    _ensure_venv,
    _extract_requirement_name,
    _format_git_signing_status,
    _format_health_summary,
    _format_version_table,
    _hash_file,
    _is_executable_path,
    _load_dev_requirements_with_specifiers,
    _load_requires_python,
    _normalize_package_name,
    _parse_args,
    _print_dry_run,
    _query_installed_versions,
    _query_python_version,
    _report_git_signing,
    _resolve_venv_python,
    _run_bootstrap_actions,
    _run_local_bootstrap,
    _run_step,
    _state_file_path,
    _venv_python_candidates,
    _verify_bootstrap,
    build_desired_state,
    build_plan,
    collect_state,
    describe_plan,
    load_bootstrap_state,
    main,
    repo_root_from_script,
    resolve_system_python_cmd,
    run_bootstrap,
    write_bootstrap_state,
)


class TestRepoRootFromScript:
    """Test Repo Root From Script."""

    def test_finds_parents_2(self, tmp_path: Path) -> None:
        """Test finds parents 2."""
        script = tmp_path / "a" / "b" / "c.py"
        script.parent.mkdir(parents=True)
        script.touch()
        assert repo_root_from_script(script) == tmp_path


class TestVenvPythonCandidates:
    """Test Venv Python Candidates."""

    def test_returns_two_candidates(self, tmp_path: Path) -> None:
        """Test returns two candidates."""
        result = _venv_python_candidates(tmp_path)
        assert len(result) == 2
        assert "bin" in str(result[0])
        assert "Scripts" in str(result[1])


class TestIsExecutablePath:
    """Test Is Executable Path."""

    def test_win32_only_checks_exists(self, tmp_path: Path) -> None:
        """Test win32 only checks exists."""
        fpath = tmp_path / "python.exe"
        fpath.touch()
        assert _is_executable_path(fpath, "win32") is True

    def test_missing_file_returns_false(self, tmp_path: Path) -> None:
        """Test missing file returns false."""
        assert _is_executable_path(tmp_path / "nope", "win32") is False

    def test_linux_checks_executable(self, tmp_path: Path) -> None:
        """Test linux checks executable."""
        fpath = tmp_path / "python"
        fpath.touch()
        fpath.chmod(0o755)
        assert _is_executable_path(fpath, "linux") is True

    def test_linux_non_executable_returns_false(self, tmp_path: Path) -> None:
        """Non-executable file returns False on Linux."""
        fpath = tmp_path / "python"
        fpath.touch()
        fpath.chmod(0o644)
        with patch("scripts.dev.bootstrap_venv.os.access", return_value=False):
            assert _is_executable_path(fpath, "linux") is False


class TestResolveVenvPython:
    """Test Resolve Venv Python."""

    def test_finds_scripts_python_on_win32(self, tmp_path: Path) -> None:
        """Test finds scripts python on win32."""
        scripts_dir = tmp_path / ".venv" / "Scripts"
        scripts_dir.mkdir(parents=True)
        python_exe = scripts_dir / "python.exe"
        python_exe.touch()
        result = _resolve_venv_python(tmp_path, "win32")
        assert result == python_exe

    def test_returns_none_when_no_venv(self, tmp_path: Path) -> None:
        """Test returns none when no venv."""
        assert _resolve_venv_python(tmp_path, "win32") is None


class TestResolveSystemPythonCmd:
    """Test Resolve System Python Cmd."""

    def test_linux_finds_python3(self) -> None:
        """Test linux finds python3."""
        result = resolve_system_python_cmd("linux", which=lambda cmd: "/usr/bin/python3" if cmd == "python3" else None)
        assert result == ["python3"]

    def test_linux_falls_back_to_python(self) -> None:
        """Test linux falls back to python."""
        result = resolve_system_python_cmd("linux", which=lambda cmd: "/usr/bin/python" if cmd == "python" else None)
        assert result == ["python"]

    def test_win32_finds_python(self) -> None:
        """Test win32 finds python."""
        result = resolve_system_python_cmd("win32", which=lambda cmd: "C:\\python.exe" if cmd == "python" else None)
        assert result == ["python"]

    def test_win32_falls_back_to_py(self) -> None:
        """Test win32 falls back to py."""
        result = resolve_system_python_cmd("win32", which=lambda cmd: "C:\\py.exe" if cmd == "py" else None)
        assert result == ["py", "-3"]

    def test_returns_none_when_no_python(self) -> None:
        """Test returns none when no python."""
        result = resolve_system_python_cmd("linux", which=lambda _: None)
        assert result is None


class TestStateFilePath:
    """Test State File Path."""

    def test_returns_expected_path(self, tmp_path: Path) -> None:
        """Test returns expected path."""
        result = _state_file_path(tmp_path)
        assert result == tmp_path / ".venv" / ".bootstrap_state.json"


class TestHashFile:
    """Test Hash File."""

    def test_hashes_existing_file(self, tmp_path: Path) -> None:
        """Test hashes existing file."""
        fpath = tmp_path / "test.txt"
        fpath.write_text("hello")
        result = _hash_file(fpath)
        assert result is not None
        assert len(result) == 64

    def test_returns_none_for_missing_file(self, tmp_path: Path) -> None:
        """Test returns none for missing file."""
        assert _hash_file(tmp_path / "missing") is None


class TestNormalizePackageName:
    """Test Normalize Package Name."""

    def test_normalizes_dashes_dots_underscores(self) -> None:
        """Test normalizes dashes dots underscores."""
        assert _normalize_package_name("My_Package.Name") == "my-package-name"


class TestExtractRequirementName:
    """Test Extract Requirement Name."""

    def test_simple_name(self) -> None:
        """Test simple name."""
        assert _extract_requirement_name("pytest>=8.0") == "pytest"

    def test_with_extras(self) -> None:
        """Test with extras."""
        assert _extract_requirement_name("package[extra]>=1.0") == "package"

    def test_with_marker(self) -> None:
        """Test with marker."""
        assert _extract_requirement_name("pkg>=1.0; python_version>='3.8'") == "pkg"

    def test_with_url(self) -> None:
        """Test with url."""
        assert _extract_requirement_name("pkg @ https://example.com") == "pkg"

    def test_empty_string(self) -> None:
        """Test empty string."""
        assert _extract_requirement_name("") is None

    def test_no_match(self) -> None:
        """Test no match."""
        assert _extract_requirement_name("!!!") is None


class TestLoadDevRequirements:
    """Dev-dep loading edge cases, exercised through _load_dev_requirements_with_specifiers.

    (The plain _load_dev_requirements helper was removed when _verify_bootstrap
    consolidated onto the specifier-preserving loader.)
    """

    def test_loads_from_pyproject(self, tmp_path: Path) -> None:
        """Test loads from pyproject."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text(
            '[project.optional-dependencies]\ndev = ["pytest>=8.0", "ruff>=0.8"]\n',
            encoding="utf-8",
        )
        names = [name for name, _ in _load_dev_requirements_with_specifiers(tmp_path)]
        assert "pytest" in names
        assert "ruff" in names

    def test_returns_empty_when_no_project_key(self, tmp_path: Path) -> None:
        """Test returns empty when no project key."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text("[tool.ruff]\n", encoding="utf-8")
        assert _load_dev_requirements_with_specifiers(tmp_path) == []

    def test_returns_empty_when_no_optional_deps(self, tmp_path: Path) -> None:
        """Test returns empty when no optional deps."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project]\nname = "foo"\n', encoding="utf-8")
        assert _load_dev_requirements_with_specifiers(tmp_path) == []

    def test_returns_empty_when_no_dev_key(self, tmp_path: Path) -> None:
        """Test returns empty when no dev key."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project.optional-dependencies]\ntest = ["pytest"]\n', encoding="utf-8")
        assert _load_dev_requirements_with_specifiers(tmp_path) == []

    def test_skips_non_string_entries(self, tmp_path: Path) -> None:
        """Test skips non string entries."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text(
            '[project.optional-dependencies]\ndev = ["pytest>=8.0"]\n',
            encoding="utf-8",
        )
        fake_data = {
            "project": {
                "optional-dependencies": {
                    "dev": ["pytest>=8.0", 42, None],
                },
            },
        }
        with patch("scripts.dev.bootstrap_venv.tomllib.loads", return_value=fake_data):
            result = _load_dev_requirements_with_specifiers(tmp_path)
        assert result == [("pytest", ">=8.0")]


class TestBuildDesiredState:
    """Test Build Desired State."""

    def test_includes_hashes(self, tmp_path: Path) -> None:
        """Test includes hashes."""
        (tmp_path / "pyproject.toml").write_text("content")
        (tmp_path / ".pre-commit-config.yaml").write_text("hooks")
        result = build_desired_state(tmp_path)
        assert result["pyproject_hash"] is not None
        assert result["pre_commit_config_hash"] is not None

    def test_missing_files_give_none(self, tmp_path: Path) -> None:
        """Test missing files give none."""
        result = build_desired_state(tmp_path)
        assert result["pyproject_hash"] is None
        assert result["pre_commit_config_hash"] is None


class TestLoadWriteBootstrapState:
    """Test Load Write Bootstrap State."""

    def test_roundtrip(self, tmp_path: Path) -> None:
        """Test roundtrip."""
        state_path = tmp_path / ".bootstrap_state.json"
        desired: dict[str, str | None] = {"pyproject_hash": "abc123", "pre_commit_config_hash": "def456"}
        write_bootstrap_state(state_path, desired)
        loaded = load_bootstrap_state(state_path)
        assert loaded == desired

    def test_returns_none_for_missing_file(self, tmp_path: Path) -> None:
        """Test returns none for missing file."""
        assert load_bootstrap_state(tmp_path / "missing.json") is None

    def test_returns_none_for_invalid_json(self, tmp_path: Path) -> None:
        """Test returns none for invalid json."""
        fpath = tmp_path / "bad.json"
        fpath.write_text("not json")
        assert load_bootstrap_state(fpath) is None

    def test_returns_none_for_non_dict_json(self, tmp_path: Path) -> None:
        """Test returns none for non dict json."""
        fpath = tmp_path / "list.json"
        fpath.write_text("[1, 2, 3]")
        assert load_bootstrap_state(fpath) is None

    def test_write_creates_parent_dirs(self, tmp_path: Path) -> None:
        """Test write creates parent dirs."""
        state_path = tmp_path / "sub" / "dir" / "state.json"
        write_bootstrap_state(state_path, {"pyproject_hash": None, "pre_commit_config_hash": None})
        assert state_path.exists()


class TestCollectState:
    """Test Collect State."""

    def test_returns_expected_keys(self, tmp_path: Path) -> None:
        """Test returns expected keys."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        state = collect_state(repo_root=tmp_path, platform="win32", which=lambda _: None)
        assert "platform" in state
        assert "repo_root" in state
        assert "venv_exists" in state
        assert "state_matches" in state


class TestBuildPlan:
    """Test Build Plan."""

    def test_missing_pyproject_fails(self) -> None:
        """Test missing pyproject fails."""
        state: dict[str, Any] = {"pyproject_present": False}
        plan = build_plan(state)
        assert plan == ["fail_missing_pyproject"]

    def test_fresh_install_creates_venv_and_installs(self) -> None:
        """Test fresh install creates venv and installs."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": None,
            "pyproject_matches": False,
            "pre_commit_hook_exists": False,
            "pre_commit_matches": False,
        }
        plan = build_plan(state)
        assert "create_venv" in plan
        assert "upgrade_pip" in plan
        assert "install_dev_extras" in plan
        assert "install_pre_commit_hooks" in plan

    def test_up_to_date_returns_empty(self) -> None:
        """Test up to date returns empty."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": "/path/to/python",
            "pyproject_matches": True,
            "pre_commit_hook_exists": True,
            "pre_commit_matches": True,
        }
        assert build_plan(state) == []

    def test_pyproject_changed_upgrades_without_create(self) -> None:
        """Test pyproject changed upgrades without create."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": "/path/to/python",
            "pyproject_matches": False,
            "pre_commit_hook_exists": True,
            "pre_commit_matches": True,
        }
        plan = build_plan(state)
        assert "create_venv" not in plan
        assert "upgrade_pip" in plan
        assert "install_dev_extras" in plan

    def test_missing_hook_installs_hooks(self) -> None:
        """Test missing hook installs hooks."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": "/path/to/python",
            "pyproject_matches": True,
            "pre_commit_hook_exists": False,
            "pre_commit_matches": True,
        }
        plan = build_plan(state)
        assert "install_pre_commit_hooks" in plan
        assert "create_venv" not in plan

    def test_pre_commit_config_changed_installs_hooks(self) -> None:
        """Test pre commit config changed installs hooks."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": "/path/to/python",
            "pyproject_matches": True,
            "pre_commit_hook_exists": True,
            "pre_commit_matches": False,
        }
        plan = build_plan(state)
        assert "install_pre_commit_hooks" in plan


class TestDescribePlan:
    """Test Describe Plan."""

    def test_missing_pyproject(self) -> None:
        """Test missing pyproject."""
        state: dict[str, Any] = {"pyproject_present": False}
        lines = describe_plan(state)
        assert any("fail_missing_pyproject" in line for line in lines)

    def test_fresh_install(self) -> None:
        """Test fresh install."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": None,
            "venv_exists": False,
            "pyproject_matches": False,
            "pre_commit_hook_exists": False,
            "pre_commit_matches": False,
        }
        lines = describe_plan(state)
        assert any("create_venv: would create" in line for line in lines)
        assert any("upgrade_pip: would run" in line for line in lines)

    def test_up_to_date(self) -> None:
        """Test up to date."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": "/python",
            "venv_exists": True,
            "pyproject_matches": True,
            "pre_commit_hook_exists": True,
            "pre_commit_matches": True,
        }
        lines = describe_plan(state)
        assert any("no-op" in line for line in lines)

    def test_venv_exists_shows_no_op(self) -> None:
        """Test venv exists shows no op."""
        state: dict[str, Any] = {
            "pyproject_present": True,
            "venv_python": "/python",
            "venv_exists": True,
            "pyproject_matches": True,
            "pre_commit_hook_exists": True,
            "pre_commit_matches": True,
        }
        lines = describe_plan(state)
        assert any("create_venv: no-op" in line for line in lines)


class TestFormatGitSigningStatus:
    """Test Format Git Signing Status."""

    def test_configured(self) -> None:
        """Test configured."""
        lines = _format_git_signing_status(True, "SSH key found")
        assert any("configured" in line for line in lines)

    def test_not_configured(self) -> None:
        """Test not configured."""
        lines = _format_git_signing_status(False, "No key")
        assert any("not_configured" in line for line in lines)


class TestPrintDryRun:
    """Test Print Dry Run."""

    def test_prints_state(self, capsys: pytest.CaptureFixture[str]) -> None:
        """Test prints state."""
        state: dict[str, Any] = {
            "repo_root": "/home/user/repos/myrepo",
            "platform": "linux",
            "system_python": "python3",
            "venv_path": "/home/user/repos/myrepo/.venv",
            "venv_python": None,
            "pyproject_present": True,
            "venv_exists": False,
            "pyproject_matches": False,
            "pre_commit_hook_exists": False,
            "pre_commit_matches": False,
        }
        _print_dry_run(state, git_signing_status=(True, "ok"))
        output = capsys.readouterr().out
        assert "[DRY-RUN]" in output
        assert "repo_root" in output


class TestRunStep:
    """Test Run Step."""

    def test_returns_returncode(self, tmp_path: Path) -> None:
        """Test returns returncode."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(args=[], returncode=0)
            assert _run_step(["echo", "hi"], cwd=tmp_path) == 0

    def test_returns_nonzero(self, tmp_path: Path) -> None:
        """Test returns nonzero."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(args=[], returncode=1)
            assert _run_step(["fail"], cwd=tmp_path) == 1


class TestConfigureLocalGit:
    """Repository-workflow git settings applied to each clone by the bootstrap."""

    def test_applies_each_setting_locally(self, tmp_path: Path) -> None:
        """Every declared setting is written with --local scope, never --global.

        Scope matters: --global would override the developer's preference in unrelated
        repositories, which the bootstrap has no business doing.
        """
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(args=[], returncode=0, stderr="")
            _configure_local_git(tmp_path)
        assert mock_run.call_count == len(LOCAL_GIT_SETTINGS)
        for call, (key, value) in zip(mock_run.call_args_list, LOCAL_GIT_SETTINGS, strict=True):
            assert call.args[0] == ["git", "config", "--local", key, value]
            assert call.kwargs["cwd"] == tmp_path

    def test_failure_warns_but_does_not_raise(self, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """A git failure is advisory: stock git behaviour is friction, not a broken bootstrap."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[], returncode=1, stderr="fatal: not in a git directory"
            )
            _configure_local_git(tmp_path)
        assert "[WARN]" in capsys.readouterr().out


class TestCreateVenv:
    """Test Create Venv."""

    def test_calls_run_step(self, tmp_path: Path) -> None:
        """Test calls run step."""
        with patch("scripts.dev.bootstrap_venv._run_step", return_value=0) as mock_step:
            result = _create_venv(
                repo_root=tmp_path,
                venv_path=tmp_path / ".venv",
                system_python_cmd=["python3"],
            )
            assert result == 0
            assert mock_step.called


class TestEnsureVenv:
    """Test Ensure Venv."""

    def test_creates_venv_when_in_plan(self, tmp_path: Path) -> None:
        """Test creates venv when in plan."""
        venv_dir = tmp_path / ".venv" / "Scripts"
        venv_dir.mkdir(parents=True)
        python_exe = venv_dir / "python.exe"
        python_exe.touch()
        with patch("scripts.dev.bootstrap_venv._create_venv", return_value=0):
            venv_python, returncode = _ensure_venv(
                repo_root=tmp_path,
                platform="win32",
                plan=["create_venv"],
                which=lambda _: "python",
            )
            assert returncode == EXIT_OK
            assert venv_python is not None

    def test_fails_when_no_system_python(self, tmp_path: Path) -> None:
        """Test fails when no system python."""
        venv_python, returncode = _ensure_venv(
            repo_root=tmp_path,
            platform="win32",
            plan=["create_venv"],
            which=lambda _: None,
        )
        assert returncode == EXIT_FAILED
        assert venv_python is None

    def test_fails_when_create_fails(self, tmp_path: Path) -> None:
        """Test fails when create fails."""
        with patch("scripts.dev.bootstrap_venv._create_venv", return_value=1):
            venv_python, returncode = _ensure_venv(
                repo_root=tmp_path,
                platform="win32",
                plan=["create_venv"],
                which=lambda _: "python",
            )
            assert returncode == 1
            assert venv_python is None

    def test_fails_when_venv_python_not_found_after_create(self, tmp_path: Path) -> None:
        """Test fails when venv python not found after create."""
        with patch("scripts.dev.bootstrap_venv._create_venv", return_value=0):
            venv_python, returncode = _ensure_venv(
                repo_root=tmp_path,
                platform="win32",
                plan=["create_venv"],
                which=lambda _: "python",
            )
            assert returncode == EXIT_FAILED
            assert venv_python is None

    def test_skips_create_when_not_in_plan(self, tmp_path: Path) -> None:
        """Test skips create when not in plan."""
        venv_dir = tmp_path / ".venv" / "Scripts"
        venv_dir.mkdir(parents=True)
        python_exe = venv_dir / "python.exe"
        python_exe.touch()
        venv_python, returncode = _ensure_venv(
            repo_root=tmp_path,
            platform="win32",
            plan=["upgrade_pip"],
            which=lambda _: "python",
        )
        assert returncode == EXIT_OK
        assert venv_python == python_exe


class TestRunBootstrapActions:
    """Test Run Bootstrap Actions."""

    def test_runs_all_actions(self, tmp_path: Path) -> None:
        """Test runs all actions."""
        venv_python = tmp_path / "python"
        plan = ["upgrade_pip", "install_dev_extras", "install_pre_commit_hooks"]
        with (
            patch("scripts.dev.bootstrap_venv._run_step", return_value=0),
            patch("scripts.dev.bootstrap_venv._check_pip_version"),
        ):
            assert _run_bootstrap_actions(repo_root=tmp_path, venv_python=venv_python, plan=plan) == EXIT_OK

    def test_stops_on_pip_failure(self, tmp_path: Path) -> None:
        """Test stops on pip failure."""
        with patch("scripts.dev.bootstrap_venv._run_step", return_value=1):
            result = _run_bootstrap_actions(
                repo_root=tmp_path,
                venv_python=tmp_path / "python",
                plan=["upgrade_pip"],
            )
            assert result == 1

    def test_stops_on_dev_extras_failure(self, tmp_path: Path) -> None:
        """Test stops on dev extras failure."""
        call_count = 0

        def _side_effect(*_args: Any, **_kwargs: Any) -> int:
            nonlocal call_count
            call_count += 1
            return 0 if call_count == 1 else 1

        with (
            patch("scripts.dev.bootstrap_venv._run_step", side_effect=_side_effect),
            patch("scripts.dev.bootstrap_venv._check_pip_version"),
        ):
            result = _run_bootstrap_actions(
                repo_root=tmp_path,
                venv_python=tmp_path / "python",
                plan=["upgrade_pip", "install_dev_extras"],
            )
            assert result == 1

    def test_stops_on_pre_commit_failure(self, tmp_path: Path) -> None:
        """Test stops on pre commit failure."""
        call_count = 0

        def _side_effect(*_args: Any, **_kwargs: Any) -> int:
            nonlocal call_count
            call_count += 1
            return 0 if call_count <= 2 else 1

        with (
            patch("scripts.dev.bootstrap_venv._run_step", side_effect=_side_effect),
            patch("scripts.dev.bootstrap_venv._check_pip_version"),
        ):
            result = _run_bootstrap_actions(
                repo_root=tmp_path,
                venv_python=tmp_path / "python",
                plan=["upgrade_pip", "install_dev_extras", "install_pre_commit_hooks"],
            )
            assert result == 1

    def test_empty_plan_is_ok(self, tmp_path: Path) -> None:
        """Test empty plan is ok."""
        assert _run_bootstrap_actions(repo_root=tmp_path, venv_python=tmp_path / "python", plan=[]) == EXIT_OK


class TestReportGitSigning:
    """Test Report Git Signing."""

    def test_configured(self, capsys: pytest.CaptureFixture[str]) -> None:
        """Test configured."""
        _report_git_signing((True, "SSH key found"))
        assert "[OK]" in capsys.readouterr().out

    def test_not_configured(self, capsys: pytest.CaptureFixture[str]) -> None:
        """Test not configured."""
        _report_git_signing((False, "No key found"))
        output = capsys.readouterr().out
        assert "[WARN]" in output
        assert "setup_git_signing" in output


class TestCheckPipVersion:
    """Test _check_pip_version warning logic."""

    def test_warns_on_old_pip(self, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """Emit warning when pip is below minimum."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[], returncode=0, stdout="pip 25.2 from /path (python 3.13)", stderr=""
            )
            _check_pip_version(tmp_path / "python", min_version="26.0")
            assert "below minimum" in capsys.readouterr().out

    def test_no_warning_on_current_pip(self, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """No warning when pip meets minimum."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[], returncode=0, stdout="pip 26.0.1 from /path (python 3.13)", stderr=""
            )
            _check_pip_version(tmp_path / "python", min_version="26.0")
            assert capsys.readouterr().out == ""

    def test_silent_on_pip_failure(self, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """No output when pip --version fails."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(args=[], returncode=1, stdout="", stderr="")
            _check_pip_version(tmp_path / "python")
            assert capsys.readouterr().out == ""

    def test_warns_on_pip_9_vs_26_minimum(self, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """Detect pip 9.0.1 < 26.0 numerically, not lexicographically."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[], returncode=0, stdout="pip 9.0.1 from /path (python 3.13)", stderr=""
            )
            _check_pip_version(tmp_path / "python", min_version="26.0")
            assert "below minimum" in capsys.readouterr().out


class TestVerifyBootstrap:
    """Test Verify Bootstrap."""

    def test_fails_when_no_venv(self, tmp_path: Path) -> None:
        """Test fails when no venv."""
        assert _verify_bootstrap(repo_root=tmp_path, platform="win32") == EXIT_FAILED

    def test_ok_when_no_requirements(self, tmp_path: Path) -> None:
        """Test ok when no requirements."""
        venv_dir = tmp_path / ".venv" / "Scripts"
        venv_dir.mkdir(parents=True)
        (venv_dir / "python.exe").touch()
        # No pyproject.toml means no requirements
        assert _verify_bootstrap(repo_root=tmp_path, platform="win32") == EXIT_OK

    def test_ok_when_all_packages_present(self, tmp_path: Path) -> None:
        """Test ok when all packages present."""
        venv_dir = tmp_path / ".venv" / "Scripts"
        venv_dir.mkdir(parents=True)
        (venv_dir / "python.exe").touch()
        (tmp_path / ".git" / "hooks").mkdir(parents=True)
        (tmp_path / ".git" / "hooks" / "pre-commit").touch()
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project.optional-dependencies]\ndev = ["testpkg>=1.0.0"]\n')
        with (
            patch("scripts.dev.bootstrap_venv._query_installed_versions", return_value={"testpkg": "9.9.9"}),
            patch("scripts.dev.bootstrap_venv._query_python_version", return_value="3.13.0"),
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")),
            patch("scripts.dev.bootstrap_venv._check_bootstrap_state", return_value=(True, "matches", "matches", "")),
        ):
            assert _verify_bootstrap(repo_root=tmp_path, platform="win32") == EXIT_OK

    def test_fails_when_packages_missing(self, tmp_path: Path) -> None:
        """Test fails when packages missing."""
        venv_dir = tmp_path / ".venv" / "Scripts"
        venv_dir.mkdir(parents=True)
        (venv_dir / "python.exe").touch()
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project.optional-dependencies]\ndev = ["testpkg>=1.0.0"]\n')
        with (
            patch("scripts.dev.bootstrap_venv._query_installed_versions", return_value={"testpkg": None}),
            patch("scripts.dev.bootstrap_venv._query_python_version", return_value="3.13.0"),
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")),
        ):
            assert _verify_bootstrap(repo_root=tmp_path, platform="win32") == EXIT_FAILED


class TestRunBootstrap:
    """Test Run Bootstrap."""

    def test_fails_when_no_pyproject(self, tmp_path: Path) -> None:
        """Test fails when no pyproject."""
        assert run_bootstrap(repo_root=tmp_path, platform="win32") == EXIT_FAILED

    def test_verify_mode(self, tmp_path: Path) -> None:
        """Test verify mode."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        with patch("scripts.dev.bootstrap_venv._verify_bootstrap", return_value=EXIT_OK) as mock_verify:
            result = run_bootstrap(repo_root=tmp_path, platform="win32", verify=True)
            assert result == EXIT_OK
            assert mock_verify.called

    def test_dry_run_mode(self, tmp_path: Path) -> None:
        """Test dry run mode."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        with patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")):
            result = run_bootstrap(
                repo_root=tmp_path,
                platform="win32",
                dry_run=True,
                which=lambda _: None,
            )
            assert result == EXIT_OK

    def test_already_up_to_date(self, tmp_path: Path) -> None:
        """Test already up to date."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        venv_dir = tmp_path / ".venv" / "Scripts"
        venv_dir.mkdir(parents=True)
        (venv_dir / "python.exe").touch()
        # Write matching state
        desired = build_desired_state(tmp_path)
        state_path = _state_file_path(tmp_path)
        write_bootstrap_state(state_path, desired)
        # Create pre-commit hook
        hooks_dir = tmp_path / ".git" / "hooks"
        hooks_dir.mkdir(parents=True)
        (hooks_dir / "pre-commit").touch()
        with patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")):
            result = run_bootstrap(repo_root=tmp_path, platform="win32")
            assert result == EXIT_OK

    def test_full_bootstrap_flow(self, tmp_path: Path) -> None:
        """Test full bootstrap flow."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        (tmp_path / ".pre-commit-config.yaml").write_text("repos: []")
        with (
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")),
            patch("scripts.dev.bootstrap_venv._ensure_venv") as mock_ensure,
            patch("scripts.dev.bootstrap_venv._run_bootstrap_actions", return_value=EXIT_OK),
        ):
            mock_ensure.return_value = (tmp_path / "python", EXIT_OK)
            result = run_bootstrap(repo_root=tmp_path, platform="win32", which=lambda _: None)
            assert result == EXIT_OK

    def test_ensure_venv_failure(self, tmp_path: Path) -> None:
        """Test ensure venv failure."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        with (
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")),
            patch("scripts.dev.bootstrap_venv._ensure_venv") as mock_ensure,
        ):
            mock_ensure.return_value = (None, EXIT_FAILED)
            result = run_bootstrap(repo_root=tmp_path, platform="win32", which=lambda _: None)
            assert result == EXIT_FAILED

    def test_actions_failure(self, tmp_path: Path) -> None:
        """Test actions failure."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        with (
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")),
            patch("scripts.dev.bootstrap_venv._ensure_venv") as mock_ensure,
            patch("scripts.dev.bootstrap_venv._run_bootstrap_actions", return_value=1),
        ):
            mock_ensure.return_value = (tmp_path / "python", EXIT_OK)
            result = run_bootstrap(repo_root=tmp_path, platform="win32", which=lambda _: None)
            assert result == 1

    def test_local_bootstrap_failure(self, tmp_path: Path) -> None:
        """Test local bootstrap failure stops bootstrap."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        with (
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")),
            patch("scripts.dev.bootstrap_venv._ensure_venv") as mock_ensure,
            patch("scripts.dev.bootstrap_venv._run_bootstrap_actions", return_value=EXIT_OK),
            patch("scripts.dev.bootstrap_venv._run_local_bootstrap", return_value=1),
        ):
            mock_ensure.return_value = (tmp_path / "python", EXIT_OK)
            result = run_bootstrap(repo_root=tmp_path, platform="win32", which=lambda _: None)
            assert result == 1

    def test_local_bootstrap_called_after_actions(self, tmp_path: Path) -> None:
        """Test local bootstrap runs after standard actions succeed."""
        (tmp_path / "pyproject.toml").write_text("[project]")
        with (
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "ok")),
            patch("scripts.dev.bootstrap_venv._ensure_venv") as mock_ensure,
            patch("scripts.dev.bootstrap_venv._run_bootstrap_actions", return_value=EXIT_OK),
            patch("scripts.dev.bootstrap_venv._run_local_bootstrap", return_value=EXIT_OK) as mock_local,
        ):
            mock_ensure.return_value = (tmp_path / "python", EXIT_OK)
            result = run_bootstrap(repo_root=tmp_path, platform="win32", which=lambda _: None)
            assert result == EXIT_OK
            assert mock_local.called


class TestRunLocalBootstrap:
    """Test spoke-local bootstrap extension point."""

    def test_skips_when_no_local_script(self, tmp_path: Path) -> None:
        """Test returns EXIT_OK when bootstrap_local.py does not exist."""
        result = _run_local_bootstrap(repo_root=tmp_path, venv_python=tmp_path / "python")
        assert result == EXIT_OK

    def test_runs_local_script_when_present(self, tmp_path: Path) -> None:
        """Test discovers and runs bootstrap_local.py."""
        local_dir = tmp_path / "scripts" / "dev"
        local_dir.mkdir(parents=True)
        (local_dir / "bootstrap_local.py").write_text("# placeholder")
        with patch("scripts.dev.bootstrap_venv._run_step", return_value=0) as mock_run:
            result = _run_local_bootstrap(repo_root=tmp_path, venv_python=tmp_path / "python")
            assert result == 0
            assert mock_run.called
            args = mock_run.call_args[0][0]
            assert "scripts.dev.bootstrap_local" in " ".join(args)

    def test_propagates_local_script_failure(self, tmp_path: Path) -> None:
        """Test propagates non-zero exit from bootstrap_local.py."""
        local_dir = tmp_path / "scripts" / "dev"
        local_dir.mkdir(parents=True)
        (local_dir / "bootstrap_local.py").write_text("# placeholder")
        with patch("scripts.dev.bootstrap_venv._run_step", return_value=1):
            result = _run_local_bootstrap(repo_root=tmp_path, venv_python=tmp_path / "python")
            assert result == 1


class TestDescribeLocalBootstrap:
    """Test dry-run description for local bootstrap."""

    def test_reports_found_when_script_exists(self, tmp_path: Path) -> None:
        """Test reports the script will run."""
        local_dir = tmp_path / "scripts" / "dev"
        local_dir.mkdir(parents=True)
        (local_dir / "bootstrap_local.py").write_text("# placeholder")
        desc = _describe_local_bootstrap(tmp_path)
        assert "would run" in desc
        assert "bootstrap_local" in desc

    def test_reports_noop_when_no_script(self, tmp_path: Path) -> None:
        """Test reports no-op."""
        desc = _describe_local_bootstrap(tmp_path)
        assert "no-op" in desc


class TestParseArgs:
    """Test Parse Args."""

    def test_dry_run_flag(self) -> None:
        """Test dry run flag."""
        args = _parse_args(["--dry-run"])
        assert args.dry_run is True
        assert args.verify is False

    def test_verify_flag(self) -> None:
        """Test verify flag."""
        args = _parse_args(["--verify"])
        assert args.verify is True
        assert args.dry_run is False

    def test_no_flags(self) -> None:
        """Test no flags."""
        args = _parse_args([])
        assert args.dry_run is False
        assert args.verify is False


class TestMain:
    """Test Main."""

    def test_dry_run_and_verify_mutual_exclusion(self) -> None:
        """Test dry run and verify mutual exclusion."""
        assert main(["--dry-run", "--verify"]) == EXIT_FAILED

    def test_delegates_to_run_bootstrap(self) -> None:
        """Test delegates to run bootstrap."""
        with patch("scripts.dev.bootstrap_venv.run_bootstrap", return_value=EXIT_OK) as mock_run:
            result = main([])
            assert result == EXIT_OK
            assert mock_run.called


class TestLoadDevRequirementsWithSpecifiers:
    """Test _load_dev_requirements_with_specifiers preserves version constraints.

    Uses synthetic package names to avoid coupling with real pyproject.toml entries.
    """

    def test_returns_empty_when_no_pyproject(self, tmp_path: Path) -> None:
        """Return empty list when pyproject.toml is missing."""
        assert _load_dev_requirements_with_specifiers(tmp_path) == []

    def test_parses_specifiers(self, tmp_path: Path) -> None:
        """Return (name, specifier) tuples preserving full constraint."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text(
            '[project.optional-dependencies]\ndev = ["testpkg>=1.0.0,<3.0.0", "foopkg>=0.5.0", "barpkg"]\n'
        )
        result = _load_dev_requirements_with_specifiers(tmp_path)
        assert ("barpkg", "") in result
        assert ("testpkg", ">=1.0.0,<3.0.0") in result
        assert ("foopkg", ">=0.5.0") in result

    def test_sorted_by_name(self, tmp_path: Path) -> None:
        """Result is sorted alphabetically by package name."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project.optional-dependencies]\ndev = ["zebrapkg>=1.0", "alphapkg>=2.0"]\n')
        result = _load_dev_requirements_with_specifiers(tmp_path)
        assert [row[0] for row in result] == ["alphapkg", "zebrapkg"]

    def test_parses_real_pyproject(self) -> None:
        """Integration: parse the real repo pyproject.toml without hardcoding versions."""
        from scripts.common.paths import repo_root

        root = repo_root()
        pyproject_path = root / "pyproject.toml"
        if not pyproject_path.exists():  # pragma: no cover - defensive guard for checkouts without pyproject
            pytest.skip("Real pyproject.toml not found")
        result = _load_dev_requirements_with_specifiers(root)
        # Structural assertions: no version coupling
        assert len(result) > 0
        assert all(isinstance(row, tuple) and len(row) == 2 for row in result)
        names = [row[0] for row in result]
        assert names == sorted(names)
        # Every name is normalized (lowercase, hyphens for separators)
        for name in names:
            assert name == name.lower()


class TestQueryInstalledVersions:
    """Test _query_installed_versions subprocess-based version lookup."""

    def test_returns_empty_for_no_names(self, tmp_path: Path) -> None:
        """Return empty dict when no package names provided."""
        assert _query_installed_versions(tmp_path / "python", []) == {}

    def test_returns_versions_when_present(self, tmp_path: Path) -> None:
        """Return dict of name -> version for installed packages (synthetic data)."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[],
                returncode=0,
                stdout='{"testpkg": "9.9.9", "foopkg": "1.2.3"}',
                stderr="",
            )
            result = _query_installed_versions(tmp_path / "python", ["testpkg", "foopkg"])
            assert result == {"testpkg": "9.9.9", "foopkg": "1.2.3"}

    def test_returns_none_for_missing(self, tmp_path: Path) -> None:
        """Missing packages map to None."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[],
                returncode=0,
                stdout='{"testpkg": "9.9.9", "foopkg": null}',
                stderr="",
            )
            result = _query_installed_versions(tmp_path / "python", ["testpkg", "foopkg"])
            assert result == {"testpkg": "9.9.9", "foopkg": None}

    def test_returns_empty_on_subprocess_failure(self, tmp_path: Path) -> None:
        """Return empty dict on subprocess failure."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(args=[], returncode=1, stdout="", stderr="")
            assert _query_installed_versions(tmp_path / "python", ["testpkg"]) == {}


class TestFormatVersionTable:
    """Test _format_version_table 3-column aligned output.

    Uses synthetic package names and versions to avoid coupling with real pyproject.toml.
    """

    def test_empty_rows_returns_header_only(self) -> None:
        """Empty requirements list produces only the header."""
        output = _format_version_table([])
        assert "Package" in output
        assert "Installed" in output
        assert "Required" in output


class TestFormatHealthSummaryEmpty:
    """Test _format_health_summary with an empty checks list."""

    def test_empty_checks_does_not_show_ready_banner(self) -> None:
        """Empty checks produces 'no checks available' instead of misleading READY."""
        output = _format_health_summary([], use_color=False)
        assert "READY FOR WORK" not in output
        assert "no checks available" in output

    def test_renders_three_columns(self) -> None:
        """Each row has package name, installed version, required specifier."""
        rows: list[tuple[str, str | None, str]] = [
            ("testpkg", "9.9.9", ">=1.0.0,<10.0.0"),
            ("foopkg", "1.2.3", ">=0.5.0"),
        ]
        output = _format_version_table(rows)
        assert "testpkg" in output
        assert "9.9.9" in output
        assert ">=1.0.0,<10.0.0" in output
        assert "foopkg" in output
        assert "1.2.3" in output

    def test_missing_installed_shown_as_placeholder(self) -> None:
        """Missing installed version renders as MISSING or dash."""
        rows: list[tuple[str, str | None, str]] = [("testpkg", None, ">=1.0.0")]
        output = _format_version_table(rows)
        assert "MISSING" in output or "-" in output
        assert "testpkg" in output

    def test_empty_specifier_rendered(self) -> None:
        """Empty specifier (no version constraint) renders cleanly."""
        rows: list[tuple[str, str | None, str]] = [("barpkg", "2.0.0", "")]
        output = _format_version_table(rows)
        assert "barpkg" in output
        assert "2.0.0" in output


@pytest.fixture
def verify_env(tmp_path: Path) -> Path:
    """Set up a venv + pyproject fixture used by _verify_bootstrap tests.

    Creates a Windows-layout venv python stub and a minimal pyproject.toml
    with a synthetic `testpkg>=1.0.0` dev dependency. Returns the repo root.
    """
    venv_dir = tmp_path / ".venv" / "Scripts"
    venv_dir.mkdir(parents=True)
    (venv_dir / "python.exe").touch()
    pyproject = tmp_path / "pyproject.toml"
    pyproject.write_text('[project.optional-dependencies]\ndev = ["testpkg>=1.0.0"]\n')
    return tmp_path


def _patch_verify_bootstrap_deps(*, git_signing_detail: str = "ok") -> Any:
    """Return a context manager that patches every external call in _verify_bootstrap.

    Callers use ``with _patch_verify_bootstrap_deps(...):`` to isolate
    _verify_bootstrap from subprocess and git-signing side effects.
    """
    from contextlib import ExitStack

    stack = ExitStack()
    stack.enter_context(
        patch("scripts.dev.bootstrap_venv._query_installed_versions", return_value={"testpkg": "9.9.9"})
    )
    stack.enter_context(patch("scripts.dev.bootstrap_venv._query_python_version", return_value="3.13.0"))
    stack.enter_context(patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, git_signing_detail)))
    return stack


class TestVerifyBootstrapTable:
    """Test _verify_bootstrap emits the 3-column table."""

    def test_table_printed_when_pyproject_has_deps(self, verify_env: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """Verify output contains the version table (synthetic fixture)."""
        with _patch_verify_bootstrap_deps():
            _verify_bootstrap(repo_root=verify_env, platform="win32")
            captured = capsys.readouterr()
            assert "Package" in captured.out
            assert "testpkg" in captured.out
            assert "9.9.9" in captured.out
            assert ">=1.0.0" in captured.out


class TestLoadRequiresPython:
    """Test _load_requires_python returns the pyproject requires-python specifier."""

    def test_returns_empty_when_no_pyproject(self, tmp_path: Path) -> None:
        """Return empty string when pyproject.toml is missing."""
        assert _load_requires_python(tmp_path) == ""

    def test_parses_specifier(self, tmp_path: Path) -> None:
        """Return the requires-python specifier string."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project]\nrequires-python = ">=3.11"\n')
        assert _load_requires_python(tmp_path) == ">=3.11"

    def test_returns_empty_when_missing_key(self, tmp_path: Path) -> None:
        """Return empty string when requires-python is not set."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project]\nname = "example"\n')
        assert _load_requires_python(tmp_path) == ""


class TestQueryPythonVersion:
    """Test _query_python_version returns the venv Python version."""

    def test_returns_version_on_success(self, tmp_path: Path) -> None:
        """Return version string from venv Python."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(args=[], returncode=0, stdout="3.13.0\n", stderr="")
            assert _query_python_version(tmp_path / "python") == "3.13.0"

    def test_returns_empty_on_failure(self, tmp_path: Path) -> None:
        """Return empty string on subprocess failure."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(args=[], returncode=1, stdout="", stderr="")
            assert _query_python_version(tmp_path / "python") == ""


class TestFormatHealthSummary:
    """Test _format_health_summary renders Expected/Found/Status with actions and banner."""

    def test_all_pass_shows_ready_banner(self) -> None:
        """All checks passing produces a READY banner."""
        checks = [
            ("Python version", ">=3.11", "3.13.0", True, ""),
            ("Bootstrap state", "fresh", "matches", True, ""),
            ("Pre-commit hooks", "installed", "installed", True, ""),
            ("Git signing", "configured", "configured", True, ""),
        ]
        output = _format_health_summary(checks, use_color=False)
        assert "READY" in output
        assert "ACTION NEEDED" not in output
        assert "PASS" in output

    def test_any_fail_shows_action_banner(self) -> None:
        """Any failing check produces an ACTION NEEDED banner."""
        checks = [
            ("Python version", ">=3.11", "3.13.0", True, ""),
            ("Pre-commit hooks", "installed", "missing", False, "Run: python -m scripts.dev.bootstrap_venv"),
        ]
        output = _format_health_summary(checks, use_color=False)
        assert "ACTION NEEDED" in output
        assert "FAIL" in output
        assert "Run: python -m scripts.dev.bootstrap_venv" in output

    def test_renders_expected_and_found(self) -> None:
        """Each check line includes expected and found values."""
        checks = [("Python version", ">=3.11", "3.13.0", True, "")]
        output = _format_health_summary(checks, use_color=False)
        assert ">=3.11" in output
        assert "3.13.0" in output
        assert "Python version" in output

    def test_no_color_mode_omits_ansi(self) -> None:
        """use_color=False produces output without ANSI escape codes."""
        checks = [("Python version", ">=3.11", "3.10.0", False, "Upgrade Python")]
        output = _format_health_summary(checks, use_color=False)
        assert "\x1b[" not in output

    def test_color_mode_emits_ansi(self) -> None:
        """use_color=True produces output with ANSI escape codes."""
        checks = [("Python version", ">=3.11", "3.13.0", True, "")]
        output = _format_health_summary(checks, use_color=True)
        assert "\x1b[" in output


class TestVerifyBootstrapHealthChecks:
    """Test _verify_bootstrap emits health signals alongside the package table."""

    def test_verify_includes_python_version_check(self, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """Verify output contains Python version check line."""
        # Override the fixture to add requires-python; use tmp_path directly.
        venv_dir = tmp_path / ".venv" / "Scripts"
        venv_dir.mkdir(parents=True)
        (venv_dir / "python.exe").touch()
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text(
            '[project]\nrequires-python = ">=3.11"\n[project.optional-dependencies]\ndev = ["testpkg>=1.0.0"]\n'
        )
        with _patch_verify_bootstrap_deps():
            _verify_bootstrap(repo_root=tmp_path, platform="win32")
            captured = capsys.readouterr()
            assert "Python version" in captured.out
            assert "3.13.0" in captured.out

    def test_verify_includes_pre_commit_check(self, verify_env: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """Verify output reports pre-commit hook status."""
        with _patch_verify_bootstrap_deps():
            _verify_bootstrap(repo_root=verify_env, platform="win32")
            captured = capsys.readouterr()
            assert "Pre-commit hooks" in captured.out

    def test_verify_includes_git_signing_check(self, verify_env: Path, capsys: pytest.CaptureFixture[str]) -> None:
        """Verify output reports git signing status."""
        with _patch_verify_bootstrap_deps(git_signing_detail="configured"):
            _verify_bootstrap(repo_root=verify_env, platform="win32")
            captured = capsys.readouterr()
            assert "Git signing" in captured.out


class TestLoadRequiresPythonNoProject:
    """Additional edge case coverage for _load_requires_python."""

    def test_returns_empty_when_no_project_table(self, tmp_path: Path) -> None:
        """Return empty when pyproject has no project table."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[build-system]\nrequires = ["setuptools"]\n')
        assert _load_requires_python(tmp_path) == ""


class TestLoadDevRequirementsWithSpecifiersEdgeCases:
    """Edge case coverage for _load_dev_requirements_with_specifiers."""

    def test_no_project_table(self, tmp_path: Path) -> None:
        """No [project] table returns empty list."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[build-system]\nrequires = ["setuptools"]\n')
        assert _load_dev_requirements_with_specifiers(tmp_path) == []

    def test_no_optional_dependencies(self, tmp_path: Path) -> None:
        """No [project.optional-dependencies] returns empty list."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project]\nname = "example"\n')
        assert _load_dev_requirements_with_specifiers(tmp_path) == []

    def test_no_dev_extra(self, tmp_path: Path) -> None:
        """No dev extra in optional-dependencies returns empty list."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project.optional-dependencies]\ntest = ["foopkg"]\n')
        assert _load_dev_requirements_with_specifiers(tmp_path) == []

    def test_skips_non_string_entries(self, tmp_path: Path) -> None:
        """Non-string entries in dev list are skipped."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project.optional-dependencies]\ndev = ["validpkg>=1.0", 123, "anotherpkg>=2.0"]\n')
        result = _load_dev_requirements_with_specifiers(tmp_path)
        names = [row[0] for row in result]
        assert "validpkg" in names
        assert "anotherpkg" in names

    def test_skips_entries_with_no_name(self, tmp_path: Path) -> None:
        """Entries with no extractable name are skipped."""
        pyproject = tmp_path / "pyproject.toml"
        pyproject.write_text('[project.optional-dependencies]\ndev = ["", "validpkg>=1.0"]\n')
        result = _load_dev_requirements_with_specifiers(tmp_path)
        assert result == [("validpkg", ">=1.0")]


class TestExtractSpecifierNoMatch:
    """Test _extract_specifier edge case when requirement has no name match."""

    def test_returns_empty_for_no_match(self) -> None:
        """Return empty string when requirement does not start with a valid name."""
        from scripts.dev.bootstrap_venv import _extract_specifier

        assert _extract_specifier(" ") == ""


class TestQueryInstalledVersionsEdgeCases:
    """Edge case coverage for _query_installed_versions."""

    def test_returns_empty_on_json_decode_error(self, tmp_path: Path) -> None:
        """Return empty dict when subprocess stdout is not valid JSON."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[], returncode=0, stdout="not-json-output", stderr=""
            )
            assert _query_installed_versions(tmp_path / "python", ["testpkg"]) == {}

    def test_returns_empty_when_parsed_not_dict(self, tmp_path: Path) -> None:
        """Return empty dict when subprocess emits JSON that is not a dict."""
        with patch("scripts.dev.bootstrap_venv.subprocess.run") as mock_run:
            mock_run.return_value = subprocess.CompletedProcess(
                args=[], returncode=0, stdout='["not", "a", "dict"]', stderr=""
            )
            assert _query_installed_versions(tmp_path / "python", ["testpkg"]) == {}


class TestShouldUseColor:
    """Test _should_use_color TTY and NO_COLOR detection."""

    def test_returns_false_when_no_color_env_set(self, monkeypatch: pytest.MonkeyPatch) -> None:
        """Honor NO_COLOR environment variable."""
        from scripts.dev.bootstrap_venv import _should_use_color

        monkeypatch.setenv("NO_COLOR", "1")
        assert _should_use_color() is False


class TestParseVersionTuple:
    """Test _parse_version_tuple numeric parsing."""

    def test_parses_standard_version(self) -> None:
        """Return integer tuple for dotted numeric version."""
        from scripts.dev.bootstrap_venv import _parse_version_tuple

        assert _parse_version_tuple("3.13.0") == (3, 13, 0)

    def test_stops_at_non_numeric_segment(self) -> None:
        """Truncate at a segment that does not start with digits."""
        from scripts.dev.bootstrap_venv import _parse_version_tuple

        assert _parse_version_tuple("3.13.abc") == (3, 13)

    def test_returns_empty_for_empty_string(self) -> None:
        """Empty input returns an empty tuple."""
        from scripts.dev.bootstrap_venv import _parse_version_tuple

        assert _parse_version_tuple("") == ()


class TestCheckPythonVersion:
    """Test _check_python_version semantics."""

    def test_fails_when_no_actual_version(self) -> None:
        """Report fail with remediation when venv Python reports no version."""
        from scripts.dev.bootstrap_venv import _check_python_version

        passed, _, _, action = _check_python_version(">=3.11", "")
        assert passed is False
        assert "run bootstrap_venv" in action

    def test_fails_when_version_below_minimum(self) -> None:
        """Report fail when actual version is below pyproject minimum."""
        from scripts.dev.bootstrap_venv import _check_python_version

        passed, _, _, action = _check_python_version(">=3.13", "3.10.0")
        assert passed is False
        assert "Upgrade Python" in action

    def test_passes_when_version_meets_minimum(self) -> None:
        """Report pass when actual version meets the pyproject minimum."""
        from scripts.dev.bootstrap_venv import _check_python_version

        passed, _, _, action = _check_python_version(">=3.11", "3.13.0")
        assert passed is True
        assert action == ""

    def test_fails_when_minor_version_below_double_digit_minimum(self) -> None:
        """Detect 3.9 < 3.11 via numeric comparison, not lexicographic."""
        from scripts.dev.bootstrap_venv import _check_python_version

        passed, _, _, action = _check_python_version(">=3.11", "3.9.0")
        assert passed is False
        assert "Upgrade Python" in action

    def test_passes_when_major_version_exceeds_minimum(self) -> None:
        """Python 4.0.0 should pass >=3.11 (not fail due to lex compare of '4' vs '3')."""
        from scripts.dev.bootstrap_venv import _check_python_version

        passed, _, _, _ = _check_python_version(">=3.11", "4.0.0")
        assert passed is True

    def test_passes_when_requires_python_empty(self) -> None:
        """No requires-python constraint accepts any version."""
        from scripts.dev.bootstrap_venv import _check_python_version

        passed, expected, found, action = _check_python_version("", "3.13.0")
        assert passed is True
        assert expected == "(any)"
        assert found == "3.13.0"
        assert action == ""


class TestCheckBootstrapStatePass:
    """Test _check_bootstrap_state returns pass when state matches."""

    def test_passes_when_state_matches(self, tmp_path: Path) -> None:
        """Return pass status when the stored state matches current pyproject."""
        from scripts.dev.bootstrap_venv import (
            _check_bootstrap_state,
            build_desired_state,
            write_bootstrap_state,
        )

        (tmp_path / "pyproject.toml").write_text('[project]\nname = "example"\n')
        (tmp_path / ".pre-commit-config.yaml").write_text("repos: []\n")
        (tmp_path / ".venv").mkdir()
        state_path = tmp_path / ".venv" / ".bootstrap_state.json"
        desired = build_desired_state(tmp_path)
        write_bootstrap_state(state_path, desired)
        passed, _, _, action = _check_bootstrap_state(tmp_path)
        assert passed is True
        assert action == ""


class TestCheckPreCommitHooks:
    """Test _check_pre_commit_hooks direct branches."""

    def test_passes_when_hook_exists(self, tmp_path: Path) -> None:
        """Return pass when .git/hooks/pre-commit exists."""
        from scripts.dev.bootstrap_venv import _check_pre_commit_hooks

        hooks_dir = tmp_path / ".git" / "hooks"
        hooks_dir.mkdir(parents=True)
        (hooks_dir / "pre-commit").touch()
        passed, expected, found, action = _check_pre_commit_hooks(tmp_path)
        assert passed is True
        assert expected == "installed"
        assert found == "installed"
        assert action == ""

    def test_fails_when_hook_missing(self, tmp_path: Path) -> None:
        """Return fail with remediation when hook is absent."""
        from scripts.dev.bootstrap_venv import _check_pre_commit_hooks

        passed, expected, found, action = _check_pre_commit_hooks(tmp_path)
        assert passed is False
        assert expected == "installed"
        assert found == "missing"
        assert "bootstrap_venv" in action


class TestCheckBootstrapStateDrift:
    """Test _check_bootstrap_state drift/fail branches."""

    def test_fails_when_state_file_missing(self, tmp_path: Path) -> None:
        """Return fail when .venv/.bootstrap_state.json is absent."""
        from scripts.dev.bootstrap_venv import _check_bootstrap_state

        (tmp_path / "pyproject.toml").write_text('[project]\nname = "example"\n')
        passed, expected, found, action = _check_bootstrap_state(tmp_path)
        assert passed is False
        assert expected == "matches"
        assert found == "drift"
        assert "bootstrap_venv" in action

    def test_fails_when_pyproject_hash_differs(self, tmp_path: Path) -> None:
        """Return fail when pyproject hash no longer matches stored state."""
        from scripts.dev.bootstrap_venv import (
            _check_bootstrap_state,
            build_desired_state,
            write_bootstrap_state,
        )

        (tmp_path / "pyproject.toml").write_text('[project]\nname = "original"\n')
        (tmp_path / ".venv").mkdir()
        state_path = tmp_path / ".venv" / ".bootstrap_state.json"
        desired = build_desired_state(tmp_path)
        write_bootstrap_state(state_path, desired)
        # Modify pyproject so the hash no longer matches
        (tmp_path / "pyproject.toml").write_text('[project]\nname = "modified"\n')
        passed, _, found, _ = _check_bootstrap_state(tmp_path)
        assert passed is False
        assert found == "drift"


class TestBuildHealthChecks:
    """Test _build_health_checks aggregates all four checks with correct ordering."""

    def test_returns_four_checks_in_order(self, tmp_path: Path) -> None:
        """Return list of 4 checks with expected names in order."""
        from scripts.dev.bootstrap_venv import _build_health_checks

        (tmp_path / "pyproject.toml").write_text('[project]\nrequires-python = ">=3.11"\nname = "example"\n')
        with (
            patch("scripts.dev.bootstrap_venv._query_python_version", return_value="3.13.0"),
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "configured")),
        ):
            checks = _build_health_checks(repo_root=tmp_path, venv_python=tmp_path / "python")
        assert len(checks) == 4
        names = [check[0] for check in checks]
        assert names == ["Python version", "Bootstrap state", "Pre-commit hooks", "Git signing"]

    def test_each_check_has_five_elements(self, tmp_path: Path) -> None:
        """Each check tuple is (name, expected, found, passed, action)."""
        from scripts.dev.bootstrap_venv import _build_health_checks

        (tmp_path / "pyproject.toml").write_text('[project]\nname = "example"\n')
        with (
            patch("scripts.dev.bootstrap_venv._query_python_version", return_value="3.13.0"),
            patch("scripts.dev.bootstrap_venv.check_git_signing", return_value=(True, "configured")),
        ):
            checks = _build_health_checks(repo_root=tmp_path, venv_python=tmp_path / "python")
        for check in checks:
            assert len(check) == 5
            assert isinstance(check[0], str)
            assert isinstance(check[3], bool)


class TestCheckGitSigningHealthFailure:
    """Test _check_git_signing_health returns fail when signing is not configured."""

    def test_fails_when_not_configured(self) -> None:
        """Report fail with detail when git signing is not configured."""
        from scripts.dev.bootstrap_venv import _check_git_signing_health

        with patch(
            "scripts.dev.bootstrap_venv.check_git_signing",
            return_value=(False, "gpg.ssh.program missing"),
        ):
            passed, expected, found, action = _check_git_signing_health()
        assert passed is False
        assert expected == "configured"
        assert found == "not configured"
        assert "gpg.ssh.program missing" in action
