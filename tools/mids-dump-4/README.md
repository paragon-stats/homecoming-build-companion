# mids-dump-4: MidsReborn 4.0 reference-dump harness

Test-only console harness that loads the Mids Reborn 4.0 Homecoming database headless and writes the same reference dumps as [`tools/mids-dump`](../mids-dump/README.md), with the same JSON shapes.
It exists so the engine's drift from 4.0 is measured, and listed by [`scripts/dev/diff-goldens.cs`](../../scripts/dev/diff-goldens.cs), before 4.0 ships.
Output goes to `tests/fixtures/mids4/`.
The 3.x dumps stay the parity goldens until a re-baseline PR (see [`mids-4-alignment.md`](../../docs/engine/mids-4-alignment.md)).

The committed dumps come from `upstream/4.0` at commit `24ee176f4`, Homecoming database version 2026.6.1303, with `pipeline-sequential.patch` applied.

## Prerequisites

- .NET SDK 8.0 or later (built with 10.x). 4.0's `MidsReborn.csproj` targets `net8.0-windows10.0.19041.0`, the same as 3.x.
- The 4.0 branch checked out as a git worktree of the Mids fork, three levels above this folder by default:

```bash
git -C <repos>/MidsReborn worktree add <repos>/MidsReborn-4.0 24ee176f4
```

From a git worktree of this repository pass the location: `-p:MidsFork4=<repos>/MidsReborn-4.0`.

## Usage

`<repo>` is the absolute path of this repository's checkout.

```bash
git -C <fork4> apply <repo>/tools/mids-dump-4/patches/pipeline-sequential.patch   # once
cd <repo>/tools/mids-dump-4
dotnet build -c Debug -p:MidsFork4=<fork4>
dotnet run -c Debug --no-build -- <fork4>/MidsReborn/Databases/Homecoming ./out - <parity-builds>
```

Running from `tools/mids-dump-4` makes `./out` the ignored `tools/mids-dump-4/out/`.
`<parity-builds>` is a scratch folder holding exactly these 23 files, copied from this repository (a recursive copy of a folder picks up other `.mbd` files):

- from `src/coh_engine/tests/fixtures/builds/`: `arsenal_dominator_cardiac`, `arsenal_dominator_incarnate`, `arsenal_dominator_incarnate_slotted`,
  `arsenal_dominator_noslots`, `hasten_2rech`, `shield_scrapper_global_endrdx`, `shield_scrapper_mixed_sets`, `shield_scrapper_noslots`,
  `shield_scrapper_set_bonuses`, `shield_scrapper_slotted`, `wpss_tanker_leadership` (each `.mbd`)
- from `samples/builds/operator-builds/`: the nine `.mbd` files there (`Hollowmind - Dominator ...`, `Laser - ALT+F4 ...`, `Laser - ARACHNOS ...`,
  `Laser - Accretion ...`, `Laser - Fallout ...`, `Laser - HAZMAT ...`, `Laser - NUKE ...`, `Laser - PRIME ...`, `Laser - QUANTUM ...`)
- from `samples/builds/community/Mids Archives/`: `Mastermind/Petless Mastermind (Demon Summoning - Force Field).mbd`,
  `Stalker/Stalker (Fiery Melee - Invulnerability).mbd`, `Widow/Melee Arachnos Widow (Night Widow Training - Widow Teamwork).mbd`

Then copy `out/` into `tests/fixtures/mids4/`, leaving out `SKIPPED.txt`, `REMAPPED.txt` and `assembly_powers.json` (see below).
`SKIPPED.txt` is [`SKIPPED.md`](../../tests/fixtures/mids4/SKIPPED.md) and `REMAPPED.txt` is [`REMAPPED.md`](../../tests/fixtures/mids4/REMAPPED.md).
Finish with `dotnet run scripts/dev/diff-goldens.cs -- --summary`.

Exit codes this harness adds to the 3.x set: `E15` fork not patched, `E16` `BuildManager` has no private `_notifier` field, `E17` class modifier tables empty after `LoadMainDatabase`.

## Pipeline

`Toon.GenerateBuffedPowerArray` runs `PlannerPowerPipeline`: assembly, enhancement buckets, per-power enhancement math, self-buff buckets, post-buff multiply and finalization.
Totals come from `ActorTotalsCalculator` through `Toon.Totals` and `Toon.TotalsCapped`, so the harness reads the same properties as the 3.x one.

Setup differences from the 3.x harness, all forced by 4.0:

