# Plan: Sample builds end to end — read, write, validate, keep

**Source PRD**: `.claude/prds/mbd-sample-builds.prd.md`
**Selected Milestones**: all five, in PRD order — #43, #47, #44, #45, #42 (one pull request)
**Complexity**: Large

## Summary

Make the `.mbd` reader, writer and validator agree on the format and prove it against real sample builds.
The `.mxd` reader accepts single-line files, the writer emits what MidsReborn writes and refuses what it does not,
`validate` accepts a bare `.mbd`, and twelve curated sample builds plus 131 legacy `.mxd` files become fixtures.
One Mids dump harness run supplies every new reference field (patron pools, inherent powers, set level limits, a powers table).
The MidsReborn fork is patched only for that run and restored afterwards.

## Decisions

The PRD's open questions, answered by the user on 2026-10-27. Tasks that depend on an answer carry its number.

| # | Question | Decision | Where it lands |
| --- | --- | --- | --- |
| 1 | #43: one single-line fixture, or the 131 Bane/Huntsman `.mxd`? | Commit all 131 (37 `Bane/` + 94 `Huntsman/` under the stash's `Soldier of Arachnos/`) and test that every one reads | T3 |
| 2 | #47: JSONC comment or a notes field? | A `$comment` field. JSONC fails `check-json` (`.pre-commit-config.yaml:103`, no exclude for `data/canonical/`; it is Python `json.load`) and `endurance_budget.load_canonical_caps` (`endurance_budget.py:93`, `json.loads`). `$comment` is the key `strategy/breakpoints.json` already uses | T6 |
| 3 | IoLevel ceiling source? | The Mids dump harness: `LevelMin`/`LevelMax` exported per set in `enhancement_sets.json` and per enhancement in `enhancements.json` — the two fields `Enhancement.CheckAndFixIOLevel` reads (`MidsReborn/Core/Enhancement.cs:345-358`). One loader feeds the writer and `H-FMT-002` | T7, T8, T14 |
| 4 | A `.mbd` cannot tell a granted sub-power from a pick | Accepted as a known limit (`chosen=True` for every entry; `H-POWER-001` may over-count on a Sorcery build). Each granted-power case is traced to its game change, repo sources first | T13, T16 |
| 5 | #42: commit dumps too? | Yes (changed after the plan was written). The single harness run (T12) also dumps the 12 curated sample builds; their dump folders (`enhanced_powers`, `incarnates`, `powers_effects`, `set_bonus_virtual_power`, `slots`, `totals`; LF line endings; about 8 MB) are committed under `fixtures/mids/builds/` beside the existing ones. The end-to-end test runs round trip + `validate` and also checks that the engine computes each build's totals without error and within the archetype's caps | T12, T17, T18 |
| 6 | #44 required inherents: hard-coded or derived? | Derived from the Mids DB through the dump: `archetypes.json` gains `InherentPowers` per class, computed the way `Build.AddAutomaticGrantedPowers` fills an empty build (`MidsReborn/Core/Build.cs:984-1021`). Pattern followed: the harness `archetypes.json` → `coh_engine.archetypes.Archetype` record (`archetypes.py:16-37`), the same per-archetype channel that carries the AT caps, so a proliferation or inherent change is a re-dump, not a code edit | T7, T8 |

## Patterns to Mirror

| Category | Source | Pattern |
| --- | --- | --- |
| Naming | `src/coh_engine/buildfile/mbd.py:161-170` | `BuildSpecError` carries `E<NN>:` + the level/power in the message |
| Registry | `src/coh_engine/hard_limits.py:150-162,172-185` | `@hard_limit_rule` pure functions; the runner only folds; `_error(rule_id, message, fix=, expected=, actual=)` |
| Shared primitive | `src/coh_engine/legality.py:94-125` | one `classify_*` decision consumed by the predicate, the diagnostic and the guard |
| Errors | `src/coh_engine/__main__.py:88-105` | `_guarded`: `ValueError` already carries its E-code; `OSError`/`KeyError` become `E72` |
| Diagnostics | `src/coh_engine/diagnostics.py:26-60` | `Diagnostic(rule_id, severity, message, fix, expected, actual)` |
| Loaders | `src/coh_engine/effect.py:184-230` | strict `r["Key"]` for fields the dump must carry; `.get` only for fields older dumps lack |
| Per-AT data | `src/coh_engine/archetypes.py:16-83` | one `Archetype` record per class from `archetypes.json`; `ArchetypeDb.nid_from_uid_class` |
| Harness export | `tools/mids-dump/Program.cs:1216-1239` (`DumpArchetypes`), `:907` (`DumpEnhancements`), `:1045` (`DumpEnhancementSets`) | anonymous-object projection + `WriteJson` |
| Harness build fields | `tools/mids-dump/Program.cs:608-676` | the per-power field set (`Level`, `SetTypes`, `Enhancements`, `Requires*`, `Chosen`) |
| Tests | `src/coh_engine/tests/test_hard_limits.py:33-72,75-92` | `_power`/`_slot` factories; one passing and one violating case per rule |
| Writer tests | `src/coh_engine/tests/test_mbd_writer.py:57-70,142-144` | `parse_mbd(write_mbd(...))` round trips; byte-exact tanker fixture |
| Sample discovery | `src/coh_engine/tests/test_mbd.py:10-24` | `REPO / "samples" / "builds"` rglob; every file must parse |
| Repo-relative defaults | `src/coh_engine/chain_report.py:72,354` | `REPO_ROOT = Path(__file__).resolve().parents[2]` |
| Comments in JSON | `strategy/breakpoints.json` top-level `$comment` | the only comment convention canonical-adjacent JSON uses |

