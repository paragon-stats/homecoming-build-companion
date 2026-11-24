"""Test run super linter."""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

import pytest

from scripts.common.paths import repo_root
from scripts.linting.tests.stub_factories import make_docker_runner_success, make_git_runner


@pytest.fixture(autouse=True)
def _set_repo_root(monkeypatch: pytest.MonkeyPatch) -> None:
    # Ensure the runner computes the correct repo root for tests.
    monkeypatch.chdir(repo_root())


def test_linux_prints_start_command_when_container_missing(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test linux prints start command when container missing."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_DEFAULT_BRANCH", raising=False)
    monkeypatch.delenv("SUPER_LINTER_VALIDATE_ALL_CODEBASE", raising=False)
    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")

    seen: list[list[str]] = []

    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success(seen=seen))

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 0

    # Sanity-check key docker run arguments are present.
    docker_run_cmds = [comprehension for comprehension in seen if comprehension[:2] == ["docker", "run"]]
    assert len(docker_run_cmds) == 1
    cmd = docker_run_cmds[0]
    assert "--rm" in cmd
    assert "--pull" in cmd
    assert runner.SUPER_LINTER_IMAGE_DEFAULT in cmd

    env_args = [value for value in cmd if value.startswith("DEFAULT_BRANCH=")]
    assert env_args == ["DEFAULT_BRANCH=origin/main"]

    # Shared config loaded via --env-file (LINTER_RULES_PATH, MARKDOWN_CONFIG_FILE
    # etc. are in .github/super-linter.env, not passed as -e args).
    assert "--env-file" in cmd


def test_docker_cmd_inserts_sudo_non_interactive_flag(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Test docker cmd inserts sudo non interactive flag."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "sudo docker")
    # Under pytest, stdio is typically not a TTY, so -n should be inserted.
    assert runner._docker_cmd()[:3] == ["sudo", "-n", "docker"]

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "sudo -n docker")
    assert runner._docker_cmd()[:3] == ["sudo", "-n", "docker"]


