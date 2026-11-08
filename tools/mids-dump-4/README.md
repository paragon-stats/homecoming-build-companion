# mids-dump-4: MidsReborn 4.0 reference-dump harness

Test-only console harness that loads the Mids Reborn 4.0 Homecoming database headless and writes the same reference dumps as [`tools/mids-dump`](../mids-dump/README.md), with identical JSON shapes.
It exists so the engine's drift from 4.0 is measured, and listed by [`scripts/dev/diff-goldens.cs`](../../scripts/dev/diff-goldens.cs), before 4.0 ships.
Output goes to `tests/fixtures/mids4/`.
The 3.x dumps stay the parity goldens until a re-baseline PR (see [`mids-4-alignment.md`](../../docs/engine/mids-4-alignment.md)).

## Prerequisites

- .NET SDK 8.0 or later (built with 10.x). 4.0's `MidsReborn.csproj` targets `net8.0-windows10.0.19041.0`, the same as 3.x.
- The 4.0 branch checked out as a git worktree of the Mids fork, three levels above this folder by default:

```bash
git -C <repos>/MidsReborn worktree add <repos>/MidsReborn-4.0 upstream/4.0
```

From a git worktree of this repository pass the location: `-p:MidsFork4=<repos>/MidsReborn-4.0`.
The harness was written against `upstream/4.0` at commit `24ee176f4`.

## Usage

```bash
git -C <repos>/MidsReborn-4.0 apply tools/mids-dump-4/patches/pipeline-sequential.patch   # once
dotnet build tools/mids-dump-4 -c Debug -p:MidsFork4=<repos>/MidsReborn-4.0
dotnet run --project tools/mids-dump-4 -c Debug --no-build -- \
  <repos>/MidsReborn-4.0/MidsReborn/Databases/Homecoming ./out - <parity-builds>
```

`<parity-builds>` is a folder searched recursively for `.mbd` files.
The 3.x tree was dumped from 23 builds, and the 4.0 tree uses the same set:

- the eleven under `src/coh_engine/tests/fixtures/builds/`
- the nine under `samples/builds/operator-builds/`
- the Petless Mastermind, Stalker and Melee Arachnos Widow builds under `samples/builds/community/Mids Archives/`

Copy them into one scratch folder and pass that.
Then copy `out/` into `tests/fixtures/mids4/`, leaving out `SKIPPED.txt` (its content is [`SKIPPED.md`](../../tests/fixtures/mids4/SKIPPED.md)).
Finish with `dotnet run scripts/dev/diff-goldens.cs -- --summary`.

## Pipeline

`Toon.GenerateBuffedPowerArray` runs `PlannerPowerPipeline`: assembly, enhancement buckets, per-power enhancement math, self-buff buckets, post-buff multiply and finalization.
Totals come from `ActorTotalsCalculator` through `Toon.Totals` and `Toon.TotalsCapped`, so the harness reads the same properties as the 3.x one.

Setup differences from the 3.x harness, all forced by 4.0:

- `LoadLevelsDatabase` runs after `LoadMainDatabase`, because it reads progression data from the main database. In the 3.x order it opens a dialog and blocks forever.
- No `AttribMods.Load`, `LoadReplacementTable` or `AssignSetBonusIndexes`. 4.0 removed the class-table loader, and its own load sequence skips the other two (`LoadReplacementTable` blocks on a dialog).
- `Config.DataPath` is set to the database folder, because 4.0 refuses a build whose database name differs from the active one.
- 4.0 reports build-load problems with modal dialogs. The harness swaps `BuildManager`'s private notifier for a recording one.
  A build that fails to load is written to `SKIPPED.txt` with the notice text, and the run continues.

## The data race is still present

4.0 still runs the per-power enhancement loop and the post-buff multiply loop under `Parallel.For`.
They are in `ExecutePerPowerEnhancementMathPhase` and `ExecutePostBuffMultiplyPhase` of `PlannerPowerPipeline.cs`.
The loop body still reaches across powers.
`GBPA_Pass1_EnhancePreED` and `GBPA_Pass3_EnhancePostED` pass other powers' `_mathPowers[index]` to `GBPA_ApplyIncarnateEnhancements`,
and the absorb step writes into another power's `_buffedPowers[hIDX]`.
That is the cross-power read and write the 3.x patch fixed.
The 3.x shared `ModifyEffects` dictionary is no longer written inside the loop.
The race was found by reading the code. No unpatched 4.0 dump was compared with a patched one.

[`patches/pipeline-sequential.patch`](./patches/pipeline-sequential.patch) replaces both `Parallel.For` loops with index-order `for` loops.
Keep it applied to the 4.0 worktree as an uncommitted edit.
The harness exits `E15` when the compiled fork still has the `Parallel.For` lambdas.

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

## Not dumped

- The `_clicksoff` variants. No 3.x clicks-off dumps are committed, so there is nothing to compare them with.
- `assembly_powers.json` is written but not committed. At 16.6 MB (10.4 MB in 3.x) it exceeds the 15 MB large-file hook limit.
  The diff script does not read it. Regenerate it with the run above.
- Anything new in 4.0. Shapes stay identical so the diff script compares like with like.

`bin/`, `obj/` and `out/` are gitignored.