- `LoadLevelsDatabase` runs after `LoadMainDatabase`, because it reads progression data from the main database. In the 3.x order it opens a dialog and blocks forever.
- No `AttribMods.Load` (4.0 removed the class-table loader) and no `LoadReplacementTable` (it blocks on a dialog; 4.0's own load sequence does not call it).
  `AssignSetBonusIndexes` is still called, as in the 3.x harness; the 4.0 app does not call it.
- `Config.DataPath` is set to the database folder, because 4.0 refuses a build whose database name differs from the active one.
- 4.0 reports build-load problems with modal dialogs. The harness swaps `BuildManager`'s private notifier for a recording one.
  A build that fails to load is written to `SKIPPED.txt` with the notice text, and the run continues.
  The compatibility summary of a build that loads after a remap goes to `REMAPPED.txt`.

## Determinism guard

4.0 still runs the per-power enhancement loop and the post-buff multiply loop under `Parallel.For`, in `ExecutePerPowerEnhancementMathPhase` and `ExecutePostBuffMultiplyPhase` at commit `24ee176f4`.
In 3.x the same loops raced and gave wrong endurance numbers. In 4.0 the Homecoming ruleset appears not to reach across powers in those loops:
`HomecomingPlannerRuleset.AllowLegacyCrossPowerIncarnatePasses` is false (`PlannerRulesets.cs:757`), the cross-power reads of `_mathPowers[index]` sit on the legacy branch only
(`PlannerPowerPipeline.cs:1078` and `:1306`), and the canonical path reads `_assembledBasePowers`, which only the assembly phase writes.

The comparison was run to check that reading.
All 22 build dumps (21 builds plus the exemplar variant) were produced three times on the unpatched fork with `--allow-unpatched`, and once with the patch applied.
The four `builds/` trees were byte-identical.
That is evidence for those 22 builds on one machine, not a proof for every build: a build that takes a legacy-ruleset path, or a later 4.0 commit, could still race.

So [`patches/pipeline-sequential.patch`](./patches/pipeline-sequential.patch) is a determinism guard that mirrors the 3.x harness, not a race fix.
It replaces both `Parallel.For` loops with index-order `for` loops, which is also the order the 3.x goldens assume.
Keep it applied to the 4.0 worktree as an uncommitted edit.
The harness exits `E15` when the compiled fork still has the `Parallel.For` lambdas; `--allow-unpatched` skips that check so the comparison can be repeated.
Dumps made with `--allow-unpatched` are not for committing.

## Shapes

The 4.0 shapes follow the current [`tools/mids-dump/Program.cs`](../mids-dump/Program.cs).
The committed 3.x goldens predate it: they have no `power_assembly.json`, `assembled_powers.json` or `forum_export.txt`, and `slots.json` lacks `ProcInclude`.
`powers_effects.json` lacks `Chosen` and the `Requires*` fields.
So the 4.0 tree has files and fields the 3.x tree lacks; the diff script compares only `totals.json` and `enhanced_powers.json`.

`enums.json` differs between the trees:

- Every `eSetType` member is renamed (for example `Blaster` is `BlasterArchetypeSets`, `AccHeal` is `AccurateHealing`); the ordinals are unchanged for members present in both.
- `eEffectType.ModifyAttrib` is removed, so `ExecutePower` moves from 85 to 84 and the new `MezProtect` takes 85.
- `eSuppress.Repelled` is gone.

## Fields with no 4.0 equivalent

These are emitted as `null` so the shapes match 3.x.

| File | Field | Why |
| --- | --- | --- |
| `totals.json` | `Totals.BuffHeal`, `TotalsCapped.BuffHeal` | `TotalStatistics` has no `BuffHeal` |
| Every file that carries effect records | `nModifierTable` | modifier tables are resolved by name (`ModifierTable`) |
| the same files | `SpecialCase` | the field is read and discarded on load |
| the same files | `ActiveConditionalsCount`, `ActiveConditionals` | `AdvancedConditions` rows replace them, in a different shape |
| `powers_effects.json`, `powers.json` | `RequiresPowers`, `RequiresPowersNot`, `RequiresClass`, `RequiresClassNot` | `AdvancedRequirements` rows replace them |

`archetypes.json` `InherentPowers` is filtered with `AdvancedRequirements` (class rows only, class allowed) in place of the 3.x `Requires` lists.

## Not committed, not dumped

- `assembly_powers.json` is written but not committed. At 16.6 MB (10.4 MB in 3.x) it exceeds the 15 MB large-file hook limit, which is global.
  A regenerated copy should be 16613241 bytes with SHA-256 `15ce53eeae78516a0c2d222e3ccfea02bd87dde7bb397f2d493088fd27b949c4`.
  The diff script does not read it.
- The `_clicksoff` variants. No 3.x clicks-off dumps are committed, so there is nothing to compare them with.
- Anything new in 4.0. Shapes stay identical so the diff script compares like with like.

`bin/` and `obj/` are gitignored, as is `out/` in this folder.
