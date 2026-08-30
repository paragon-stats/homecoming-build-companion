"""CLI entry point: ``python -m coh_engine``.

The registries are code; this is the thin execution surface a build run touches
(.claude/rules/check-registry.md § Execution during build). ``rules`` lists the
registered legality rules, hard-limit rules, and scoring metrics — the introspectable
registries. ``validate`` runs the legality + hard-limits registries over a build loaded
from a Mids-dump directory and reports diagnostics (text, or ``--format=json``), exiting
per the :mod:`coh_engine.diagnostics` severity contract. ``score`` (the metric registry)
lands with the build-eval pipeline that wires the DTO loader to the engine modules.
"""

import argparse
import json
import sys
from collections.abc import Sequence
from pathlib import Path

from coh_engine.base_totals import load_server_data
from coh_engine.diagnostics import Diagnostic, exit_code, format_json, format_text
from coh_engine.effect import load_powers_effects
from coh_engine.enh_aspects import registered_aspect_handlers
from coh_engine.enhancement import load_build_slots, load_enhancement_effects
from coh_engine.hard_limits import check_hard_limits, registered_hard_limit_rules
from coh_engine.legality import check_build_legality, load_enhancement_legality, registered_rules
from coh_engine.levels import load_level_schedule
from coh_engine.scoring import registered_metrics
from coh_engine.set_bonuses import load_set_bonus_db


def _print_rules() -> None:
    print("legality rules (a new dimension is a new @legality_rule):")
    for name in registered_rules():
        print(f"  {name}")
    print("hard-limit rules (a new limit is a new @hard_limit_rule):")
    for name in registered_hard_limit_rules():
        print(f"  {name}")
    print("enhancement aspect handlers (a new aspect is a new @_register):")
    for name in registered_aspect_handlers():
        print(f"  {name}")
    print("scoring metrics (a new metric is a new @metric):")
    for name in registered_metrics():
        print(f"  {name}")


def run_validate(build_dir: Path, mids_dir: Path) -> list[Diagnostic]:
    """Load a dumped build and run the legality + hard-limits registries over it.

    ``build_dir`` holds ``powers_effects.json`` + ``slots.json`` (a harness build dump);
    ``mids_dir`` holds the shared DB dumps (``enhancements.json``, ``enhancement_sets.json``,
    ``set_bonus_powers.json``, ``levels.json``, ``server_data.json``, ``enhancement_effects.json``).
    The two registries both fold over the loaded build; the result is their combined diagnostics.

    ``enhancement_effects.json`` is loaded and passed through to the legality registry
    because P-SET-001's pure-proc exemption is keyed on it: a piece with no
    ``Enhancement``-mode effects is a proc that works alone and must not be reported as a
    lone set piece. Without it the rule degrades silently, warning on every pure proc a
    real build slots by itself (Performance Shifter: Chance for +Endurance, Force Feedback:
    Chance for +Recharge, Achilles' Heel: Chance for Resistance Debuff). The set-global and
    unique signals do not need it, so the failure is partial, which is what made it hard to
    notice.
    """
    powers = load_powers_effects(build_dir / "powers_effects.json")
    slots = dict(load_build_slots(build_dir / "slots.json"))
    enh_legality = load_enhancement_legality(mids_dir / "enhancements.json")
    set_db = load_set_bonus_db(mids_dir / "enhancement_sets.json", mids_dir / "set_bonus_powers.json")
    schedule = load_level_schedule(mids_dir / "levels.json")
    server = load_server_data(mids_dir / "server_data.json")
    enh_effects = dict(load_enhancement_effects(mids_dir / "enhancement_effects.json"))
    return [
        *check_hard_limits(powers, slots, schedule, server.max_slots),
        *check_build_legality(powers, slots, enh_legality, set_db, enh_effects),
    ]


def _emit(diagnostics: list[Diagnostic], as_json: bool) -> int:
    if as_json:
        print(json.dumps(format_json(diagnostics), indent=2))
    else:
        print(format_text(diagnostics) if diagnostics else "no violations")
    return exit_code(diagnostics)


def main(argv: Sequence[str] | None = None) -> int:
    """Dispatch the CLI. ``rules`` introspects the registries; ``validate`` runs them."""
    parser = argparse.ArgumentParser(prog="coh_engine", description="MidsReborn build-math port.")
    sub = parser.add_subparsers(dest="command")
    sub.add_parser("rules", help="list the registered legality/hard-limit rules + metrics")
    v = sub.add_parser("validate", help="run the legality + hard-limits registries over a dumped build")
    v.add_argument("build_dir", type=Path, help="directory with powers_effects.json + slots.json")
    v.add_argument(
        "--mids-dir", type=Path, default=None, help="directory with the shared DB dumps (default: build_dir/../..)"
    )
    v.add_argument("--format", choices=("text", "json"), default="text", help="output format")
    args = parser.parse_args(argv)

    if args.command == "rules":
        _print_rules()
        return 0
    if args.command == "validate":
        mids_dir = args.mids_dir or args.build_dir.parent.parent
        return _emit(run_validate(args.build_dir, mids_dir), args.format == "json")
    print("coh_engine: MidsReborn build-math port.")
    print("Spec: docs/engine/mids-port-spec.md. Commands: rules, validate (score lands with the build-eval pipeline).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
