# Rule: Extensible check registries — a new dimension is a new rule, not a dispatcher edit

**Status:** MUST. Applies to every validation, diagnostic, and strategy-scoring layer in
this project — legality, the hard-limits validator, goal scoring, cap and endurance
adherence, and the per-aspect enhancement handlers. It does **not** apply to the
Mids-faithful numeric port (see the fidelity boundary below).

This is the governing architecture for the engine's evaluation layers. It was the
intended shape from the start; it is stated here so it is enforced, not re-discovered.

## The guiding principle

> **A new dimension = write a rule + `@rule`. No editing the dispatcher, no touching the
> others.**

Adding a legality rule, a scoring metric, a cap check, or an enhancement-aspect handler
is authoring one self-contained function and registering it. It never means editing a
central `if/elif` ladder, a `switch`, or the other rules. If a change to one dimension
forces an edit to the dispatcher or to a sibling rule, the layer is built wrong.

## The pattern

Three parts. Every registry-backed layer has the same shape.

### 1. A pre-built context

Build the shared inputs once, up front, and pass them to every rule. The context does
the walking, resolving, and precomputing that all rules need (e.g. the filled
`(power, slot, enhancement)` triples for legality), so no rule re-derives it and no two
rules walk the same data twice.

```python
@dataclass(frozen=True)
class LegalityContext:
    powers: ...
    slots: ...
    enh_db: ...
    set_db: ...
    filled: Sequence[tuple[Power, SlotRef, EnhancementLegality]]   # precomputed once
```

### 2. Rules as pure functions, registered by decorator

A rule is `(Context) -> Iterable[Diagnostic]`. It is pure — no shared mutable state — and
it names its own rule ID(s). Registration is a decorator; the registry is a plain list.

```python
Rule = Callable[[Context], Iterable[Diagnostic]]
_RULES: list[Rule] = []

def rule(fn: Rule) -> Rule:
    _RULES.append(fn)
    return fn

@rule
def per_power_set_duplicate(ctx: Context) -> Iterable[Diagnostic]:
    ...
```

### 3. A runner that composes, never decides

The public entry point builds the context and folds every registered rule over it. It
contains no per-dimension logic — adding a rule changes its behaviour without changing
its body.

```python
def check_build_legality(...) -> list[Diagnostic]:
    ctx = LegalityContext(..., filled=_build_filled(...))
    return [d for r in _RULES for d in r(ctx)]
```

## Classify once — one decision, many consumers

When a decision is consumed in more than one way (a bool predicate, a diagnostic, a
fail-fast guard), compute it **once** as a typed verdict and let every consumer read that
verdict. Never re-derive the same decision in a second place, or the tested copy and the
production copy drift apart.

```python
class PlacementVerdict(Enum):
    VALID; UNKNOWN_ENH; UNKNOWN_SET; SET_NOT_ACCEPTED; CLASS_NOT_ACCEPTED

def classify_placement(power, enh, set_db) -> PlacementVerdict: ...      # the one source
def is_enhancement_valid(power, enh, set_db) -> bool:                    # predicate
    return classify_placement(power, enh, set_db) is PlacementVerdict.VALID
# the placement rule maps the verdict -> Diagnostic; a guard raises on the first non-VALID
```

The shared *primitive* underneath a rule (e.g. `power_accepts_set`) stays single-sourced
too; wrappers (raise / bool / diagnostic) consume the primitive, they do not re-encode it.

## Assert against engine output, not re-derived thresholds

A consumer that checks a build against a limit the engine already applied must compare
against the engine's **own computed output**, never re-derive the threshold. Diff
`Totals` against `TotalsCapped` to find over-cap fields; do not recompute `cap - 1`
expressions that must then be kept in lockstep with the capping code. The same discipline
applies anywhere a layer would restate a number the engine produces.

## The fidelity boundary — what stays a literal port

The Mids-faithful numeric port is **out of scope** and must not be registry-ized:
`base_totals` buff/enhancement routing (`_route_buff_effect`, `_route_enhance_main`), the
effect-to-aspect map (`_enhance_aspect`), and the comparison/branch switches that mirror
C# `switch` **order**. Their entire value is being a byte-faithful transcription of
MidsReborn; generalizing them trades the fidelity guarantee (the whole point of the port)
for modularity the numeric core does not need. Keep them literal.

## Where the pattern applies (beneficiary map)

| Layer | Registry keyed on | Status |
| --- | --- | --- |
| Legality (`legality.py`) | placement + build-wide rules | adopted (the reference implementation) |
| Goal-scoring metrics (`scoring.py`) | metric name (from `build_profiles.json`) | adopted |
| Cap / endurance adherence (`scoring.py`) | adherence check | adopted (cap check diffs Totals vs TotalsCapped) |
| Hard-limits validator (`P8` pre-flight: slots, picks, pools, tiers) | limit rule | **adopt at birth** — do not grow an `if/elif` |
| Per-aspect enhancement handlers (recharge, end, acc, range, interrupt, damage) | `eEnhance` aspect | planned central module; `GlobalEnhance` reserves the slots |
| Optimizer (`CP9`) scoring inner loop | reuses the metric + adherence registries | must consume, not re-implement |

