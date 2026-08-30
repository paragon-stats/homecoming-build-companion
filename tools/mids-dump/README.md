# mids-dump — MidsReborn reference-dump harness

Test-only C# console harness that loads the MidsReborn Homecoming database **headless**
(no UI) and dumps JSON reference data the Python engine (`src/coh_engine/`) baselines its
golden fixtures against. Mids is the reference implementation: every numeric claim in the
port is validated against these dumps, per
[`docs/engine/mids-port-spec.md`](../../docs/engine/mids-port-spec.md).

## Prerequisites

- .NET SDK 8.0+ (builds with 10.x).
- The MidsReborn fork checked out as a **sibling** of this repo
  (`<repos>/MidsReborn/MidsReborn/MidsReborn.csproj` — the project reference is relative).

## Usage

```bash
dotnet build -c Debug
dotnet run -c Debug --no-build -- <midsreborn-fork>/MidsReborn/Databases/Homecoming ./out
```

Load sequence mirrors `MainModule.LoadDataAsync` (minus graphics); dialogs only fire on
load errors.

## Race-free dumps — apply the harness patch first

`MidsReborn.GenerateBuffedPowerArray` runs its per-power pass loops under `Parallel.For`,
which carries three intra-pass data races (a cross-power `Math_Mag`/`BuffedMag` read, a torn
`Effects` array, and a concurrent write to the shared non-thread-safe `ModifyEffects`
dictionary). They surface on **incarnate and set-bonus** builds — e.g. Combat Jumping's
endurance dumps ÷2.15 instead of the correct ÷1.45. The interactive UI happens to show the
correct in-index-order result; the harness must reproduce it deterministically.

The fork is a read-only vendored reference and stays pristine (no branches, no commits), so
the fix lives here as a patch applied only for the duration of a dump, then reverted:

```bash
FORK=<repos>/MidsReborn
git -C "$FORK" apply tools/mids-dump/patches/gbpa-sequential.patch   # serialize the pass loops
dotnet build -c Debug                                                # rebuild against the patched fork
dotnet run -c Debug --no-build -- "$FORK"/MidsReborn/Databases/Homecoming ./out - <parity-builds>
git -C "$FORK" checkout -- MidsReborn/clsToonX.cs                     # revert — fork back to pristine
```