## Files to Change

| File | Action | Why |
| --- | --- | --- |
| `src/coh_engine/buildfile/mxd.py` | UPDATE | one `_lines()` normalizing `\|\|` → `\|\n\|` for `classify` and `decode_share_block` |
| `src/coh_engine/tests/test_mxd.py` | UPDATE | single-line Red test |
| `src/coh_engine/tests/fixtures/mxd/legacy/arachnos-soldier/{bane,huntsman}/*.mxd` | CREATE (131) | decision 1 |
| `src/coh_engine/tests/test_mxd_parity.py` | UPDATE | every legacy file reads; failures listed, none dropped |
| `tools/mids-dump/Program.cs` | UPDATE | `Ancillary` + `InherentPowers` on archetypes; `LevelMin`/`LevelMax` on sets and enhancements; new `DumpPowers` |
| `tools/mids-dump/README.md` | UPDATE | Outputs table rows for the new fields and `powers.json` |
| `src/coh_engine/tests/fixtures/mids/{archetypes,enhancements,enhancement_sets,powers,version}.json` | UPDATE/CREATE | from the one harness run |
| `src/coh_engine/tests/fixtures/mids/builds/wpss_tanker_leadership*/` | UPDATE | Mids' load of the regenerated tanker file |
| `src/coh_engine/archetypes.py`, `tests/test_archetypes.py` | UPDATE | `ancillary`, `inherent_powers` fields + tests |
| `data/canonical/archetypes/arachnos_soldier.json`, `arachnos_widow.json` | UPDATE | `$comment` (decision 2) |
| `src/coh_engine/buildfile/mbd.py` | UPDATE | `MbdDb` loader, invariants + primitives, `dump_mbd`, writer fixes, `.mbd` → `Power`/`SlotRef` resolution |
| `src/coh_engine/tests/test_mbd_writer.py`, `test_mbd.py` | UPDATE | round trip over every sample; E73–E78; per-set IoLevel |
| `src/coh_engine/tests/fixtures/builds/wpss_tanker_leadership.mbd` | UPDATE | regenerated from `SPEC` by the fixed writer |
| `src/coh_engine/tests/wpss_tanker_spec.py` | UPDATE | drop inherents the writer now adds itself (keep the slotted ones) |
| `src/coh_engine/hard_limits.py`, `tests/test_hard_limits.py` | UPDATE | `MbdFormat` context field; `H-FMT-001..003` |
| `src/coh_engine/__main__.py`, `tests/test_main.py` | UPDATE | `validate <build.mbd>`; directory path untouched |
| `docs/slotting-and-leveling.md` | UPDATE | granted-power provenance table (decision 4) |
| `samples/builds/operator-builds/*.mbd`, `samples/builds/community/Mids Archives/<AT>/*.mbd` | CREATE (10 new; 2 already tracked) | decision 5 |
| `samples/builds/README.md` | CREATE | coverage table, the three missing archetypes, provenance |
| `src/coh_engine/tests/test_sample_builds.py` | CREATE | end-to-end pass over every sample `.mbd` |
| `.claude/prds/mbd-sample-builds.prd.md` | UPDATE | milestone rows `in-progress`; decisions recorded (done with this plan) |

## Execution order

Tasks are grouped by issue below, but three harness edits (T4, T7, T11) and two fixture preparations
(T10's regenerated tanker file, T17's curated samples) must precede the single harness run (T12).
Every consumer test that reads a new dump field goes Green only after T12.
Suggested order: T1–T3, T4, T7, T8–T10, T11, T17, **T12**, T5–T6, T13–T16, T18–T19.
Commit per issue as each goes Green; the harness commit may carry `Refs #44`, `Refs #45`, `Refs #47` together.