## Execution during build (skills / CLI)

The registries are code (testable, 100% covered); the **orchestration** is the surface a
build run touches. Expose them through `python -m coh_engine`:

- `validate <build>` — runs the legality + hard-limits rule registries → diagnostics
  (text by default, `--format=json` per [`error-output.md`](#diagnostic-contract-summary)).
- `score <build> --profile <name>` — runs the metric + adherence registries → the profile
  score + warnings.

A Claude Code skill (e.g. `/validate-build`) is a thin wrapper over those subcommands for
the interactive workflow; the logic stays in the registries so it is unit-tested. Skills
wrap; code decides. Adding a rule needs no skill or CLI change — the runner already folds
it in.

## Reference

- [`error-output.md`](#diagnostic-contract-summary) — the `Diagnostic` contract every rule emits.
- [`hard-limits.md`](build-rules/hard-limits.md) — the invariants the hard-limits validator registers.
- [`build-creation.md`](build-rules/build-creation.md) — the P8 pre-flight checklist the validator mechanizes.
- [`python.md`](../repository-standards/style-guides/python-style-guide.md) — pure check functions `(build, context) -> list[Violation]`.

## Diagnostic contract summary

The `Diagnostic` contract every rule above emits. The full guide is [error-output-style-guide.md](../repository-standards/style-guides/error-output-style-guide.md).

**Status:** MUST. Applies to every error message, warning, log line, validator violation, or user-facing diagnostic emitted by tools in this project.

A useful error message lets the reader fix the problem without re-running the tool to gather more context. A useless error message just signals that something failed.

### Required content

Every diagnostic message includes:

1. **Rule ID or error code** — stable identifier the reader can search and cite. Format: `H-<DOMAIN>-<NN>` for hard rules, `P-<DOMAIN>-<NN>` for preference rules, `E<NN>` for non-rule tool errors.
2. **What failed** — the specific check or operation that did not succeed, in concrete terms.
3. **Expected vs. actual** — what the tool needed and what it found, both as concrete values.
4. **Location** — file path, line number, power name, slot level, or whatever pinpoints where the problem is.
5. **Smallest concrete corrective action** — one sentence telling the reader what to change.

### Format

Diagnostic output is **machine-parseable structured first**, human-readable derived. Default machine format:

```text
{path}:{line}:{column}: {severity}: {rule_id}: {message}
```

Examples:

```text
build.json:42:1: error: H-SLOT-001: total added slots (74) exceeds cap (67); remove 7 slots from one or more powers
build.json:18:1: warning: P-SLOT-001: attack "Crushing Blow" slot 2 has Dmg/Rchg piece; expected an Acc-bearing piece
build.json:91:1: warning: P-IO-002: Steadfast Protection: Resistance/+Defense unique not slotted; expected in any resistance toggle
```

The validator's `__main__.py` accepts `--format=json` to emit structured violations:

```json
{
  "rule_id": "H-SLOT-001",
  "severity": "error",
  "path": "build.json",
  "line": 42,
  "column": 1,
  "message": "total added slots (74) exceeds cap (67)",
  "fix": "remove 7 slots from one or more powers",
  "expected": 67,
  "actual": 74
}
```

### Severity levels

- **error** — invariant violation. Build is invalid until fixed. Validator exits 1.
- **warning** — preference violation. Build is valid but suboptimal. Validator exits 2 if no errors and any warnings; exits 0 if no warnings either.
- **info** — observation, no action required. Reserved for stats and summaries.

Severity escalation rule: a fixable preference miss is a **warning**.
A claim that doesn't match content (e.g. `build_goal: soft-cap-defense` but build only reaches 38% defense) is a warning unless explicitly upgraded by user-configured strictness.

### Tone

- Direct factual statements only.
- No emojis, no exclamation marks, no praise tokens, no "perhaps" or "maybe" hedging unless expressing genuine uncertainty.
- "Found 3 violations." — yes. "Oops! Looks like there might be a few issues!" — no.
- "Slot 3 of 'Crushing Blow' has no enhancement." — yes. "It seems the slot might be empty." — no.

When uncertain about a diagnostic (e.g. canonical data is missing the expected field), say so explicitly:

```text
build.json:54:1: info: E07: cannot verify P-GOAL-001 (soft-cap-defense reach) — build math support not yet implemented
```

### Suppression

Tools accept `--ignore-rule=<rule_id>` and `--ignore-rule=<rule_id>:<line>` for targeted suppression. Suppression in source files uses comments at the violation site:

```text
# noqa: P-SLOT-001  -- Build Up: snipe accuracy already met via Tactics + Kismet
```

The reason is **required** after `--`. A bare `noqa: P-SLOT-001` is rejected.

### Diagnostic contract references

- [`docs/repository-standards/style-guides/error-output-style-guide.md`](../repository-standards/style-guides/error-output-style-guide.md)
  — full canonical error-output style guide imported from kloehnwars-homelab.
  This rule is the auto-loaded summary; that doc is the SSOT.
- [`python.md`](../repository-standards/style-guides/python-style-guide.md) — Python conventions for tools that emit diagnostics.
