# Sample builds end to end: read, write, validate, keep

One pull request closing five issues in `paragon-stats/homecoming-build-companion`, worked in this order: #43 (`.mxd`
single-line parse), #47 (Arachnos Soldier patron-pool record), #44 (`.mbd` writer invariants and round trip), #45
(`validate` accepts a `.mbd`), #42 (sample `.mbd` builds as validation fixtures).

## Problem

The engine can read a `.mbd`, write one, and validate one, but not the same build through all three. The legacy `.mxd` reader
rejects every real single-line file; the writer emits two fields MidsReborn never writes (`IoLevel` 50, `Level` "50") and passes
`PowerSets` through unchecked; `validate` runs only on a directory the C# Mids dump harness produced, so no generated or
hand-authored `.mbd` can be checked by the project's own validator; and the real sample builds that would catch all of this sit
in an untracked git stash. The cost is builds handed to the operator that MidsReborn crashes on or renders with large negative
defence and resistance.

## Evidence

- Issue #44: two generated builds failed in MidsReborn during authoring, one with `IndexOutOfRangeException` at
  `Character.Reset` (7-entry `PowerSets`), one with every typed defence and resistance negative (`IoLevel` above the set's ceiling).
- Issue #43: all 131 extracted Bane/Huntsman `.mxd` files fail the "header line and at least one payload line" check; Mids'
  `||` → `|\n|` normalization made all 131 parse.
- Issue #47: 47% of 132 sample Bane builds take a patron pool, led by Mace Mastery (42), while the archetype record says the
  archetype has no epic pool.
- Measured at `4aa2fb23`: the writer emits `IoLevel` 50 and `Level` "50" on every piece; MidsReborn stores both 0-based and raw,
  so every Mids-saved sample build carries `IoLevel` 49 (or 19/29/34/39 for lower-ceiling sets, 1 for Hamidon) and `Level` "49".
  The writer-produced builds in `samples/builds/generated/` carry 50 on 672 slots.
- Measured: no dump of the powers database exists in the repository; the harness exports only a `StaticIndex` to `FullName`
  map, and the per-power fields the validator needs come from a per-build dump. The dump fixtures carry no per-set level bounds;
  canonical `boost_sets` does (`min_level`, `max_level`).
- Measured: `epic_pool_category` is null for every archetype record, not only the two VEATs, and nothing in the code reads it.
- Measured: `stash@{0}` (on `feat/engine-incarnate-and-chain`) exists, untracked files only: 3,133 `.mxd`, 111 `.mbd`
  (23 operator, 88 archive), 6 `.mxdm`, 1 `.png`. The main checkout holds 19 more `.mbd` outside the stash (4 operator,
  3 archive, 12 generated).

## Users

- **Primary**: the build author (a Claude session or the operator) emitting a `.mbd` for MidsReborn and checking it before it is
  loaded; the engine maintainer who needs real fully-slotted builds as regression fixtures.
- **Not for**: forum readers of the markdown view; per-build Mids numeric parity.

## Hypothesis

We believe **a reader, writer and validator that agree on the `.mbd` format, exercised against real sample builds** will
**stop format defects reaching MidsReborn** for **build authors and the engine maintainer**.
We'll know we're right when **every committed sample build parses, round-trips through the model unchanged, and
`validate <build.mbd>` reports a 7-entry `PowerSets` and an out-of-range `IoLevel` without a harness dump**.

## Success Metrics

| Metric | Target | How measured |
| --- | --- | --- |
| Single-line `.mxd` parse | 100% of committed `.mxd` fixtures, single-line and multi-line, no parity regression | `test_mxd*.py` |
| `.mbd` round trip | parse → write → parse equal for every committed sample `.mbd` | round-trip test |
| Writer refusals | each invariant violation (`PowerSets` arity, `IoLevel`, `Level`, inherents) refused with an E-code | writer tests |
| Per-set `IoLevel` | 19 Achilles' Heel; 29 Kismet, Steadfast Protection; 39 Miracle, Touch of Death; 49 Shield Wall, Panacea | writer tests |
| `validate <build.mbd>` | runs without a harness dump; `H-FMT-001..003` reported; directory path unchanged; JSON output | CLI tests |
| Sample fixtures | one build per archetype available (12); each loads and computes, every failure listed, none dropped | end-to-end test |
| Quality gates | `pytest --cov=coh_engine --cov-fail-under=100`, ruff, mypy clean; failing test first | CI |

## Scope

**MVP** — the five issues' acceptance criteria as written, except the items under Open Questions that need a decision first.

**Out of scope** — deferred, with the reason:

- Full per-build Mids numeric parity for the sample builds — #42 allows the minimum load-and-compute form.
- Committing the 3,133 legacy `.mxd` files or all 111 archive `.mbd` — a curated subset lands.
- The `.mxd` → `.mbd` conversion — #22.
- Archetype-class gating of powers in the validator — not modelled today; no issue asks for it.
- Any change to the Mids-faithful numeric port — check-registry.md fidelity boundary.

## Delivery Milestones

<!-- Business outcomes, not engineering tasks. /plan turns each into a plan. -->
<!-- Status: pending | in-progress | complete -->

| # | Milestone | Outcome | Status | Plan |
| --- | --- | --- | --- | --- |
| 1 | #43 `.mxd` single-line share blocks parse | A real one-line `.mxd` parses to the same model as its multi-line form; banner-prefixed and multi-line forms unchanged | complete | `.claude/plans/mbd-sample-builds.plan.md` |
| 2 | #47 Arachnos Soldier patron pools | The archetype record reflects the four VEAT patron pools (or states why null), Widow checked, a Mace Mastery build validates clean | complete | `.claude/plans/mbd-sample-builds.plan.md` |
| 3 | #44 `.mbd` writer invariants and round trip | Writer emits what MidsReborn writes (8 `PowerSets`, per-set `IoLevel`, 0-based `Level`, inherents) and refuses otherwise; every sample `.mbd` round-trips | complete | `.claude/plans/mbd-sample-builds.plan.md` |
| 4 | #45 `validate` accepts a `.mbd` | `python -m coh_engine validate build.mbd` folds the existing registries plus `H-FMT-001..003` over a bare `.mbd` | complete | `.claude/plans/mbd-sample-builds.plan.md` |
| 5 | #42 sample builds as fixtures | Curated sample `.mbd` builds committed (12, one per archetype available); an end-to-end pass loads and computes each and lists failures | in-progress | `.claude/plans/mbd-sample-builds.plan.md` |

## Open Questions

Decided by the user on 2026-10-27; the plan's "Decisions" section carries the evidence and the tasks each answer lands in.

- [x] #43 "all 131 Bane/Huntsman `.mxd` files": commit the 131 as a fixture folder (read from `stash@{0}` with `git show`) and test that every one reads.
- [x] #47 record "or a comment": a `$comment` field (the `strategy/breakpoints.json` convention). JSONC would fail the `check-json`
      pre-commit hook and `endurance_budget.load_canonical_caps`, which both parse with Python `json`. The harness also exports
      `Archetype.Ancillary` for every class so the four VEAT pools are read from data, not hand-set.
- [x] #44/#45 `IoLevel` ceiling: a new harness export of each set's `LevelMin`/`LevelMax` (and each enhancement's, for generic IOs),
      the fields MidsReborn's own `CheckAndFixIOLevel` reads; one loader for the writer and `H-FMT-002`.
- [x] #45 `Chosen` absent from `.mbd`: accepted as a known limit. Each granted sub-power the samples carry is traced to its game change,
      repo patch history first (`data/diff/patch_history/`, `community/`, `docs/`), then release notes.
- [x] #42 layout and dumps: the 12 `.mbd` under the existing `samples/builds/` subfolders, plus their 12 Mids dump folders committed as fixtures
      under `src/coh_engine/tests/fixtures/mids/builds/` (changed 2026-10-27, about 8 MB).
- [x] #42 "load and compute within caps": met by the committed dumps; the end-to-end pass runs round trip + `validate` + totals computed from the dump within the archetype caps.
- [x] #44 required inherents: derived from the Mids DB through a new `InherentPowers` field on the harness `archetypes.json`
      (the per-archetype channel `coh_engine.archetypes` already reads), filtered the way `Build.AddAutomaticGrantedPowers` fills a new build.
- [x] Curated subset as listed; Defender, Peacebringer, Warshade recorded as gaps in `samples/builds/README.md`; the stash stays for #22.

## Risks

| Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- |
| One harness run is needed and the Mids fork must stay pristine | high | medium | Apply `gbpa-sequential.patch`, dump once, restore `clsToonX.cs`; the operator runs it from the plan's exact commands |
| Deriving picks from a `.mbd` misclassifies auto-granted sub-powers | medium | low | Document the limit; the pick count may over-count by one on Sorcery builds |
| Fixture size growth if dumps are committed | medium | low | Curated subset only; decide before committing |
| Round trip exposes fields the reader drops | medium | medium | Compare parsed models; extend the model only where a sample proves a loss |
| The shared stash is popped or dropped by another session | low | high | Read files with `git show 'stash@{0}^3:<path>'`; never pop |

---
*Status: PLANNED — implementation plan at `.claude/plans/mbd-sample-builds.plan.md` (all five milestones).*
