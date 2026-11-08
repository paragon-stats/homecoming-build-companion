"""Structured diagnostics — the ``error-output.md`` machine-first output standard.

Every legality/validation finding is a :class:`Diagnostic`: a stable rule ID, a
severity, a concrete message, and a location. Diagnostics render both the
machine-parseable text line and the ``--format=json`` object, so a tool can emit
either without the finding sites re-deriving the format.

The severity contract (``error-output.md`` § Severity levels):

- ``error`` — invariant violation; the build is invalid. Any error -> exit 1.
- ``warning`` — preference violation; valid but suboptimal. Warnings without
    errors -> exit 2.
- ``info`` — observation, no action. Alone -> exit 0.

Spec: docs/style-guides/error-output.md.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from typing import Any

_SEVERITIES = ("error", "warning", "info")


@dataclass(frozen=True, slots=True)
class Diagnostic:
    """One finding, rendered per ``error-output.md``.

    ``expected``/``actual``/``fix`` are optional structured context: present in
    the JSON form only when set, absent from the text line (which carries just the
    message). ``path``/``line``/``column`` default to the whole-build anchor when
    the finding has no finer site.

    Raises:
        ValueError: ``E10`` if ``severity`` is not one of error/warning/info.
    """

    rule_id: str
    severity: str
    message: str
    path: str = "build.json"
    line: int = 1
    column: int = 1
    fix: str = ""
    expected: Any = None
    actual: Any = None

    def __post_init__(self) -> None:
        """Reject a severity outside the canonical set (fail loud, not silent)."""
        if self.severity not in _SEVERITIES:
            raise ValueError(
                f"E10: diagnostic severity {self.severity!r} is not one of {list(_SEVERITIES)}; "
                "use 'error', 'warning', or 'info'"
            )

    def format_text(self) -> str:
        """The machine-parseable line ``{path}:{line}:{column}: {severity}: {rule_id}: {message}``."""
        return f"{self.path}:{self.line}:{self.column}: {self.severity}: {self.rule_id}: {self.message}"

    def to_json(self) -> dict[str, Any]:
        """The ``--format=json`` object: required keys always, optional context when set."""
        obj: dict[str, Any] = {
            "rule_id": self.rule_id,
            "severity": self.severity,
            "path": self.path,
            "line": self.line,
            "column": self.column,
            "message": self.message,
        }
        if self.fix:
            obj["fix"] = self.fix
        if self.expected is not None:
            obj["expected"] = self.expected
        if self.actual is not None:
            obj["actual"] = self.actual
        return obj


def format_text(diagnostics: Sequence[Diagnostic]) -> str:
    """Join a run's diagnostics into one newline-separated text block, in order."""
    return "\n".join(d.format_text() for d in diagnostics)


def format_json(diagnostics: Sequence[Diagnostic]) -> list[dict[str, Any]]:
    """The ``--format=json`` array for a run's diagnostics, in order."""
    return [d.to_json() for d in diagnostics]


def exit_code(diagnostics: Sequence[Diagnostic]) -> int:
    """Standard exit code: any ``error`` -> 1, else any ``warning`` -> 2, else 0."""
    severities = {d.severity for d in diagnostics}
    if "error" in severities:
        return 1
    if "warning" in severities:
        return 2
    return 0