[`patches/gbpa-sequential.patch`](./patches/gbpa-sequential.patch) replaces both `Parallel.For`
loops with sequential `for` loops (see the patch's inline comment for the full rationale).
Non-incarnate builds are byte-identical patched or not — only race-carrying builds change.

## Outputs

| File | Contents |
| --- | --- |
| `version.json` | Homecoming DB version + dump timestamp — every baseline is version-stamped |
| `server_data.json` | `BaseToHit`, `MaxSlots`, `EnableInherentSlotting` |
| `archetypes.json` | Per-AT `Column` (class→column indirection) and all caps (HP/Res/Damage/Recharge/Recovery/Regen/Perception) |
| `maths.json` | `MultED` / `MultTO` / `MultDO` / `MultSO` / `MultHO` / `MultIO` — the ED and grade-effectiveness tables |
| `enhancement_classes.json` | Enhancement class ID/name lookup |
| `enhancements.json` | Per-enhancement `StaticIndex` / `UID` / `TypeID` (`.mxd` slot-reader byte-count map), plus the legality fields: `ClassIds` (resolved `EnhancementClasses[..].ID`), `nIDSet`, `Unique`, `MutExId`/`MutExName`, `Superior`, `LongName` |
| `legality_probe.json` | Per named (power, enhancement) pair: Mids' `IsEnhancementValid` verdict + `Power.Enhancements` / `SetTypes` / enh `nIDSet` — the golden-negative reference (Recharge IO in One with the Shield, Force Feedback in Soul Drain both reject; Recharge IO in Hasten accepts) |
| `enhancement_effects.json` | Per-enhancement `Superior` + `nIDSet` (owning set) + `Enhancement`-mode effects (aspect / schedule / multiplier / buff-mode) — the enhancement value-pipeline input |
| `enhancement_sets.json` | Per set: `Uid`, `SetType` (+ `SetTypeName`), member `Enhancements[]`, tier `Bonus[]` (`Slotted` / `PvMode` / `Index[]`), and per-enhancement `SpecialBonus[]` — the set-bonus assembly definitions |
| `set_bonus_powers.json` | The referenced `set_bonus` powers (keyed by global Power id) the virtual power clones effects from, with the MyPet-only flags |
| `power_static_index.json` | `StaticIndex` → `FullName` map (`.mxd` build-file resolution layer) |
| `enums.json` | Name → ordinal for every enum the engine indexes arrays by (`eDamage`, `eEffectType`, `eStatType`, `eEnhance`, `eSchedule`, `eSetType`, …) |
| `config.json` | Config state totals are computed under (`Suppression`, `DisablePvE`, `ForceLevel`, `ScalingToHit`) |
| `builds/<name>/powers_effects.json` | Per parity build (4th CLI arg): the build's resolved DB powers with the full effect field set |
| `builds/<name>/slots.json` | Per parity build: the resolved per-power slot layout (`Level`, `Enh` nID, `Grade`, `IOLevel`, `RelativeLevel`) |
| `builds/<name>/enhanced_powers.json` | Per parity build: each power's enhanced scalars + effect `Math_Mag` (`GetEnhancedPower`), with the unenhanced `Base` scalars. Adds `CastTime` and `DamagePerActivation` (`FXGetDamageValue` under the `config.json` damage mode) for the CP6.1 derived-stat parity |
| `builds/<name>/set_bonus_virtual_power.json` | Per parity build: the assembled `SetBonusVirtualPower` effects (structural key + `Mag`) — the golden for the port's set-bonus assembly |
| `builds/<name>/incarnates.json` | Per parity build: each StatInclude `Incarnate.*` power resolved to its GrantPower-granted sub-powers (`fx.nSummon` → `db.Power[nSummon]`), with each sub-power's accept-gate `Enhancements` class list and its `Enhancement`/`DamageBuff` effects (the `IgnoreED` pre/post-ED split). Empty `[]` for a build with no incarnate |
| `builds/<name>/totals.json` | Per parity build: `Totals` + `TotalsCapped` after `GenerateBuffedPowerArray()` |
| `builds/<name>_exemplar<N>/*` | A designated build recomputed at a lowered `ForceLevel` (N) — the exemplar-parity fixtures; the set-bonus gate keys on `PickLevel`, not the DB minimum, so bonuses from powers picked above N drop |

Each build power in `powers_effects.json` also carries `Chosen` (`PowerEntry.Chosen` — false
for every power Mids auto-adds rather than the player picking: inherents, incarnates, and
granted sub-powers such as Sorcery's Translocation; the hard-limits validator excludes these
from the 24-pick count) and `PickLevel` (the build's actual
`PowerEntry.Level`) alongside the DB minimum `Level`; the set-bonus exemplar gate uses
`PickLevel` (they coincide at `ForceLevel` 50 and diverge under exemplar). It further
carries the base `RechargeTime` / `CastTime` / `InterruptTime` and `IgnoreEnh` (the
`eEnhance` aspects the power ignores) — the CP6.1 derived-stat layer's inputs; the
per-power scalar fold skips an ignored aspect (e.g. One with the Shield ignores
`RechargeTime`).

Each build power in `powers_effects.json` also carries `SetTypes` (the `eSetType`
ordinals the power accepts) so the port can map which IO sets are slottable where.

## Re-baselining

After any Homecoming DB update in the fork: re-run the dump, refresh
`src/coh_engine/tests/fixtures/mids/`, and re-run `pytest src/coh_engine/tests`.
`test_mids_parity.py` fails loudly if the port and the Mids reference dumps drift.

Float caveat: .NET serializes `float` with shortest-round-trip strings ("0.7"), which
Python parses as float64. Comparisons must quantize both sides through
`coh_engine.maths.f32` — see `test_mids_parity.py`.

`bin/`, `obj/`, and `out/` are gitignored; the committed baseline lives in the Python
test fixtures, not here.