## Tasks

### Issue #43 — `.mxd` single-line share blocks

#### Task 1: Red — a single-line share block fails today

- **Action**: in `test_mxd.py`, build `single = header + "|" + payload + "|"` on one line from `_make_mxd_share` (`test_mxd.py:20-26`).
  Assert `classify(single) == "mxd"` and `decode_mxd(single) == decode_mxd(multi)`.
  Confirm it fails with `share block needs a header line and at least one payload line` (`mxd.py:95`).
- **Mirror**: `test_decode_share_block_needs_payload_line` (`test_mxd.py:62`).
- **Validate**: `pytest src/coh_engine/tests/test_mxd.py -k single -x` fails for that reason.

#### Task 2: Green — one normalization point

- **Action**: add `_lines(text) -> list[str]` in `mxd.py` that applies `text.replace("||", "|\n|")` then the existing `re.split(r"[\r\n]+", …)` filter.
  Call it from `classify` (`mxd.py:67`) and `decode_share_block` (`mxd.py:93`). No call-site normalization.
- **Mirror**: `MidsReborn/Core/MidsCharacterFileFormat.cs:1157,1254` (`ReadToEnd().Replace("||", "|\n|")`).
- **Validate**: `pytest src/coh_engine/tests/test_mxd.py src/coh_engine/tests/test_mxd_parity.py src/coh_engine/tests/test_mxd_reader.py` green (5-fixture parity unchanged).

#### Task 3: the 131 legacy fixtures read (decision 1)

- **Action**: list `git ls-tree -r --name-only 'stash@{0}^3:samples/builds/community/Mids Archives/Soldier of Arachnos'`.
  For each `Bane/*.mxd` and `Huntsman/*.mxd`, write it from Git Bash with
  `git show 'stash@{0}^3:<full path>' > 'src/coh_engine/tests/fixtures/mxd/legacy/arachnos-soldier/<bane|huntsman>/<name>'`.
  Keep names verbatim; the fixtures path is already excluded from the whitespace fixers (`.pre-commit-config.yaml:99-113`).
  Never pop or apply the stash.
  Add `test_every_legacy_mxd_reads` in `test_mxd_parity.py`: rglob the `legacy/` folder, assert 131 files,
  run `read_mxd(text, power_index, enh_index, ARCHETYPES)` on each, collect `(name, error)` for every failure,
  and assert the list is empty — all failures reported, none dropped.
  The existing `test_all_mxd_fixtures_match_their_mbd` globs `MXD_DIR.glob("*.mxd")` non-recursively (`test_mxd_parity.py:83`), so its `== 5` holds.
- **Mirror**: `test_all_mxd_fixtures_match_their_mbd` (`test_mxd_parity.py:78-88`) for index loading.
- **Validate**: `pytest src/coh_engine/tests/test_mxd_parity.py -k legacy`.
  If a file raises for a reason the decode fix does not cover (a pre-3.10 "legacy" field set, an unresolved StaticIndex), record it as a finding.
  The issue claims all 131 read after the normalization; confirm or disprove, do not skip silently.
- **Commits**: `fix(engine): parse single-line .mxd share blocks the way MidsReborn does` (`Refs #43`);
  `test(engine): keep the 131 Bane and Huntsman legacy .mxd sample builds as reader fixtures` (`Closes #43`).

### Issue #47 — Arachnos Soldier patron pools

#### Task 4: harness exports each class's ancillary pools

- **Action**: in `DumpArchetypes` (`Program.cs:1216-1239`) add `Ancillary = at.Ancillary.Select(i => db.Powersets[i].FullName).ToList()`.
  `Archetype.Ancillary` is `int[]` powerset indexes (`MidsReborn/Core/Base/Data_Classes/Archetype.cs:147`).
  The README row for `archetypes.json` names the field.
- **Mirror**: the `ClassType = at.ClassType.ToString()` projection in the same method.
- **Validate**: `dotnet build -c Debug` in `tools/mids-dump` (the run itself is T12).

#### Task 5: Red/Green — the record shows the four VEAT patron pools