def test_docker_cmd_allows_interactive_sudo_when_tty(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Test docker cmd allows interactive sudo when tty."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "sudo docker")
    monkeypatch.setattr(sys.stdin, "isatty", lambda: True)
    monkeypatch.setattr(sys.stdout, "isatty", lambda: True)
    monkeypatch.setattr(sys.stderr, "isatty", lambda: True)

    assert runner._docker_cmd()[:2] == ["sudo", "docker"]


def test_run_docker_prints_sudo_v_when_sudo_auth_required(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test run docker prints sudo v when sudo auth required."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "sudo -n docker")

    def fake_run(cmd: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
        if cmd[:4] == ["sudo", "-n", "docker", "version"]:
            return subprocess.CompletedProcess(
                cmd,
                1,
                stdout="",
                stderr="sudo: a password is required\n",
            )
        raise AssertionError(f"Unexpected command: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code != 0

    captured = capsys.readouterr()
    assert "sudo -v" in captured.err


def test_run_docker_fails_when_docker_missing(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test run docker fails when docker missing."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")

    def fake_run(cmd: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
        if cmd[:2] == ["docker", "version"]:
            raise FileNotFoundError("docker not found")
        raise AssertionError(f"Unexpected command: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 127

    captured = capsys.readouterr()
    assert "Docker command not found" in captured.err


def test_run_docker_falls_back_to_validate_all_codebase_when_ref_missing(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test run docker falls back to validate all codebase when ref missing."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_DEFAULT_BRANCH", raising=False)
    monkeypatch.delenv("SUPER_LINTER_VALIDATE_ALL_CODEBASE", raising=False)
    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")

    seen: list[list[str]] = []

    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner(ref_exists=False))
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success(seen=seen))

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 0

    captured = capsys.readouterr()
    assert "DEFAULT_BRANCH ref not found locally" in captured.err

    docker_run_cmds = [comprehension for comprehension in seen if comprehension[:2] == ["docker", "run"]]
    assert len(docker_run_cmds) == 1
    cmd = docker_run_cmds[0]
    assert "DEFAULT_BRANCH=origin/main" in cmd
    assert "VALIDATE_ALL_CODEBASE=true" in cmd


def test_run_docker_handles_missing_git(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
    """Test run docker handles missing git."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_DEFAULT_BRANCH", raising=False)
    monkeypatch.delenv("SUPER_LINTER_VALIDATE_ALL_CODEBASE", raising=False)
    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")

    monkeypatch.setattr(
        "scripts.linting.run_super_linter.git_runner.run_git", make_git_runner(error="git not found on PATH")
    )
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success())

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 0


def test_resolve_super_linter_image_default(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test resolve super linter image default."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_IMAGE", raising=False)
    assert runner._resolve_super_linter_image() == runner.SUPER_LINTER_IMAGE_DEFAULT


def test_resolve_super_linter_image_slim_override(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test resolve super linter image slim override."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_IMAGE", "ghcr.io/super-linter/super-linter:slim-v8")
    assert runner._resolve_super_linter_image() == runner.SUPER_LINTER_IMAGE_DEFAULT


def test_resolve_super_linter_image_custom(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test resolve super linter image custom."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_IMAGE", "my-image:v1")
    assert runner._resolve_super_linter_image() == "my-image:v1"


def test_resolve_default_branch_env_override(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test resolve default branch env override."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DEFAULT_BRANCH", "develop")
    assert runner._resolve_default_branch(Path.cwd()) == "develop"


def test_resolve_default_branch_non_remote_ref(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test resolve default branch falls back to main for a non-remote ref."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_DEFAULT_BRANCH", raising=False)
    monkeypatch.setattr(
        "scripts.linting.run_super_linter.git_runner.run_git",
        make_git_runner(head_ref="refs/heads/main"),
    )
    assert runner._resolve_default_branch(Path.cwd()) == "main"


def test_build_docker_args_without_env_file(tmp_path: Path) -> None:
    """Test build docker args omits --env-file when super-linter.env is absent."""
    import scripts.linting.run_super_linter as runner

    args = runner._build_docker_args(tmp_path, env_pairs=[("A", "b")], image="img:v8", pull_policy="never")
    assert "--env-file" not in args
    assert args[-1] == "img:v8"


def test_ensure_image_fresh_stale_no_local_image_id(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test ensure image fresh stale path skips removal when local image id is None."""
    import scripts.linting.run_super_linter as runner

    def fake_run(
        cmd: list[str], **_kwargs: object
    ) -> subprocess.CompletedProcess[str] | subprocess.CompletedProcess[bytes]:
        if cmd[1:3] == ["image", "inspect"]:
            if any("RepoDigests" in arg for arg in cmd):
                return subprocess.CompletedProcess(cmd, 0, stdout="img:v8@sha256:old", stderr="")
            return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="")
        if cmd[1:4] == ["buildx", "imagetools", "inspect"]:
            return subprocess.CompletedProcess(cmd, 0, stdout=b"new-manifest", stderr=b"")
        if cmd[1:2] == ["pull"]:
            return subprocess.CompletedProcess(cmd, 0)
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._ensure_image_fresh(["docker"], "img:v8", cwd=Path.cwd()) == 0
    captured = capsys.readouterr()
    assert "updated" in captured.err.lower()


def test_candidate_refs_for_refs_prefix() -> None:
    """Test candidate refs for refs prefix."""
    import scripts.linting.run_super_linter as runner

    assert runner._candidate_refs_for("refs/heads/main") == ["refs/heads/main"]


def test_candidate_refs_for_with_remote() -> None:
    """Test candidate refs for with remote."""
    import scripts.linting.run_super_linter as runner

    result = runner._candidate_refs_for("origin/main")
    assert "refs/remotes/origin/main" in result
    assert "refs/heads/origin/main" in result


def test_maybe_append_default_filter_excludes_non_default_image() -> None:
    """Test maybe append default filter excludes non default image."""
    import scripts.linting.run_super_linter as runner

    env_pairs: list[tuple[str, str]] = []
    runner._maybe_append_default_filter_excludes(env_pairs, image="custom:v1", repo_root=repo_root())
    assert not any(key == "FILTER_REGEX_EXCLUDE" for key, _ in env_pairs)


def test_maybe_append_default_filter_excludes_already_set(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test maybe append default filter excludes already set."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("FILTER_REGEX_EXCLUDE", "custom_pattern")
    env_pairs: list[tuple[str, str]] = []
    runner._maybe_append_default_filter_excludes(
        env_pairs, image=runner.SUPER_LINTER_IMAGE_DEFAULT, repo_root=repo_root()
    )
    assert not any(key == "FILTER_REGEX_EXCLUDE" for key, _ in env_pairs)


def test_maybe_append_default_filter_excludes_default_image(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test maybe append default filter excludes applies CI-parity pattern."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("FILTER_REGEX_EXCLUDE", raising=False)
    env_pairs: list[tuple[str, str]] = []
    runner._maybe_append_default_filter_excludes(
        env_pairs, image=runner.SUPER_LINTER_IMAGE_DEFAULT, repo_root=repo_root()
    )
    values = dict(env_pairs)
    assert "FILTER_REGEX_EXCLUDE" in values
    pattern = values["FILTER_REGEX_EXCLUDE"]
    assert ".venv" in pattern
    workflow = (repo_root() / ".github" / "workflows" / "super-linter.yml").read_text(encoding="utf-8")
    ci = re.search(r"^\s+FILTER_REGEX_EXCLUDE: '([^']*)'$", workflow, re.MULTILINE)
    assert ci is not None
    assert pattern.endswith("|" + ci.group(1))


def test_default_image_is_the_one_the_ci_action_pins() -> None:
    """The local image is the version the workflow's pinned super-linter action names."""
    import scripts.linting.run_super_linter as runner

    workflow = (repo_root() / ".github" / "workflows" / "super-linter.yml").read_text(encoding="utf-8")
    pinned = re.search(r"uses: super-linter/super-linter@[0-9a-f]{40}\s+#\s*(v[0-9.]+)", workflow)
    assert pinned is not None
    expected = f"ghcr.io/super-linter/super-linter:{pinned.group(1)}"
    assert expected == runner.SUPER_LINTER_IMAGE_DEFAULT


@pytest.mark.parametrize("reader", ["ci_super_linter_image", "ci_filter_regex_exclude"])
def test_ci_readers_fail_when_the_workflow_line_is_missing(tmp_path: Path, reader: str) -> None:
    """Each reader names the workflow when the line it needs is absent."""
    import scripts.linting.run_super_linter as runner

    (tmp_path / ".github" / "workflows").mkdir(parents=True)
    (tmp_path / ".github" / "workflows" / "super-linter.yml").write_text("jobs: {}\n", encoding="utf-8")
    with pytest.raises(ValueError, match=r"super-linter\.yml"):
        getattr(runner, reader)(tmp_path)


def test_worktree_git_args_empty_for_a_normal_checkout(tmp_path: Path) -> None:
    """A checkout whose .git is a directory needs no extra mounts."""
    import scripts.linting.run_super_linter as runner

    (tmp_path / ".git").mkdir()
    assert runner._worktree_git_args(tmp_path) == []
    assert runner._git_common_dir(tmp_path) == tmp_path / ".git"


def test_worktree_git_args_mount_the_shared_git_dir(tmp_path: Path) -> None:
    """A linked worktree gets the shared git dir mounted and a .git file naming its container path."""
    import scripts.linting.run_super_linter as runner

    common = tmp_path / "repo" / ".git"
    git_dir = common / "worktrees" / "wt"
    git_dir.mkdir(parents=True)
    (git_dir / "commondir").write_text("../..\n", encoding="utf-8")
    work = tmp_path / "wt"
    work.mkdir()
    (work / ".git").write_text(f"gitdir: {git_dir}\n", encoding="utf-8")

    args = runner._worktree_git_args(work)

    gitfile = git_dir / "super-linter.gitfile"
    assert gitfile.read_text(encoding="utf-8") == "gitdir: /git-common/worktrees/wt"
    assert f"{common.resolve()}:/git-common" in args
    assert f"{gitfile}:/workspace/.git:ro" in args
    assert "GIT_CONFIG_VALUE_0=*" in args


@pytest.mark.parametrize(("hook_id", "suffixes"), [("markdownlint", (".md",)), ("editorconfig-checker", None)])
def test_pre_commit_excludes_agree_with_ci_on_every_tracked_file(
    hook_id: str, suffixes: tuple[str, ...] | None
) -> None:
    """A tracked file is skipped by the pre-commit hook exactly when CI's Super-Linter skips it."""
    import yaml

    import scripts.linting.run_super_linter as runner

    root = repo_root()
    config = yaml.safe_load((root / ".pre-commit-config.yaml").read_text(encoding="utf-8"))
    hook = next(h for repo in config["repos"] for h in repo["hooks"] if h["id"] == hook_id)
    local = re.compile(hook["exclude"])
    ci = re.compile(runner.ci_filter_regex_exclude(root))
    tracked = subprocess.run(["git", "ls-files"], cwd=root, capture_output=True, text=True, check=True).stdout.split()
    files = [f for f in tracked if suffixes is None or f.endswith(suffixes)]
    disagree = [f for f in files if bool(local.search(f)) != bool(ci.search(f"/github/workspace/{f}"))]
    assert disagree == []


def test_maybe_set_default_jscpd_non_default_image() -> None:
    """Test maybe set default jscpd non default image."""
    import scripts.linting.run_super_linter as runner

    env_pairs: list[tuple[str, str]] = []
    runner._maybe_set_default_jscpd_config_file(env_pairs, image="custom:v1", repo_root=Path.cwd())
    assert not any(key == "JSCPD_LINTER_RULES" for key, _ in env_pairs)


def test_maybe_set_default_jscpd_env_already_set(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test maybe set default jscpd env already set."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("JSCPD_LINTER_RULES", "/custom/path")
    env_pairs: list[tuple[str, str]] = []
    runner._maybe_set_default_jscpd_config_file(
        env_pairs, image=runner.SUPER_LINTER_IMAGE_DEFAULT, repo_root=Path.cwd()
    )
    assert not any(key == "JSCPD_LINTER_RULES" for key, _ in env_pairs)


def test_maybe_set_default_jscpd_no_config_file(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    """Test maybe set default jscpd no config file."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("JSCPD_LINTER_RULES", raising=False)
    monkeypatch.delenv("JSCPD_CONFIG_FILE", raising=False)
    env_pairs: list[tuple[str, str]] = []
    runner._maybe_set_default_jscpd_config_file(env_pairs, image=runner.SUPER_LINTER_IMAGE_DEFAULT, repo_root=tmp_path)
    assert not any(key == "JSCPD_LINTER_RULES" for key, _ in env_pairs)


def test_repo_root() -> None:
    """Test repo root."""
    import scripts.linting.run_super_linter as runner

    result = runner._repo_root()
    assert isinstance(result, Path)
    assert (result / "pyproject.toml").exists()


def test_is_docker_permission_denied() -> None:
    """Test is docker permission denied."""
    import scripts.linting.run_super_linter as runner

    assert (
        runner._is_docker_permission_denied("Got permission denied while trying to connect to the Docker daemon socket")
        is True
    )
    assert runner._is_docker_permission_denied("everything is fine") is False


def test_try_sudo_docker_cmd_windows(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test try sudo docker cmd windows."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setattr("scripts.linting.run_super_linter.sys.platform", "win32")
    assert runner._try_sudo_docker_cmd(Path.cwd()) is None


def test_try_sudo_docker_cmd_success(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test try sudo docker cmd success."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 0, stdout="Docker version", stderr="")

    monkeypatch.setattr("scripts.linting.run_super_linter.sys.platform", "linux")
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    result = runner._try_sudo_docker_cmd(Path.cwd())
    assert result == ["sudo", "-n", "docker"]


def test_try_sudo_docker_cmd_failure(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test try sudo docker cmd failure."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="sudo: a password is required")

    monkeypatch.setattr("scripts.linting.run_super_linter.sys.platform", "linux")
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._try_sudo_docker_cmd(Path.cwd()) is None


def test_resolve_usable_docker_cmd_permission_denied_sudo_fallback(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Test resolve usable docker cmd permission denied sudo fallback."""
    import scripts.linting.run_super_linter as runner

    call_count = 0

    def fake_run(cmd: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
        nonlocal call_count
        call_count += 1
        if cmd[:2] == ["docker", "version"]:
            return subprocess.CompletedProcess(
                cmd,
                1,
                stdout="",
                stderr="Got permission denied while trying to connect to the Docker daemon socket",
            )
        if cmd[:4] == ["sudo", "-n", "docker", "version"]:
            return subprocess.CompletedProcess(cmd, 0, stdout="Docker version", stderr="")
        raise AssertionError(f"Unexpected command: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.sys.platform", "linux")
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    result_cmd, ret = runner._resolve_usable_docker_cmd(Path.cwd(), ["docker"])
    assert result_cmd == ["sudo", "-n", "docker"]
    assert ret == 0


def test_resolve_usable_docker_cmd_permission_denied_no_sudo(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Test resolve usable docker cmd permission denied no sudo."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
        if cmd[:2] == ["docker", "version"]:
            return subprocess.CompletedProcess(
                cmd,
                1,
                stdout="",
                stderr="Got permission denied while trying to connect to the Docker daemon socket",
            )
        if cmd[:4] == ["sudo", "-n", "docker", "version"]:
            return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="sudo: a password is required")
        raise AssertionError(f"Unexpected command: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.sys.platform", "linux")
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    result_cmd, _ret = runner._resolve_usable_docker_cmd(Path.cwd(), ["docker"])
    assert result_cmd is None
    captured = capsys.readouterr()
    assert "Docker access denied" in captured.err


def test_resolve_usable_docker_cmd_generic_failure(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Test resolve usable docker cmd generic failure."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="some other error")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    result_cmd, _ret = runner._resolve_usable_docker_cmd(Path.cwd(), ["docker"])
    assert result_cmd is None
    captured = capsys.readouterr()
    assert "Docker is not usable" in captured.err


def test_resolve_validate_all_codebase_env_override(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test resolve validate all codebase env override."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_VALIDATE_ALL_CODEBASE", "TRUE")
    assert runner._resolve_validate_all_codebase(Path.cwd(), "main") == "true"


def test_resolve_pull_policy_invalid(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
    """Test resolve pull policy invalid."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_PULL_POLICY", "invalid")
    assert runner._resolve_pull_policy() is None
    captured = capsys.readouterr()
    assert "Invalid SUPER_LINTER_PULL_POLICY" in captured.err


def test_run_docker_invalid_pull_policy(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    """Test run docker invalid pull policy."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")
    monkeypatch.setenv("SUPER_LINTER_PULL_POLICY", "invalid")

    seen: list[list[str]] = []
    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success(seen=seen))

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 2


def test_run_docker_with_fcntl_locking(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Test run docker with fcntl locking."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")

    seen: list[list[str]] = []
    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success(seen=seen))

    # Create a mock fcntl module
    flock_calls: list[tuple[int, int]] = []

    class FakeFcntl:
        LOCK_EX = 2
        LOCK_UN = 8

        @staticmethod
        def flock(file_desc: int, operation: int) -> None:
            flock_calls.append((file_desc, operation))

    monkeypatch.setattr(runner, "fcntl", FakeFcntl)

    exit_code = runner._run_docker(repo_root())
    assert exit_code == 0
    # Verify both lock and unlock were called
    assert len(flock_calls) == 2
    assert flock_calls[0][1] == FakeFcntl.LOCK_EX
    assert flock_calls[1][1] == FakeFcntl.LOCK_UN


def test_main_calls_run_docker(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test main calls run docker."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setattr(runner, "_run_docker", lambda _root: 42)
    assert runner.main() == 42


# --- Image freshness check tests ---


def test_get_local_repo_digest_found(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test get local repo digest found."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(
            cmd, 0, stdout="ghcr.io/super-linter/super-linter@sha256:abc123\n", stderr=""
        )

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    result = runner._get_local_repo_digest(["docker"], "img:v8", cwd=Path.cwd())
    assert result == "sha256:abc123"


def test_get_local_repo_digest_not_found(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test get local repo digest not found."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="No such image")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._get_local_repo_digest(["docker"], "img:v8", cwd=Path.cwd()) is None


def test_get_local_repo_digest_no_at_sign(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test get local repo digest no at sign."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 0, stdout="malformed-output", stderr="")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._get_local_repo_digest(["docker"], "img:v8", cwd=Path.cwd()) is None


def test_get_remote_repo_digest_success(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test get remote repo digest success."""
    import hashlib

    import scripts.linting.run_super_linter as runner

    manifest = b'{"schemaVersion": 2}'
    expected = "sha256:" + hashlib.sha256(manifest).hexdigest()

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[bytes]:
        return subprocess.CompletedProcess(cmd, 0, stdout=manifest, stderr=b"")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    result = runner._get_remote_repo_digest(["docker"], "img:v8", cwd=Path.cwd())
    assert result == expected


def test_get_remote_repo_digest_failure(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test get remote repo digest failure."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[bytes]:
        return subprocess.CompletedProcess(cmd, 1, stdout=b"", stderr=b"not found")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._get_remote_repo_digest(["docker"], "img:v8", cwd=Path.cwd()) is None


def test_get_local_image_id_found(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test get local image id found."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 0, stdout="sha256:imgid999\n", stderr="")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._get_local_image_id(["docker"], "img:v8", cwd=Path.cwd()) == "sha256:imgid999"


def test_get_local_image_id_not_found(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test get local image id not found."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._get_local_image_id(["docker"], "img:v8", cwd=Path.cwd()) is None


def test_pull_image_success(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test pull image success."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 0)

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._pull_image(["docker"], "img:v8", cwd=Path.cwd()) == 0


def test_pull_image_failure(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test pull image failure."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(cmd, 1)

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._pull_image(["docker"], "img:v8", cwd=Path.cwd()) == 1


def test_remove_old_image(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test remove old image."""
    import scripts.linting.run_super_linter as runner

    seen: list[list[str]] = []

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        seen.append(cmd)
        return subprocess.CompletedProcess(cmd, 0, stdout="", stderr="")

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    runner._remove_old_image(["docker"], "sha256:old123", cwd=Path.cwd())
    assert seen == [["docker", "rmi", "sha256:old123"]]


def test_ensure_image_fresh_missing_pull_succeeds(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test ensure image fresh missing pull succeeds."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        if cmd[1:3] == ["image", "inspect"]:
            return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="")
        if cmd[1:2] == ["pull"]:
            return subprocess.CompletedProcess(cmd, 0)
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._ensure_image_fresh(["docker"], "img:v8", cwd=Path.cwd()) == 0
    captured = capsys.readouterr()
    assert "not found locally" in captured.err
    assert "image pulled" in captured.err


def test_ensure_image_fresh_missing_pull_fails(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test ensure image fresh missing pull fails."""
    import scripts.linting.run_super_linter as runner

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        if cmd[1:3] == ["image", "inspect"]:
            return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="")
        if cmd[1:2] == ["pull"]:
            return subprocess.CompletedProcess(cmd, 1)
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._ensure_image_fresh(["docker"], "img:v8", cwd=Path.cwd()) == 1
    captured = capsys.readouterr()
    assert "Failed to pull" in captured.err


def test_ensure_image_fresh_digests_match(monkeypatch: pytest.MonkeyPatch) -> None:
    """Test ensure image fresh digests match."""
    import hashlib

    import scripts.linting.run_super_linter as runner

    manifest = b'{"test": true}'
    digest = "sha256:" + hashlib.sha256(manifest).hexdigest()

    def fake_run(
        cmd: list[str], **_kwargs: object
    ) -> subprocess.CompletedProcess[str] | subprocess.CompletedProcess[bytes]:
        if cmd[1:3] == ["image", "inspect"]:
            return subprocess.CompletedProcess(cmd, 0, stdout=f"img:v8@{digest}", stderr="")
        if cmd[1:4] == ["buildx", "imagetools", "inspect"]:
            return subprocess.CompletedProcess(cmd, 0, stdout=manifest, stderr=b"")
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._ensure_image_fresh(["docker"], "img:v8", cwd=Path.cwd()) == 0


def test_ensure_image_fresh_stale_pulls_and_removes(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test ensure image fresh stale pulls and removes old image."""
    import scripts.linting.run_super_linter as runner

    seen: list[list[str]] = []

    def fake_run(
        cmd: list[str], **_kwargs: object
    ) -> subprocess.CompletedProcess[str] | subprocess.CompletedProcess[bytes]:
        seen.append(cmd)
        if cmd[1:3] == ["image", "inspect"]:
            if any("RepoDigests" in arg for arg in cmd):
                return subprocess.CompletedProcess(cmd, 0, stdout="img:v8@sha256:old_digest", stderr="")
            return subprocess.CompletedProcess(cmd, 0, stdout="sha256:oldid", stderr="")
        if cmd[1:4] == ["buildx", "imagetools", "inspect"]:
            return subprocess.CompletedProcess(cmd, 0, stdout=b"different-manifest", stderr=b"")
        if cmd[1:2] == ["pull"]:
            return subprocess.CompletedProcess(cmd, 0)
        if cmd[1:2] == ["rmi"]:
            return subprocess.CompletedProcess(cmd, 0, stdout="", stderr="")
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._ensure_image_fresh(["docker"], "img:v8", cwd=Path.cwd()) == 0
    captured = capsys.readouterr()
    assert "stale" in captured.err.lower()
    assert "updated" in captured.err.lower()
    rmi_cmds = [cmd for cmd in seen if cmd[1:2] == ["rmi"]]
    assert len(rmi_cmds) == 1
    assert rmi_cmds[0][2] == "sha256:oldid"


def test_ensure_image_fresh_remote_unavailable(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test ensure image fresh remote unavailable."""
    import scripts.linting.run_super_linter as runner

    def fake_run(
        cmd: list[str], **_kwargs: object
    ) -> subprocess.CompletedProcess[str] | subprocess.CompletedProcess[bytes]:
        if cmd[1:3] == ["image", "inspect"]:
            return subprocess.CompletedProcess(cmd, 0, stdout="img:v8@sha256:abc", stderr="")
        if cmd[1:4] == ["buildx", "imagetools", "inspect"]:
            return subprocess.CompletedProcess(cmd, 1, stdout=b"", stderr=b"network error")
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._ensure_image_fresh(["docker"], "img:v8", cwd=Path.cwd()) == 0
    captured = capsys.readouterr()
    assert "Cannot check registry" in captured.err


def test_ensure_image_fresh_stale_pull_fails_uses_cache(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    """Test ensure image fresh stale pull fails uses cache."""
    import scripts.linting.run_super_linter as runner

    def fake_run(
        cmd: list[str], **_kwargs: object
    ) -> subprocess.CompletedProcess[str] | subprocess.CompletedProcess[bytes]:
        if cmd[1:3] == ["image", "inspect"]:
            if any("RepoDigests" in arg for arg in cmd):
                return subprocess.CompletedProcess(cmd, 0, stdout="img:v8@sha256:old", stderr="")
            return subprocess.CompletedProcess(cmd, 0, stdout="sha256:oldid", stderr="")
        if cmd[1:4] == ["buildx", "imagetools", "inspect"]:
            return subprocess.CompletedProcess(cmd, 0, stdout=b"new-manifest", stderr=b"")
        if cmd[1:2] == ["pull"]:
            return subprocess.CompletedProcess(cmd, 1)
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)
    assert runner._ensure_image_fresh(["docker"], "img:v8", cwd=Path.cwd()) == 0
    captured = capsys.readouterr()
    assert "Using cached image" in captured.err


def test_run_docker_always_policy_uses_pull_never(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Test run docker always policy uses pull never."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_DEFAULT_BRANCH", raising=False)
    monkeypatch.delenv("SUPER_LINTER_VALIDATE_ALL_CODEBASE", raising=False)
    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")
    monkeypatch.setenv("SUPER_LINTER_PULL_POLICY", "always")

    seen: list[list[str]] = []
    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success(seen=seen))

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 0

    docker_run_cmds = [cmd for cmd in seen if cmd[:2] == ["docker", "run"]]
    assert len(docker_run_cmds) == 1
    cmd = docker_run_cmds[0]
    pull_idx = cmd.index("--pull")
    assert cmd[pull_idx + 1] == "never"


def test_run_docker_always_policy_freshness_failure(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Test run docker always policy freshness failure."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")
    monkeypatch.setenv("SUPER_LINTER_PULL_POLICY", "always")

    def fake_run(cmd: list[str], **_kwargs: object) -> subprocess.CompletedProcess[str]:
        if cmd[:2] == ["docker", "version"]:
            return subprocess.CompletedProcess(cmd, 0, stdout="", stderr="")
        # Image missing locally + pull fails → _ensure_image_fresh returns 1
        if cmd[1:3] == ["image", "inspect"]:
            return subprocess.CompletedProcess(cmd, 1, stdout="", stderr="")
        if cmd[1:2] == ["pull"]:
            return subprocess.CompletedProcess(cmd, 1)
        raise AssertionError(f"Unexpected: {cmd}")  # pragma: no cover

    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", fake_run)

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 1


def test_run_docker_missing_policy_uses_pull_missing(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Test run docker missing policy uses pull missing."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_DEFAULT_BRANCH", raising=False)
    monkeypatch.delenv("SUPER_LINTER_VALIDATE_ALL_CODEBASE", raising=False)
    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")
    monkeypatch.setenv("SUPER_LINTER_PULL_POLICY", "missing")

    seen: list[list[str]] = []
    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success(seen=seen))

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 0

    docker_run_cmds = [cmd for cmd in seen if cmd[:2] == ["docker", "run"]]
    assert len(docker_run_cmds) == 1
    cmd = docker_run_cmds[0]
    pull_idx = cmd.index("--pull")
    assert cmd[pull_idx + 1] == "missing"


def test_build_env_pairs_omits_ansible_directory(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """Test _build_env_pairs drops homelab-only ANSIBLE_DIRECTORY override."""
    import scripts.linting.run_super_linter as runner

    monkeypatch.delenv("SUPER_LINTER_DEFAULT_BRANCH", raising=False)
    monkeypatch.delenv("SUPER_LINTER_VALIDATE_ALL_CODEBASE", raising=False)
    monkeypatch.setenv("SUPER_LINTER_DOCKER_CMD", "docker")

    seen: list[list[str]] = []
    monkeypatch.setattr("scripts.linting.run_super_linter.git_runner.run_git", make_git_runner())
    monkeypatch.setattr("scripts.linting.run_super_linter.subprocess.run", make_docker_runner_success(seen=seen))

    exit_code = runner._run_docker(Path.cwd())
    assert exit_code == 0

    docker_run_cmds = [cmd for cmd in seen if cmd[:2] == ["docker", "run"]]
    assert len(docker_run_cmds) == 1
    cmd = docker_run_cmds[0]
    assert not any(val.startswith("ANSIBLE_DIRECTORY=") for val in cmd)
    assert not any(val.startswith("ANSIBLE_COLLECTIONS_PATH=") for val in cmd)
