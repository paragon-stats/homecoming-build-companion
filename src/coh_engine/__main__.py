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
from collections.abc import Callable, Sequence
from pathlib import Path

from coh_engine.base_totals import load_server_data
from coh_engine.chain_report import (
    MOVES,
    build_chain_setup,
    parse_assumptions,
    parse_target,
    run_chain,
    stance_to_json,
    stance_to_text,
)
from coh_engine.damage_model import Stance
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


def _guarded(run: Callable[[], int]) -> int:
    """Run a subcommand; an input error prints one ``error:`` line on stderr and exits 1, with no traceback.

    A ``ValueError`` already carries its ``E<NN>`` message. A ``KeyError`` or ``OSError`` is an input whose
    shape or location is wrong, reported as ``E72``.
    """
    try:
        return run()
    except ValueError as error:
        message = str(error)
    except OSError as error:
        where = error.filename or "an input file"
        message = f"E72: cannot read {where}: {error.strerror or error}; check that the path exists and is readable"
    except KeyError as error:
        message = f"E72: an input is missing the key {error}; re-dump the build or check the data matches its schema"
    print(f"error: {message}", file=sys.stderr)
    return 1


def run_chain_command(args: argparse.Namespace) -> int:
    """Run the ``chain`` subcommand; input errors print to stderr and exit 1 (see :func:`_guarded`)."""
    return _guarded(lambda: _chain(args))


def _chain(args: argparse.Namespace) -> int:
    setup = build_chain_setup(
        args.build_dir,
        mids_dir=args.mids_dir or args.build_dir.parent.parent,
        clicksoff_dir=args.clicksoff_dir,
        canonical=args.canonical,
        mids_db=args.mids_db,
        scenario=parse_target(args.target),
        move=args.move,
        standoff_ft=args.standoff,
        assumptions=parse_assumptions(args.assume),
    )
    stances = list(Stance) if args.stance == "all" else [Stance(args.stance)]
    forms = None if args.form is None else [args.form]
    results = run_chain(setup, stances, path=str(args.build_dir), forms=forms)
    if args.format == "json":
        print(json.dumps([stance_to_json(setup, r) for r in results], indent=2))
    else:
        print("\n\n".join(stance_to_text(setup, r) for r in results))
    return exit_code([d for r in results for d in r.diagnostics])


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
    c = sub.add_parser("chain", help="plan the attack chain and layered damage of a dumped build")
    c.add_argument("build_dir", type=Path, help="Mids dump build directory")
    c.add_argument("--mids-dir", type=Path, default=None, help="shared DB dumps (default: build_dir/../..)")
    c.add_argument("--clicksoff-dir", type=Path, default=None, help="clicks-off dump (default: <build_dir>_clicksoff)")
    c.add_argument("--canonical", type=Path, default=None, help="canonical data root (default: data/canonical)")
    c.add_argument("--mids-db", type=Path, default=None, help="directory with AttribMod.json (needed for pet builds)")
    c.add_argument("--stance", choices=(*(s.value for s in Stance), "all"), default="all", help="stance to plan")
    c.add_argument("--form", default=None, help="plan one form only ('' is the base form); default: every form")
    c.add_argument("--target", default="single", help="'single' or 'spawn:N'")
    c.add_argument("--move", choices=MOVES, default="run", help="movement mode used to cross the standoff")
    c.add_argument("--standoff", type=float, default=None, metavar="FT", help="ranged standoff in feet")
    c.add_argument("--assume", action="append", default=[], metavar="KEY=VALUE", help="override an assumption")
    c.add_argument("--format", choices=("text", "json"), default="text", help="output format")
    args = parser.parse_args(argv)

    if args.command == "rules":
        _print_rules()
        return 0
    if args.command == "validate":
        mids_dir = args.mids_dir or args.build_dir.parent.parent
        return _guarded(lambda: _emit(run_validate(args.build_dir, mids_dir), args.format == "json"))
    if args.command == "chain":
        return run_chain_command(args)
    print("coh_engine: MidsReborn build-math port.")
    print(
        "Spec: docs/engine/mids-port-spec.md. Commands: rules, validate, chain (score lands with the build-eval pipeline)."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