- **Action**: `Archetype.ancillary: tuple[str, ...]` read strictly from `r["Ancillary"]` (`archetypes.py:60-80`).
  Tests in `test_archetypes.py`: `Class_Arachnos_Soldier` ancillary equals the four `Epic.VEAT_*` sets (Mace, Soul, Mu, Leviathan);
  `Class_Arachnos_Widow` the same; every `Playable` class has at least one ancillary set (the issue's "extend to every archetype" refactor).
  Red before T12 (the fixture lacks the key), Green after.
- **Mirror**: `load_archetypes` strict field reads; `conftest.py` `ArchetypeDb` fixtures.
- **Validate**: `pytest src/coh_engine/tests/test_archetypes.py`.

#### Task 6: canonical records carry the explanation (decision 2)

- **Action**: add to `arachnos_soldier.json` and `arachnos_widow.json`, directly after `"epic_pool_category": null`, a `"$comment"` string.
  Text: null in the game-data harvest for every archetype (not a VEAT defect); patron/ancillary availability is read from
  the Mids dump `archetypes.json` `Ancillary` field, which lists the four `Epic.VEAT_*` pools for both VEATs.
  Confirm readers tolerate the extra key: `load_canonical_caps` reads `attrib_max_max` only (`endurance_budget.py:93-94`);
  `check-json` parses fine; `scripts/*.ps1` reference `archetypes/` capture folders, not these files.
  Note in the commit body that `manifest.json` (`scripts/manifest.ps1`) records the harvest's sha256 and is left as the harvest snapshot.
- **Mirror**: `strategy/breakpoints.json` `$comment`.
- **Validate**: `pre-commit run check-json --all-files`; `pytest src/coh_engine/tests/test_endurance_budget.py`.
- **Criterion "a Mace Mastery build validates without an archetype-mismatch diagnostic"**: covered by T15's known-good sample,
  the Bane operator build (`PowerSets[7] == "Epic.VEAT_Mace_Mastery"`), which must report no `error`.
  No archetype-mismatch rule exists; the PRD keeps class gating out of scope.
- **Commits**: `feat(tools): export each archetype's ancillary pools from the Mids dump harness` (`Refs #47`);
  `fix(data): explain the null epic_pool_category on the VEAT records and read patron pools from the dump` (`Closes #47`).

### Issue #44 — `.mbd` writer invariants and round trip

The writer already exists at `mbd.py:148-387` (`write_mbd(spec, schedule)`, landed in 7d2b3ead).
The issue's `mbd_writer.py` / `test_cli.py` names predate it; no new module.

#### Task 7: harness exports the level limits and the inherent powers (decisions 3 and 6)

- **Action**: `DumpEnhancementSets` adds `t.s.LevelMin, t.s.LevelMax`; `DumpEnhancements` adds `t.e.LevelMin, t.e.LevelMax` (the InventO bounds).
  `DumpArchetypes` adds `InherentPowers`: for each class, every power of an `ePowerSetType.Inherent` powerset that
  `Build.AddAutomaticGrantedPowers` would add to an empty build of that class.
  That is: `IncludeFlag`, `InherentType != Prestige`, class requirement met (`Requires.ClassName`/`ClassNameNot` against the class),
  and no power requirement (`Requires.PowerID` empty; those are granted by a pick, such as Double Jump).
  Export each as `{FullName, Level}` (Mids adds at `power.Level`, so Rest lands at level 2 as the samples show). README rows updated.
- **Mirror**: `Build.cs:984-1021`; `DatabaseAPI.GetInherentPowerset` (`DatabaseAPI.cs:524`).
- **Validate**: `dotnet build -c Debug`.
  After T12, `archetypes.json` for `Class_Scrapper` lists Critical_Hit, Brawl, Sprint, Rest, Swift, Hurdle, Health, Stamina
  (compare with the Scrapper sample's `Inherent.*` entries).
  `Special_Set_Bonuses` is Mids' virtual set-bonus power — if the filter includes it, keep it; the samples carry it.

#### Task 8: Red/Green — invariants single-sourced; the writer emits what Mids writes

- **Action** in `mbd.py`:
  - `MbdDb` frozen dataclass (enhancement index by UID → nid/type/set, set bounds by set nid, `ArchetypeDb`) with
    `load_mbd_db(mids_dir)` reading `enhancements.json`, `enhancement_sets.json`, `archetypes.json`. The same bundle serves T13.
  - Primitives, each one function:
    `powersets_arity(power_sets) -> int` (expected 8: primary, secondary, "", pool×4, ancillary);
    `io_level_bounds(uid, db) -> tuple[int, int]` ported from `Enhancement.CheckAndFixIOLevel` (`Enhancement.cs:340-358`:
    SetO → the set's `LevelMin/LevelMax`; InventO → the enhancement's; other types have no IO level — write 1, the value Mids keeps for them in the samples),
    returned 0-based (`LevelMax - 1`, so Achilles' Heel 19, Kismet 29, Miracle 39, Shield Wall 49);
    `level_text_problem(value) -> str | None` (must be a string of an int in 0..49, `CharacterBuildData.cs:41`);
    `missing_inherents(entries, class_name, db) -> list[str]`.
  - `check_mbd_invariants(doc, db)` raising `BuildSpecError` `E73` (arity), `E74` (IoLevel outside the bounds, naming power, uid, bounds),
    `E75` (Level text), `E76` (missing inherents, listing them). Called by both emitters below.
  - `dump_mbd(build: Build) -> dict` — the inverse of `parse_mbd` (`+1` on power and slot levels, `str(level)` for the header,
    the five enhancement keys, `SubPowerEntries`).
  - `write_mbd(spec, schedule, db)`: `"Level": str(spec.level - 1)` (new `BuildSpec.level: int = 50`); `IoLevel` from `io_level_bounds`;
    append the class's `InherentPowers` missing from `spec.inherents` as unslotted entries at their `Level`; keep the pick/slot walk untouched.
    Mids clamps a too-high IoLevel on load (the committed tanker dump already shows 49), which is why the defect surfaced only on sets with a lower ceiling.
- **Mirror**: `classify_placement` (`legality.py:102-121`) for "one decision, many consumers"; `_ordered_picks` E46 messages for the refusal style.
- **Validate**: `pytest src/coh_engine/tests/test_mbd_writer.py` with new tests:
  `test_io_level_follows_the_set_ceiling` (19/29/29/39/39/49/49 for the seven sets the issue names);
  `test_seven_powersets_are_refused` (E73); `test_an_io_level_above_its_set_is_refused` (E74);
  `test_a_one_based_level_is_refused` (E75); `test_a_missing_inherent_is_refused` (E76, with `check_mbd_invariants` on a hand-built doc);
  `test_inherents_the_spec_omits_are_added`.

#### Task 9: Red/Green — every sample `.mbd` round-trips

- **Action**: `test_every_sample_mbd_round_trips` over `samples/builds/**/*.mbd` and `fixtures/builds/*.mbd`:
  `parse_mbd(dump_mbd(load_mbd(f))) == load_mbd(f)` and `check_mbd_invariants(dump_mbd(...), db)` raises nothing.
  Failures collected per file and asserted empty.
  If a sample fails an invariant, that is a finding about the invariant (Mids wrote the file), not a reason to drop the sample.
- **Mirror**: `test_loads_all_sample_builds` (`test_mbd.py:16-24`).
- **Validate**: `pytest src/coh_engine/tests/test_mbd_writer.py -k round_trips`.

#### Task 10: regenerate the tanker fixture

- **Action**: trim `INHERENTS` in `wpss_tanker_spec.py` to the slotted ones (Sprint, Swift, Health, Hurdle, Stamina) and let the writer add the rest.
  Rewrite `fixtures/builds/wpss_tanker_leadership.mbd` from `SPEC` (`json.dumps(write_mbd(SPEC, schedule, db), indent=2) + "\n"`,
  exactly as `test_the_writer_reproduces_the_committed_tanker_build` compares it).
  The regenerated file is Mids-loaded again in T12 so `fixtures/mids/builds/wpss_tanker_leadership/` matches it.
- **Mirror**: `test_mbd_writer.py:142-162`.
- **Validate**: `pytest src/coh_engine/tests/test_mbd_writer.py` after T12.
- **Commits**: `feat(tools): export set level limits and per-class inherent powers from the Mids dump harness` (`Refs #44`);
  `fix(engine): write .mbd files the way MidsReborn writes them, and refuse what it would not load` (`Closes #44`).

### Issue #45 — `validate` accepts a `.mbd`

#### Task 11: harness exports the powers table

- **Action**: new `DumpPowers(outDir)` → `powers.json`: every power of a powerset whose `SetType` is Primary, Secondary, Ancillary,
  Pool, Inherent, or an incarnate set (the filter `Program.cs:403` uses).
  Fields: `FullName`, `NIDPower`, `StaticIndex`, `PowerType`, `Level`, `SetTypes`, `Enhancements`, `RequiresPowers`, `RequiresPowersNot`,
  `RequiresClass`, `RequiresClassNot`, `PowerSetType` — the `Program.cs:608-676` field set minus `Effects`, `Chosen`, `StatInclude`.
  Register it in the dump sequence (`Program.cs:117-131`) and the README.
- **Mirror**: `DumpPowerIndex` (`Program.cs:1268`) for the DB walk; `:608-676` for field names.
- **Validate**: after T12, file size recorded in the commit body (expected low single-digit MB; above 5 MB, narrow the powerset filter before committing).

#### Task 12: the one harness run (operator step; exact commands)

- **Action**, from the worktree root, Git Bash:

  ```bash
  FORK=/c/Users/petek/repos/MidsReborn
  PATCH="$PWD/tools/mids-dump/patches/gbpa-sequential.patch"
  git -C "$FORK" status --short                       # must print nothing
  git -C "$FORK" apply "$PATCH"
  cd tools/mids-dump
  dotnet build -c Debug
  dotnet run -c Debug --no-build -- "$FORK/MidsReborn/Databases/Homecoming" ./out - ../../src/coh_engine/tests/fixtures/builds
  cd ../..
  git -C "$FORK" checkout -- MidsReborn/clsToonX.cs
  git -C "$FORK" status --short                       # must print nothing
  ```

  Third arg `-` skips the `.mxd` re-export (the five parity `.mxd` fixtures stay as they are).
  Then compare `out/version.json` with `fixtures/mids/version.json`.
  Same DB version: copy `archetypes.json`, `enhancements.json`, `enhancement_sets.json`, `powers.json`, `version.json`
  and `builds/wpss_tanker_leadership*/` into `fixtures/mids/`, then `git diff --stat` and read it before copying anything else.
  Different DB version: copy only those files, run the whole suite, and stop to report if any parity test moves.
  A DB bump is a re-baseline decision for the user, not this plan.
- **Validate**: `git -C "$FORK" status --short` empty; `pytest src/coh_engine/tests` green; `git status --short` shows only the intended fixture files.

#### Task 13: Red/Green — resolve a `.mbd` into `Power` / `SlotRef`

- **Action** in `mbd.py`: `load_power_table(path) -> Mapping[str, PowerTemplate]` from `powers.json`.
  `powers_from_build(build, table) -> tuple[Power, ...]`: `build_index` = entry position, `pick_level = entry.level`,
  DB `level`, `set_types`, `enhancements`, `requires_*` from the table, `chosen=True`, `effects=()`,
  neutral numeric fields as `test_hard_limits._power` sets them.
  `slots_from_build(build, db) -> dict[int, tuple[SlotRef, ...]]`: `enh` = nid by UID, `grade`/`relative_level` ordinals
  from the tables now at `mxd.py:143-157`, moved to `mbd.py` and imported back by `mxd.py`.
  Unknown power name → `ValueError` `E77`; unknown enhancement UID → `E78`, each naming the entry and the file to re-dump.
  Module docstring records decision 4: no `Chosen` in a `.mbd`, so a granted pool sub-power counts as a pick.
- **Mirror**: `load_powers_effects` (`effect.py:184`), `load_build_slots` (`enhancement.py:127`), `_power` factory (`test_hard_limits.py:33-72`).
- **Validate**: `pytest src/coh_engine/tests/test_mbd.py -k resolve` — the Bane sample resolves with 24 picks and every slot's `enh >= 0`;
  a mutated UID raises `E78`.

#### Task 14: Red/Green — `H-FMT-001..003` as registered rules

- **Action**: `MbdFormat` frozen dataclass (`power_sets`, `level_text`, `io_bounds_by_nid`) as an optional `HardLimitsContext.format`
  (`None` on the directory path, so that path's output is unchanged); `build_context`/`check_hard_limits` take `format=None` keyword.
  Three `@hard_limit_rule`s reading only the T8 primitives:
  `H-FMT-001` (`expected=8, actual=n`, fix: pad with `""` or drop the extra);
  `H-FMT-002` per filled slot outside its bounds (power, UID, bounds);
  `H-FMT-003` (`level_text` not `"0".."49"`; fix: write character level minus one as a string).
  Each yields nothing when `format is None`.
- **Mirror**: `_rule_single_ancillary_pool` (`hard_limits.py:370-385`); `test_registry_lists_every_rule` (`test_hard_limits.py:75`).
- **Validate**: `pytest src/coh_engine/tests/test_hard_limits.py` — a passing and a violating case per rule; the registry test lists the three new names.

#### Task 15: Red/Green — the CLI route

- **Action**: `validate <path>`: when `path.suffix == ".mbd"`, `run_validate_mbd(path, mids_dir)` loads the raw JSON once
  (for `Level` text and `PowerSets`), then `load_mbd`, `load_mbd_db`, `load_power_table`, resolves (T13), builds `MbdFormat`,
  and folds `check_hard_limits(..., format=...)` + `check_build_legality(..., enh_effects)`
  (the P-SET-001 pure-proc exemption, as `run_validate` does at `__main__.py:71-85`).
  `--mids-dir` default for a file: `REPO_ROOT / "src/coh_engine/tests/fixtures/mids"` (the only dump the repo ships), mirroring `chain_report.py:354`.
  Directory input keeps `run_validate` untouched.
- **Mirror**: `__main__.py:59-86,128-134`; `test_main.py:71-86`.
- **Validate**: `pytest src/coh_engine/tests/test_main.py`:
  text and `--format json` on the Bane operator sample report no `error` (exit 0 or 2);
  a `tmp_path` copy with `PowerSets[:7]` reports `H-FMT-001`;
  a copy with one `IoLevel` set to 50 on an Achilles' Heel piece reports `H-FMT-002`;
  a copy with `"Level": "50"` reports `H-FMT-003`; the existing directory tests unchanged.
  `python -m coh_engine rules` lists the three rules.

#### Task 16: granted-power provenance (decision 4)

- **Action**: a table in `docs/slotting-and-leveling.md` (the inherents section, `:113-120`) listing each power the samples carry
  that Mids adds without a pick, what grants it, and the game change behind it, repo sources first:
  - Fitness (Swift, Hurdle, Health, Stamina) inherent — live Issue 19 (Nov 2010);
    in repo: `docs/slotting-and-leveling.md:116`, `community/mechanics/guide-to-power-pools.md:93`.
    Release notes not in repo → new research: the Issue 19 patch notes on homecoming.wiki / Paragon Wiki.
  - Translocation and Arcane Power (Sorcery) — Pool Powerset Update: Sorcery / Travel Power Updates, April 20 2021;
    in repo: `data/diff/patch_history/Issue_27_Page_2.md:9,12` (numeric rows only; the page URL is the source).
    These are the only Pool-group grants, hence the only `H-POWER-001` over-count risk.
  - Double Jump, Afterburner (inherent toggle), Speed Phase — the same Travel Power Updates (Issue 27 Page 1/2);
    not in repo beyond the Sorcery rows → new research on the same pages.
  - Fast Snipe — the snipe rework; not in repo → new research (Homecoming Issue 25/Page notes).
  - Prestige sprints and Prestige Ninja Run — P2W grants; in repo: `community/mechanics/a-p2w-store-primer.md`.
  Only Pool-group grants affect pick counting; the Inherent-group ones are already excluded by `_is_inherent`.
- **Mirror**: the table style already in that doc.
- **Validate**: `pre-commit run markdownlint --files docs/slotting-and-leveling.md`.
- **Commits**: `feat(tools): export the powers table the validator needs for a bare .mbd` (`Refs #45`);
  `feat(engine): validate a .mbd file through the registries, with the format invariants as hard-limit rules` (`Closes #45`);
  `docs: trace the powers MidsReborn grants without a pick to their game changes` (`Refs #45`).

### Issue #42 — sample builds as fixtures

#### Task 17: the curated subset (decision 5)

- **Action**: twelve `.mbd`, one per archetype with a sample, written with `git show 'stash@{0}^3:<path>' > '<dest>'`
  (Git Bash; never pop/apply) or copied from the main checkout's untracked folders, verified by `git hash-object` against `git ls-tree -l`:
  - already tracked: Scrapper `Laser - Accretion …`, Dominator `Hollowmind …` (`samples/builds/operator-builds/`).
  - stash `operator-builds/`: Blaster `Laser - PRIME - Blaster …`, Brute `Laser - HAZMAT - Brute …`, Controller `Laser - ALT+F4 - Controller …`,
    Corruptor `Laser - NUKE - Corruptor …`, Sentinel `Laser - Fallout - Sentinel …`, Tanker `Laser - QUANTUM - Tanker …`,
    Arachnos Soldier `Laser - ARACHNOS - Arachnos Soldier …`
    (the main checkout's untracked `Laser - Arachnos - …` is the same build by name; take the stash copy and confirm by hash).
  - stash `community/Mids Archives/`: Mastermind one of the five `Mastermind (… - …).mbd`, Stalker `Stalker (Fiery Melee - Invulnerability).mbd`,
    Arachnos Widow `Widow/Melee Arachnos Widow (Night Widow Training - Widow Teamwork).mbd`,
    kept under `samples/builds/community/Mids Archives/<AT>/` so #22 can land the rest beside them.
  - No chosen file may share a stem with one of the five `fixtures/mxd/*.mxd` (`_mbd_for` asserts one match, `test_mxd_parity.py:36-39`).
    Defender, Peacebringer, Warshade have no sample: recorded as gaps in T19.
- **Mirror**: existing `samples/builds/{community,operator-builds}/` layout; `test_loads_all_sample_builds` already rglobs them.
- **Validate**: `pytest src/coh_engine/tests/test_mbd.py`; `git status --short` lists exactly the ten new files.

#### Task 18: Red/Green — the end-to-end pass

- **Action**: `test_sample_builds.py`: for every `samples/builds/**/*.mbd`: parse; round trip (T8 `dump_mbd`); resolve (T13);
  run `check_hard_limits(..., format=MbdFormat)` and `check_build_legality`;
  collect `(file, failure)` for every exception or `error` diagnostic; assert the list is empty with the whole list in the message;
  assert the set of classes equals the twelve expected (a dropped file is a failure, not a smaller pass).
  An archive build whose UIDs predate the current DB (`E78`) is unsuitable as a fixture — swap it for a newer one in T17 rather than relaxing the test.
  Also compute each build's totals from its committed dump folder (decision 5); assert no error and every total within the archetype's caps.
- **Mirror**: `test_the_loaded_tanker_build_validates_without_errors` (`test_mbd_writer.py:164`); `test_loads_all_sample_builds`.
- **Validate**: `pytest src/coh_engine/tests/test_sample_builds.py`.

#### Task 19: `samples/builds/README.md`

- **Action**: coverage table (archetype → file → origin: operator build or Mids archive, `captured_by: local-capture` wording per `no-user-name.md`),
  the three archetypes with no sample, the note that `stash@{0}` holds the remaining 3,133 `.mxd` and archive `.mbd` for #22,
  and how the files are exercised (`test_mbd.py`, `test_mbd_writer.py` round trip, `test_sample_builds.py`, `validate <file>`).
- **Mirror**: `tools/mids-dump/README.md` Outputs table style.
- **Validate**: `pre-commit run markdownlint --files samples/builds/README.md`.
- **Commits**: `test(samples): keep one Mids-saved sample build per archetype as end-to-end fixtures` (`Closes #42`).

## Validation

Run from the worktree; the venv lives in the main checkout.

```bash
VENV=../../../.venv/Scripts
$VENV/python -m pytest src/coh_engine/tests -q
$VENV/python -m pytest src/coh_engine/tests --cov=src/coh_engine --cov-report=term-missing --cov-fail-under=100
$VENV/ruff check . && $VENV/ruff format --check . && $VENV/mypy scripts src
$VENV/pre-commit run --all-files
$VENV/python -m coh_engine rules
$VENV/python -m coh_engine validate "samples/builds/operator-builds/Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training).mbd" --format json
$VENV/python -m coh_engine validate src/coh_engine/tests/fixtures/mids/builds/wpss_tanker_leadership   # directory path unchanged
git log --show-signature -12                                                                            # every commit signed, each with Refs/Closes
```

`make lint` / `make cov` work with `VENV_BIN=../../../.venv/Scripts` (`Makefile:9-19`).

## Risks

| Risk | Likelihood | Mitigation |
| --- | --- | --- |
| The fork's Homecoming DB moved since the fixtures were dumped; a full re-dump churns every parity fixture | medium | T12 copies only the new/changed files first and diffs `version.json`; a DB bump stops for a user decision |
| `powers.json` is large | medium | powerset-type filter; size reported in the commit; narrow before committing above 5 MB |
| A legacy `.mxd` raises in the semantic reader for a reason unrelated to `\|\|` | medium | T3 reports every failure; a genuine legacy limit becomes a finding on the issue, not a skipped file |
| An archive `.mbd` carries UIDs the current DB no longer has | medium | T18 fails loud (`E78`); T17 swaps for a newer file |
| `InherentPowers` filter differs from what Mids adds on load (`Special_Set_Bonuses`, Fast Snipe) | medium | T7 compares against the Scrapper sample; the round trip over every sample (T9) catches an over-strict E76 |
| Sorcery sample over-counts picks (`H-POWER-001`) | low | decision 4: documented limit; none of the twelve curated builds takes Sorcery (check in T17) |
| `manifest.json` sha for the two edited canonical files goes stale | certain | stated in T6's commit body; regenerating the 10 MB manifest is the user's call |
| The shared stash is popped by another session | low | read with `git show 'stash@{0}^3:<path>'` only; copy early (T17 before T12) |

## Acceptance

- [ ] All tasks complete; one commit or more per issue with `Refs #N` / `Closes #N`, signed, Conventional Commits
- [ ] Validation passes: pytest with 100% coverage of `src/coh_engine`, ruff, mypy, pre-commit
- [ ] Patterns mirrored, not reinvented: no dispatcher edit; H-FMT rules and writer refusals share one primitive each
- [ ] The MidsReborn fork is pristine after T12 (`git -C <fork> status --short` empty)
- [ ] PRD milestone rows updated to `complete` as each issue closes
