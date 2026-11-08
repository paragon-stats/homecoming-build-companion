"""Where the forum source material lives: one resolver shared by every CLI that reads or writes captures.

The captures sit in a private checkout beside this repository's main checkout. The root is, in order,
the ``--source-root`` flag, the ``COH_SOURCE_ROOT`` variable, then ``../homecoming-source-material``
relative to the main checkout (a harness worktree resolves to the main checkout, not to itself).

Errors: E04 the resolved root is missing or is not a directory.
"""

from __future__ import annotations

import argparse
import os
import subprocess
from collections.abc import Mapping
from pathlib import Path

ENV_VAR = "COH_SOURCE_ROOT"
REPO_ROOT = Path(__file__).resolve().parents[2]
SIBLING = "homecoming-source-material"


def main_checkout(repo: Path) -> Path:
    """The main checkout of the repository at ``repo``; ``repo`` itself outside a git repository."""
    try:
        common = subprocess.run(
            ["git", "rev-parse", "--path-format=absolute", "--git-common-dir"],
            cwd=repo,
            capture_output=True,
            text=True,
            check=True,
        ).stdout.strip()
    except OSError, subprocess.CalledProcessError:
        return repo
    return Path(common).parent


def default_root() -> Path:
    """``../homecoming-source-material`` beside the main checkout."""
    return main_checkout(REPO_ROOT).parent / SIBLING


def add_source_root_argument(parser: argparse.ArgumentParser) -> None:
    """Add ``--source-root`` to ``parser``."""
    parser.add_argument(
        "--source-root",
        type=Path,
        default=None,
        metavar="PATH",
        help=f"the private source-material checkout (default ${ENV_VAR}, else {SIBLING} beside the main checkout)",
    )


def source_root(flag: Path | None = None, env: Mapping[str, str] | None = None) -> Path:
    """Resolve the root, or exit with E04 when it is not a directory."""
    environ = os.environ if env is None else env
    root = flag or (Path(environ[ENV_VAR]) if environ.get(ENV_VAR) else default_root())
    if not root.is_dir():
        found = "exists but is not a directory" if root.exists() else "was not found"
        raise SystemExit(f"E04: expected a directory at {root}, which {found}; set --source-root or {ENV_VAR}")
    return root
