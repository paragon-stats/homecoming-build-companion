"""Where the forum source material lives: one resolver shared by every CLI that reads or writes captures.

The captures sit in a private checkout beside this repository. The root is, in order, the
``--source-root`` flag, the ``COH_SOURCE_ROOT`` variable, then ``../homecoming-source-material``
relative to this repository.

Errors: E04 the resolved root is not a directory.
"""

from __future__ import annotations

import argparse
import os
from collections.abc import Mapping
from pathlib import Path

ENV_VAR = "COH_SOURCE_ROOT"
REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_ROOT = REPO_ROOT.parent / "homecoming-source-material"


def add_source_root_argument(parser: argparse.ArgumentParser) -> None:
    """Add ``--source-root`` to ``parser``."""
    parser.add_argument(
        "--source-root",
        type=Path,
        default=None,
        metavar="PATH",
        help=f"the private source-material checkout (default ${ENV_VAR}, else {DEFAULT_ROOT})",
    )


def source_root(flag: Path | None = None, env: Mapping[str, str] | None = None) -> Path:
    """Resolve the root, or exit with E04 when it is not a directory."""
    environ = os.environ if env is None else env
    root = flag or (Path(environ[ENV_VAR]) if environ.get(ENV_VAR) else DEFAULT_ROOT)
    if not root.is_dir():
        raise SystemExit(f"E04: expected a directory at {root}; set --source-root or {ENV_VAR}")
    return root
