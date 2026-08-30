#!/usr/bin/env python3
"""Bootstrap the repo venv and dev tooling (cross-platform).

Usage:
    python -m scripts.dev.bootstrap_venv
    python3 -m scripts.dev.bootstrap_venv
    py -3 -m scripts.dev.bootstrap_venv
    python -m scripts.dev.bootstrap_venv --dry-run
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tomllib
from collections.abc import Callable, Mapping
from pathlib import Path

from scripts.testing.hooks.check_git_signing import check_git_signing

EXIT_OK = 0
EXIT_FAILED = 1
VENV_DIR = ".venv"
PYPROJECT_FILE = "pyproject.toml"
STATE_FILENAME = ".bootstrap_state.json"
State = Mapping[str, object]
_REQ_NAME_RE = re.compile(r"^[A-Za-z0-9_.-]+")


def repo_root_from_script(script_path: Path) -> Path:
    """Resolve repo root from script location.

    Uses parents[2] instead of tools.common.paths.repo_root() because
    this bootstrapper runs before the venv exists — the package may not
    be importable yet.
    """
    return script_path.resolve().parents[2]


def _venv_python_candidates(repo_root: Path) -> list[Path]:
    return [
        repo_root / VENV_DIR / "bin" / "python",
        repo_root / VENV_DIR / "Scripts" / "python.exe",
    ]


def _is_executable_path(path: Path, platform: str) -> bool:
    if platform == "win32":
        return path.exists()
    return path.exists() and os.access(path, os.X_OK)


def _resolve_venv_python(repo_root: Path, platform: str) -> Path | None:
    for candidate in _venv_python_candidates(repo_root):
        if _is_executable_path(candidate, platform):
            return candidate
    return None


def resolve_system_python_cmd(platform: str, which: Callable[[str], str | None] = shutil.which) -> list[str] | None:
    """Resolve system python cmd."""
    if platform == "win32":
        candidates = ["python", "py"]
    else:
        candidates = ["python3", "python"]

    for candidate in candidates:
        if which(candidate):
            if candidate == "py":
                return ["py", "-3"]
            return [candidate]
    return None


def _state_file_path(repo_root: Path) -> Path:
    return repo_root / VENV_DIR / STATE_FILENAME


def _hash_file(path: Path) -> str | None:
    if not path.exists():
        return None
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _normalize_package_name(name: str) -> str:
    return re.sub(r"[-_.]+", "-", name).lower()


def _extract_requirement_name(requirement: str) -> str | None:
    if not requirement:
        return None
    base = requirement.split(";", 1)[0].strip()
    base = base.split("@", 1)[0].strip()
    base = base.split("[", 1)[0].strip()
    match = _REQ_NAME_RE.match(base)
    return match.group(0) if match else None


def _load_dev_dep_strings(repo_root: Path) -> list[str]:
    """Read the raw dev-dependency strings from pyproject.toml.

    Returns an empty list when pyproject.toml is missing or has no
    [project.optional-dependencies.dev] list. Non-string entries in the
    list are filtered out.
    """
    pyproject_path = repo_root / PYPROJECT_FILE
    if not pyproject_path.exists():
        return []
    data = tomllib.loads(pyproject_path.read_text(encoding="utf-8"))
    project = data.get("project")
    if not isinstance(project, dict):
        return []
    optional_deps = project.get("optional-dependencies")
    if not isinstance(optional_deps, dict):
        return []
    dev_deps = optional_deps.get("dev")
    if not isinstance(dev_deps, list):
        return []
    return [entry for entry in dev_deps if isinstance(entry, str)]


def _extract_specifier(requirement: str) -> str:
    """Return the version specifier portion of a PEP 508 requirement string."""
    base = requirement.split(";", 1)[0].strip()
    base = base.split("@", 1)[0].strip()
    base = base.split("[", 1)[0].strip()
    name_match = _REQ_NAME_RE.match(base)
    if not name_match:
        return ""
    return base[name_match.end() :].strip()


def _load_dev_requirements_with_specifiers(repo_root: Path) -> list[tuple[str, str]]:
    """Load dev requirements preserving version specifiers.

    Returns a sorted list of (normalized_name, specifier) tuples. Packages
    without an explicit specifier map to an empty string.
    """
    rows: dict[str, str] = {}
    for entry in _load_dev_dep_strings(repo_root):
        name = _extract_requirement_name(entry)
        if not name:
            continue
        normalized = _normalize_package_name(name)
        specifier = _extract_specifier(entry)
        rows[normalized] = specifier
    return sorted(rows.items())


def _query_installed_versions(venv_python: Path, names: list[str]) -> dict[str, str | None]:
    """Query installed package versions from the venv Python.

    Returns a dict mapping each requested name to its installed version, or
    None if the package is not installed. Returns an empty dict on subprocess
    failure.
    """
    if not names:
        return {}
    script = (
        "import json, sys\n"
        "from importlib import metadata\n"
        "names = json.loads(sys.argv[1])\n"
        "result = {}\n"
        "for name in names:\n"
        "    try:\n"
        "        result[name] = metadata.version(name)\n"
        "    except metadata.PackageNotFoundError:\n"
        "        result[name] = None\n"
        "print(json.dumps(result))\n"
    )
    proc = subprocess.run(
        [str(venv_python), "-c", script, json.dumps(names)],
        check=False,
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        return {}
    try:
        parsed = json.loads(proc.stdout.strip())
    except json.JSONDecodeError:
        return {}
    if not isinstance(parsed, dict):
        return {}
    return {str(key): (None if value is None else str(value)) for key, value in parsed.items()}


def _format_version_table(rows: list[tuple[str, str | None, str]]) -> str:
    """Render a 3-column aligned table: Package, Installed, Required.

    Missing installed versions render as "MISSING". Empty specifiers render
    as "(any)".
    """
    headers = ("Package", "Installed", "Required")
    display_rows: list[tuple[str, str, str]] = []
    for name, installed, required in rows:
        installed_str = installed if installed is not None else "MISSING"
        required_str = required if required else "(any)"
        display_rows.append((name, installed_str, required_str))
    col_widths = [
        max(len(headers[0]), *(len(row[0]) for row in display_rows)) if display_rows else len(headers[0]),
        max(len(headers[1]), *(len(row[1]) for row in display_rows)) if display_rows else len(headers[1]),
        max(len(headers[2]), *(len(row[2]) for row in display_rows)) if display_rows else len(headers[2]),
    ]
    separator = "  ".join("-" * width for width in col_widths)
    header_line = "  ".join(header.ljust(width) for header, width in zip(headers, col_widths, strict=True))
    lines = [header_line, separator]
    for row in display_rows:
        lines.append("  ".join(value.ljust(width) for value, width in zip(row, col_widths, strict=True)))
    return "\n".join(lines)


def _load_requires_python(repo_root: Path) -> str:
    """Return the project.requires-python specifier from pyproject.toml."""
    pyproject_path = repo_root / PYPROJECT_FILE
    if not pyproject_path.exists():
        return ""
    data = tomllib.loads(pyproject_path.read_text(encoding="utf-8"))
    project = data.get("project")
    if not isinstance(project, dict):
        return ""
    value = project.get("requires-python")
    return value if isinstance(value, str) else ""


def _query_python_version(venv_python: Path) -> str:
    """Return the version (e.g., '3.13.0') reported by the venv Python."""
    proc = subprocess.run(
        [str(venv_python), "-c", "import sys; print('{}.{}.{}'.format(*sys.version_info[:3]))"],
        check=False,
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        return ""
    return proc.stdout.strip()


def _should_use_color() -> bool:
    """Return True when ANSI colors should be emitted."""
    if os.environ.get("NO_COLOR"):
        return False
    return sys.stdout.isatty()


def _format_health_summary(
    checks: list[tuple[str, str, str, bool, str]],
    *,
    use_color: bool,
) -> str:
    """Render a health check table with an aggregate status banner.

    Each check is (name, expected, found, passed, action). Action is the
    remediation message shown under failing checks. Banner is READY (green)
    when all pass, ACTION NEEDED (red) otherwise. Empty checks list
    produces a "(no checks available)" line instead of a misleading
    READY banner.
    """
    if not checks:
        return "Venv Health Check\n(no checks available)"
    green = "\x1b[32m" if use_color else ""
    red = "\x1b[31m" if use_color else ""
    reset = "\x1b[0m" if use_color else ""
    headers = ("Check", "Expected", "Found", "Status")
    rows: list[tuple[str, str, str, str]] = [
        (name, expected, found, "PASS" if passed else "FAIL") for name, expected, found, passed, _ in checks
    ]
    widths = [max(len(headers[i]), *(len(row[i]) for row in rows)) for i in range(len(headers))]
    gap_count = len(headers) - 1
    gap_width = 2
    lines: list[str] = []
    lines.append("Venv Health Check")
    lines.append("-" * (sum(widths) + gap_count * gap_width))
    lines.append("  ".join(header.ljust(width) for header, width in zip(headers, widths, strict=True)))
    lines.append("  ".join("-" * width for width in widths))
    for name, expected, found, passed, action in checks:
        status = "PASS" if passed else "FAIL"
        color = green if passed else red
        row = [
            name.ljust(widths[0]),
            expected.ljust(widths[1]),
            found.ljust(widths[2]),
            f"{color}{status}{reset}".ljust(widths[3] + len(color) + len(reset)),
        ]
        lines.append("  ".join(row))
        if not passed and action:
            lines.append(f"  Action: {action}")
    lines.append("")
    all_passed = all(passed for _, _, _, passed, _ in checks)
    banner_color = green if all_passed else red
    banner_text = "READY FOR WORK" if all_passed else "ACTION NEEDED"
    banner_width = max(40, len(banner_text) + 4)
    border = "=" * banner_width
    padding = (banner_width - len(banner_text)) // 2
    padded_banner = " " * padding + banner_text + " " * (banner_width - padding - len(banner_text))
    lines.append(f"{banner_color}{border}{reset}")
    lines.append(f"{banner_color}{padded_banner}{reset}")
    lines.append(f"{banner_color}{border}{reset}")
    return "\n".join(lines)


def build_desired_state(repo_root: Path) -> dict[str, str | None]:
    """Build desired state."""
    return {
        "pyproject_hash": _hash_file(repo_root / PYPROJECT_FILE),
        "pre_commit_config_hash": _hash_file(repo_root / ".pre-commit-config.yaml"),
    }


def load_bootstrap_state(state_path: Path) -> dict[str, str | None] | None:
    """Load bootstrap state."""
    if not state_path.exists():
        return None
    try:
        data = json.loads(state_path.read_text(encoding="utf-8"))
    except OSError, json.JSONDecodeError:
        return None
    if not isinstance(data, dict):
        return None
    return {
        "pyproject_hash": data.get("pyproject_hash"),
        "pre_commit_config_hash": data.get("pre_commit_config_hash"),
    }


def write_bootstrap_state(state_path: Path, desired_state: dict[str, str | None]) -> None:
    """Write bootstrap state."""
    state_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "pyproject_hash": desired_state.get("pyproject_hash"),
        "pre_commit_config_hash": desired_state.get("pre_commit_config_hash"),
    }
    state_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def collect_state(
    *,
    repo_root: Path,
    platform: str,
    which: Callable[[str], str | None] = shutil.which,
) -> State:
    """Collect state."""
    venv_python = _resolve_venv_python(repo_root, platform)
    pyproject_present = (repo_root / PYPROJECT_FILE).exists()
    desired_state = build_desired_state(repo_root) if pyproject_present else None
    state_path = _state_file_path(repo_root)
    existing_state = load_bootstrap_state(state_path)
    pyproject_matches = bool(
        existing_state and desired_state and existing_state.get("pyproject_hash") == desired_state.get("pyproject_hash")
    )
    pre_commit_matches = bool(
        existing_state
        and desired_state
        and existing_state.get("pre_commit_config_hash") == desired_state.get("pre_commit_config_hash")
    )
    state_matches = pyproject_matches and pre_commit_matches
    return {
        "platform": platform,
        "repo_root": str(repo_root),
        "system_python": (" ".join(resolve_system_python_cmd(platform, which=which) or []) or None),
        "venv_path": str(repo_root / VENV_DIR),
        "venv_python": str(venv_python) if venv_python else None,
        "venv_exists": (repo_root / VENV_DIR).exists(),
        "pyproject_present": pyproject_present,
        "pre_commit_hook_exists": (repo_root / ".git" / "hooks" / "pre-commit").exists(),
        "state_matches": state_matches,
        "pyproject_matches": pyproject_matches,
        "pre_commit_matches": pre_commit_matches,
        "desired_state": desired_state,
        "state_path": str(state_path),
    }


def build_plan(state: State) -> list[str]:
    """Build plan."""
    actions = []
    if not state.get("pyproject_present"):
        actions.append("fail_missing_pyproject")
        return actions
    venv_missing = not state.get("venv_python")
    if venv_missing:
        actions.append("create_venv")
    if venv_missing or not state.get("pyproject_matches"):
        actions.append("upgrade_pip")
        actions.append("install_dev_extras")
    if venv_missing or (not state.get("pre_commit_hook_exists")) or (not state.get("pre_commit_matches")):
        actions.append("install_pre_commit_hooks")
    return actions


def describe_plan(state: State) -> list[str]:
    """Describe plan."""
    if not state.get("pyproject_present"):
        return ["fail_missing_pyproject: would stop (pyproject.toml missing)"]

    lines = []
    if not state.get("venv_python"):
        lines.append("create_venv: would create .venv")
    elif state.get("venv_exists") or state.get("venv_python"):  # pragma: no branch
        lines.append("create_venv: no-op (venv already exists)")

    venv_missing = not state.get("venv_python")
    if venv_missing or not state.get("pyproject_matches"):
        lines.append("upgrade_pip: would run .venv python -m pip install -U pip")
        lines.append('install_dev_extras: would run .venv python -m pip install -e ".[dev]"')
    else:
        lines.append("upgrade_pip: no-op (bootstrap state unchanged)")
        lines.append("install_dev_extras: no-op (bootstrap state unchanged)")

    if not venv_missing and state.get("pre_commit_matches") and state.get("pre_commit_hook_exists"):
        lines.append("install_pre_commit_hooks: no-op (hook already installed, state unchanged)")
    else:
        lines.append("install_pre_commit_hooks: would run .venv python -m pre_commit install --install-hooks")
    return lines


def _format_git_signing_status(signing_ok: bool, message: str) -> list[str]:
    status = "configured" if signing_ok else "not_configured"
    return [
        "  git_signing:",
        f"    status: {status}",
        f"    detail: {message}",
    ]


def _print_dry_run(
    state: State,
    *,
    git_signing_status: tuple[bool, str],
) -> None:
    print("[DRY-RUN] Repo bootstrap plan")
    print(f"  repo_root: {state.get('repo_root')}")
    print(f"  platform: {state.get('platform')}")
    print(f"  system_python: {state.get('system_python')}")
    print(f"  venv_path: {state.get('venv_path')}")
    print(f"  venv_python: {state.get('venv_python')}")
    print("  actions:")
    for line in describe_plan(state):
        print(f"    - {line}")
    repo_root = Path(str(state.get("repo_root", "")))
    print(f"    - {_describe_local_bootstrap(repo_root)}")
    signing_ok, message = git_signing_status
    for line in _format_git_signing_status(signing_ok, message):
        print(line)


def _run_step(argv: list[str], *, cwd: Path) -> int:
    proc = subprocess.run(argv, cwd=str(cwd), check=False)
    return int(proc.returncode)


def _create_venv(
    *,
    repo_root: Path,
    venv_path: Path,
    system_python_cmd: list[str],
) -> int:
    argv = [*system_python_cmd, "-m", "venv", str(venv_path)]
    return _run_step(argv, cwd=repo_root)


def _ensure_venv(
    *,
    repo_root: Path,
    platform: str,
    plan: list[str],
    which: Callable[[str], str | None],
) -> tuple[Path | None, int]:
    venv_path = repo_root / VENV_DIR
    if "create_venv" in plan:
        system_python_cmd = resolve_system_python_cmd(platform, which=which)
        if not system_python_cmd:
            print("[FAIL] System Python not found on PATH.", file=sys.stderr)
            return None, EXIT_FAILED
        print(f"[INFO] Creating venv at {venv_path}.")
        create_rc = _create_venv(
            repo_root=repo_root,
            venv_path=venv_path,
            system_python_cmd=system_python_cmd,
        )
        if create_rc != 0:
            return None, create_rc

    venv_python = _resolve_venv_python(repo_root, platform)
    if venv_python is None:
        print("[FAIL] Repo venv python not found after creation.", file=sys.stderr)
        return None, EXIT_FAILED
    return venv_python, EXIT_OK


def _check_pip_version(venv_python: Path, *, min_version: str = "26.0") -> None:
    """Warn if pip version is below the minimum pinned in pyproject.toml."""
    proc = subprocess.run(
        [str(venv_python), "-m", "pip", "--version"],
        capture_output=True,
        text=True,
        check=False,
    )
    if proc.returncode != 0:
        return
    # Output format: "pip 26.0.1 from /path/to/pip (python 3.13)"
    parts = proc.stdout.strip().split()
    if len(parts) >= 2:
        installed = parts[1]
        installed_tuple = _parse_version_tuple(installed)
        min_tuple = _parse_version_tuple(min_version)
        if installed_tuple and min_tuple and installed_tuple < min_tuple:
            print(f"[WARN] pip {installed} is below minimum {min_version}. Run: pip install --upgrade pip")


def _run_bootstrap_actions(*, repo_root: Path, venv_python: Path, plan: list[str]) -> int:
    if "upgrade_pip" in plan:
        print("[INFO] Upgrading pip.")
        pip_rc = _run_step(
            [str(venv_python), "-m", "pip", "install", "-U", "pip"],
            cwd=repo_root,
        )
        if pip_rc != 0:
            return pip_rc
        _check_pip_version(venv_python)

    if "install_dev_extras" in plan:
        print("[INFO] Installing dev dependencies.")
        dev_rc = _run_step(
            [str(venv_python), "-m", "pip", "install", "-e", ".[dev]"],
            cwd=repo_root,
        )
        if dev_rc != 0:
            return dev_rc

    if "install_pre_commit_hooks" in plan:
        print("[INFO] Installing pre-commit hooks.")
        hook_rc = _run_step(
            [str(venv_python), "-m", "pre_commit", "install", "--install-hooks"],
            cwd=repo_root,
        )
        if hook_rc != 0:
            return hook_rc

    _configure_local_git(repo_root)

    return EXIT_OK


# Repository-workflow git settings applied to every clone. Git deliberately refuses to let a
# repository push config to the machines that clone it, so this cannot ship in a tracked file
# and be picked up automatically -- the bootstrap is the earliest point a clone runs our code.
#
# Scope is deliberately --local: it configures this checkout without overriding whatever the
# developer prefers in their other repositories.
LOCAL_GIT_SETTINGS: tuple[tuple[str, str], ...] = (
    # `git push` on a new branch sets its upstream instead of failing with
    # "has no upstream branch". Branches here are short-lived and always push to origin, so
    # the prompt is friction with no decision behind it.
    ("push.autoSetupRemote", "true"),
)


def _configure_local_git(repo_root: Path) -> None:
    """Apply the repository-workflow git settings to this clone.

    Advisory: a failure here leaves the developer with stock git behaviour, which is a
    friction, not a broken bootstrap. It must not fail the run.
    """
    for key, value in LOCAL_GIT_SETTINGS:
        proc = subprocess.run(
            ["git", "config", "--local", key, value],
            cwd=repo_root,
            capture_output=True,
            text=True,
            check=False,
        )
        if proc.returncode == 0:
            print(f"[OK] git config --local {key}={value}")
        else:
            print(f"[WARN] could not set git config {key}: {proc.stderr.strip()}")


BOOTSTRAP_LOCAL_MODULE = "scripts.dev.bootstrap_local"
BOOTSTRAP_LOCAL_PATH = Path("scripts/dev/bootstrap_local.py")


def _run_local_bootstrap(*, repo_root: Path, venv_python: Path) -> int:
    """Run spoke-local bootstrap if scripts/dev/bootstrap_local.py exists.

    Hub-and-spoke extension point: spokes place repo-specific setup
    (e.g., ansible-galaxy collection install) in bootstrap_local.py.
    The hub bootstrap discovers and runs it after the standard steps.
    """
    local_script = repo_root / BOOTSTRAP_LOCAL_PATH
    if not local_script.exists():
        return EXIT_OK
    print(f"[INFO] Running spoke-local bootstrap: {BOOTSTRAP_LOCAL_PATH}")
    return _run_step(
        [str(venv_python), "-m", BOOTSTRAP_LOCAL_MODULE],
        cwd=repo_root,
    )


def _describe_local_bootstrap(repo_root: Path) -> str:
    """Describe local bootstrap for dry-run output."""
    local_script = repo_root / BOOTSTRAP_LOCAL_PATH
    if local_script.exists():
        return f"run_local_bootstrap: would run {BOOTSTRAP_LOCAL_PATH}"
    return "run_local_bootstrap: no-op (scripts/dev/bootstrap_local.py not found)"


def _report_git_signing(signing_status: tuple[bool, str]) -> None:
    signing_ok, message = signing_status
    if signing_ok:
        print("[OK] Git signing is configured.")
        return
    print(f"[WARN] Git signing is not configured: {message}")
    print("Run: python -m scripts.devops.setup_git_signing")


def _verify_bootstrap(*, repo_root: Path, platform: str) -> int:
    venv_python = _resolve_venv_python(repo_root, platform)
    if venv_python is None:
        print("[FAIL] Repo venv python not found.", file=sys.stderr)
        return EXIT_FAILED
    specs = _load_dev_requirements_with_specifiers(repo_root)
    if not specs:
        print("[OK] No dev requirements found to verify.")
        return EXIT_OK
    installed = _query_installed_versions(venv_python, [name for name, _ in specs])
    table_rows: list[tuple[str, str | None, str]] = [
        (name, installed.get(name), specifier) for name, specifier in specs
    ]
    print(_format_version_table(table_rows))
    print()

    health_checks = _build_health_checks(repo_root=repo_root, venv_python=venv_python)
    print(_format_health_summary(health_checks, use_color=_should_use_color()))

    # The version query above already identifies missing packages; a query
    # failure returns {} and therefore reports every package missing.
    missing = [name for name, _ in specs if installed.get(name) is None]
    if missing:
        print("[FAIL] Missing dev dependencies in repo venv:", file=sys.stderr)
        for name in missing:
            print(f"- {name}", file=sys.stderr)
        return EXIT_FAILED
    all_health_ok = all(passed for _, _, _, passed, _ in health_checks)
    if not all_health_ok:
        return EXIT_FAILED
    return EXIT_OK


def _parse_version_tuple(version: str) -> tuple[int, ...]:
    """Parse a dotted version string into a tuple of integers.

    Non-numeric segments are truncated; leading integer portion is kept.
    Empty or unparseable input returns an empty tuple.
    """
    parts: list[int] = []
    for segment in version.split("."):
        match = re.match(r"\d+", segment)
        if not match:
            break
        parts.append(int(match.group(0)))
    return tuple(parts)


def _check_python_version(requires_python: str, actual_version: str) -> tuple[bool, str, str, str]:
    """Return (passed, expected, found, action) for the Python version check."""
    expected = requires_python if requires_python else "(any)"
    found = actual_version if actual_version else "unknown"
    if not actual_version:
        return (False, expected, found, "Venv Python did not report version; run bootstrap_venv")
    # Parse versions as integer tuples to avoid lexicographic string comparison
    # (e.g., "3.9.0" < "3.11" is False under string compare but True numerically).
    if requires_python.startswith(">="):
        min_version = requires_python[2:].strip()
        actual_tuple = _parse_version_tuple(actual_version)
        min_tuple = _parse_version_tuple(min_version)
        if actual_tuple and min_tuple and actual_tuple < min_tuple:
            return (False, expected, found, "Upgrade Python to match pyproject requires-python")
    return (True, expected, found, "")


def _check_bootstrap_state(repo_root: Path) -> tuple[bool, str, str, str]:
    """Return (passed, expected, found, action) for bootstrap state drift."""
    state_path = _state_file_path(repo_root)
    desired = build_desired_state(repo_root)
    existing = load_bootstrap_state(state_path)
    matches = bool(
        existing
        and desired
        and existing.get("pyproject_hash") == desired.get("pyproject_hash")
        and existing.get("pre_commit_config_hash") == desired.get("pre_commit_config_hash")
    )
    if matches:
        return (True, "matches", "matches", "")
    return (False, "matches", "drift", "Run: python -m scripts.dev.bootstrap_venv")


def _check_pre_commit_hooks(repo_root: Path) -> tuple[bool, str, str, str]:
    """Return (passed, expected, found, action) for pre-commit hook install."""
    hook_path = repo_root / ".git" / "hooks" / "pre-commit"
    if hook_path.exists():
        return (True, "installed", "installed", "")
    return (False, "installed", "missing", "Run: python -m scripts.dev.bootstrap_venv")


def _check_git_signing_health() -> tuple[bool, str, str, str]:
    """Return (passed, expected, found, action) for git signing config."""
    configured, detail = check_git_signing()
    if configured:
        return (True, "configured", "configured", "")
    return (False, "configured", "not configured", detail)


def _build_health_checks(
    *,
    repo_root: Path,
    venv_python: Path,
) -> list[tuple[str, str, str, bool, str]]:
    """Collect all health check results for the verify output."""
    requires_python = _load_requires_python(repo_root)
    actual_python = _query_python_version(venv_python)
    py_passed, py_expected, py_found, py_action = _check_python_version(requires_python, actual_python)
    state_passed, state_expected, state_found, state_action = _check_bootstrap_state(repo_root)
    hook_passed, hook_expected, hook_found, hook_action = _check_pre_commit_hooks(repo_root)
    sign_passed, sign_expected, sign_found, sign_action = _check_git_signing_health()
    return [
        ("Python version", py_expected, py_found, py_passed, py_action),
        ("Bootstrap state", state_expected, state_found, state_passed, state_action),
        ("Pre-commit hooks", hook_expected, hook_found, hook_passed, hook_action),
        ("Git signing", sign_expected, sign_found, sign_passed, sign_action),
    ]


def run_bootstrap(
    *,
    repo_root: Path,
    platform: str,
    dry_run: bool = False,
    verify: bool = False,
    which: Callable[[str], str | None] = shutil.which,
) -> int:
    """Run bootstrap."""
    if not (repo_root / PYPROJECT_FILE).exists():
        print("[FAIL] pyproject.toml not found; run from repo root.", file=sys.stderr)
        return EXIT_FAILED

    state = collect_state(repo_root=repo_root, platform=platform, which=which)
    plan = build_plan(state)
    if verify:
        # --verify runs its own signing health check via _build_health_checks;
        # checking here too would double the ssh-keygen smoke test.
        return _verify_bootstrap(repo_root=repo_root, platform=platform)
    signing_status = check_git_signing()
    if dry_run:
        _print_dry_run(state, git_signing_status=signing_status)
        return EXIT_OK

    if not plan:
        print("[OK] Repo bootstrap already up to date.")
        _report_git_signing(signing_status)
        return EXIT_OK

    venv_python, venv_rc = _ensure_venv(
        repo_root=repo_root,
        platform=platform,
        plan=plan,
        which=which,
    )
    if venv_rc != 0 or venv_python is None:
        return venv_rc

    actions_rc = _run_bootstrap_actions(repo_root=repo_root, venv_python=venv_python, plan=plan)
    if actions_rc != 0:
        return actions_rc

    local_rc = _run_local_bootstrap(repo_root=repo_root, venv_python=venv_python)
    if local_rc != 0:
        return local_rc

    desired_state = state.get("desired_state")
    state_path = state.get("state_path")
    if isinstance(desired_state, dict) and isinstance(state_path, str):
        write_bootstrap_state(Path(state_path), desired_state)
    _report_git_signing(signing_status)
    return EXIT_OK


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Bootstrap the repo venv and dev tooling.")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print current state and planned actions without making changes.",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="Verify dev dependencies are installed in the repo venv.",
    )
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    """Main."""
    args = _parse_args(argv)
    if args.dry_run and args.verify:
        print("[FAIL] --dry-run and --verify cannot be used together.", file=sys.stderr)
        return EXIT_FAILED
    repo_root = repo_root_from_script(Path(__file__))
    return run_bootstrap(
        repo_root=repo_root,
        platform=sys.platform,
        dry_run=args.dry_run,
        verify=args.verify,
    )


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main(sys.argv[1:]))  # pragma: no cover
