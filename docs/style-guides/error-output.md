# Rule: Diagnostic and error-output standard

**Status:** MUST. Applies to every error message, warning, log line, validator violation, or user-facing diagnostic emitted by tools in this project.

A useful error message lets the reader fix the problem without re-running the tool to gather more context. A useless error message just signals that something failed.

## Required content

Every diagnostic message includes:

1. **Rule ID or error code** — stable identifier the reader can search and cite. Format: `H-<DOMAIN>-<NN>` for hard rules, `P-<DOMAIN>-<NN>` for preference rules, `E<NN>` for non-rule tool errors.
2. **What failed** — the specific check or operation that did not succeed, in concrete terms.
3. **Expected vs. actual** — what the tool needed and what it found, both as concrete values.
4. **Location** — file path, line number, power name, slot level, or whatever pinpoints where the problem is.
5. **Smallest concrete corrective action** — one sentence telling the reader what to change.

## Format

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

## Severity levels

- **error** — invariant violation. Build is invalid until fixed. Validator exits 1.
- **warning** — preference violation. Build is valid but suboptimal. Validator exits 2 if no errors and any warnings; exits 0 if no warnings either.
- **info** — observation, no action required. Reserved for stats and summaries.

Severity escalation rule: a fixable preference miss is a **warning**.
A claim that doesn't match content (e.g. `build_goal: soft-cap-defense` but build only reaches 38% defense) is a warning unless explicitly upgraded by user-configured strictness.

## Tone

- Direct factual statements only.
- No emojis, no exclamation marks, no praise tokens, no "perhaps" or "maybe" hedging unless expressing genuine uncertainty.
- "Found 3 violations." — yes. "Oops! Looks like there might be a few issues!" — no.
- "Slot 3 of 'Crushing Blow' has no enhancement." — yes. "It seems the slot might be empty." — no.

When uncertain about a diagnostic (e.g. canonical data is missing the expected field), say so explicitly:

```text
build.json:54:1: info: E07: cannot verify P-GOAL-001 (soft-cap-defense reach) — build math support not yet implemented
```

## Suppression

Tools accept `--ignore-rule=<rule_id>` and `--ignore-rule=<rule_id>:<line>` for targeted suppression. Suppression in source files uses comments at the violation site:

```text
# noqa: P-SLOT-001  -- Build Up: snipe accuracy already met via Tactics + Kismet
```

The reason is **required** after `--`. A bare `noqa: P-SLOT-001` is rejected.

## Reference

- [`docs/repository-standards/style-guides/error-output-style-guide.md`](../repository-standards/style-guides/error-output-style-guide.md) — full canonical error-output style guide imported from kloehnwars-homelab.
  This rule is the auto-loaded summary; that doc is the SSOT.
- [`python.md`](../repository-standards/style-guides/python-style-guide.md) — Python conventions for tools that emit diagnostics.
- The anti-sycophancy posture this rule mirrors.
