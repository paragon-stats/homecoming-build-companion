"""Withhold rows that name a person the public repository must not name.

The names come from ``COH_BANNED_NAMES`` (comma-separated), the source the CI operator-name
check reads, and match as whole words, case-insensitively, as that check does. A row is judged
on its serialised text, so a name in a title, a path or a URL all count. The capture stays in
the private source repository; only the public row is left out, and nothing about a withheld
row is printed or kept here but the count.
"""

from __future__ import annotations

import json
import os
import re
from collections.abc import Iterable
from typing import Any

ENV_VAR = "COH_BANNED_NAMES"


def banned_pattern() -> re.Pattern[str] | None:
    """The whole-word pattern for the names in the environment, or ``None`` when none are set."""
    names = [n.strip() for n in os.environ.get(ENV_VAR, "").split(",") if n.strip()]
    if not names:
        return None
    return re.compile(r"\b(?:" + "|".join(re.escape(n) for n in names) + r")\b", re.IGNORECASE)


def withhold(rows: Iterable[dict[str, Any]], pattern: re.Pattern[str] | None) -> tuple[list[dict[str, Any]], int]:
    """``(kept rows, number withheld)``; nothing is withheld without a pattern."""
    rows = list(rows)
    if pattern is None:
        return rows, 0
    kept = [r for r in rows if not pattern.search(json.dumps(r, ensure_ascii=False))]
    return kept, len(rows) - len(kept)


def report(pattern: re.Pattern[str] | None, withheld: int) -> str:
    """The one status line for a run."""
    if pattern is None:
        return f"name filter off: {ENV_VAR} is unset, no row withheld"
    return f"{withheld} row(s) withheld by the name filter"
