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

The project reference expects the fork three levels above `tools/mids-dump`. From a git worktree,
pass its location: `dotnet build -c Debug -p:MidsFork=<repos>/MidsReborn`.

## Race-free dumps — the fork stays patched

`MidsReborn.GenerateBuffedPowerArray` runs its per-power pass loops under `Parallel.For`,
which carries three intra-pass data races (a cross-power `Math_Mag`/`BuffedMag` read, a torn
`Effects` array, and a concurrent write to the shared non-thread-safe `ModifyEffects`
dictionary). They surface on **incarnate and set-bonus** builds — e.g. Combat Jumping's
endurance dumps ÷2.15 instead of the correct ÷1.45. The interactive UI happens to show the
correct in-index-order result; the harness must reproduce it deterministically.

The fork takes no branches or commits, so the fix is
[`patches/gbpa-sequential.patch`](./patches/gbpa-sequential.patch), kept applied to the fork
as an uncommitted edit. It is not reverted after a dump: an unpatched run does not fail, it
silently gives wrong endurance numbers, so a fork left unpatched by mistake corrupts every
fixture it produces. `git -C <fork> status --short` lists only `MidsReborn/clsToonX.cs`, as modified.

```bash
FORK=<repos>/MidsReborn
git -C "$FORK" apply tools/mids-dump/patches/gbpa-sequential.patch   # once, if status shows the file clean
dotnet build -c Debug                                                # rebuild against the patched fork
dotnet run -c Debug --no-build -- "$FORK"/MidsReborn/Databases/Homecoming ./out - <parity-builds>
```

The harness refuses to dump (`E15`, exit 2) when the fork it was built against is unpatched.
The next upstream sync (MidsReborn 4.0) overwrites the fork, so the check fails after it until
the patch is re-applied, or until the new `GenerateBuffedPowerArray` is found not to need it.

The patch replaces both `Parallel.For` loops with sequential `for` loops (see its inline
comment for the full rationale). Non-incarnate builds are byte-identical patched or not —
only race-carrying builds change.

## Outputs

| File | Contents |
| --- | --- |
| `version.json` | Homecoming DB version + dump timestamp — every baseline is version-stamped |
| `server_data.json` | `BaseToHit`, `MaxSlots`, `EnableInherentSlotting` |
| `archetypes.json` | Per-AT `Column` (class→column indirection), all caps (HP/Res/Damage/Recharge/Recovery/Regen/Perception), `Ancillary` (the patron/ancillary powerset full names the class may take) and `InherentPowers` (`FullName` + DB `Level` of each inherent power Mids adds without a pick, filtered as `Build.AddAutomaticGrantedPowers` does) |
| `maths.json` | `MultED` / `MultTO` / `MultDO` / `MultSO` / `MultHO` / `MultIO` — the ED and grade-effectiveness tables |
| `enhancement_classes.json` | Enhancement class ID/name lookup |
| `enhancements.json` | Per-enhancement `StaticIndex` / `UID` / `TypeID` (`.mxd` slot-reader byte-count map), plus the legality fields: `ClassIds` (resolved `EnhancementClasses[..].ID`), `nIDSet`, `Unique`, `MutExId`/`MutExName`, `Superior`, `LongName`, and `LevelMin`/`LevelMax` (the InventO IO-level bounds `CheckAndFixIOLevel` clamps to) |
| `legality_probe.json` | Per named (power, enhancement) pair: Mids' `IsEnhancementValid` verdict + `Power.Enhancements` / `SetTypes` / enh `nIDSet` — the golden-negative reference (Recharge IO in One with the Shield, Force Feedback in Soul Drain both reject; Recharge IO in Hasten accepts) |
| `enhancement_effects.json` | Per-enhancement `Superior` + `nIDSet` (owning set) + `Enhancement`-mode effects (aspect / schedule / multiplier / buff-mode), plus `IsProc` and `PowerNid` (the enhancement's own power) — the enhancement value-pipeline and assembly input |
| `assembly_powers.json` | Unindented: each enhancement's own power and every power reachable through GrantPower, ExecutePower or an absorbed summon, with the assembly fields and extended effect records |
| `assembly_entities.json` | The summoned entities absorbing powers create (`ClassNid`, `PowersetNids`) and those powersets' powers in order, plus every entity a buildable power summons with the effect types of its first powerset's powers (`FirstPowersetEffectTypes`, for pets-only enhancement effects) |
| `enhancement_sets.json` | Per set: `Uid`, `SetType` (+ `SetTypeName`), `LevelMin`/`LevelMax` (the SetO IO-level bounds `CheckAndFixIOLevel` clamps to), member `Enhancements[]`, tier `Bonus[]` (`Slotted` / `PvMode` / `Index[]`), and per-enhancement `SpecialBonus[]` — the set-bonus assembly definitions |
| `set_bonus_powers.json` | The referenced `set_bonus` powers (keyed by global Power id) the virtual power clones effects from, with the MyPet-only flags; effects carry the assembly keys too (`Reward` for the chance modifiers) |
| `power_static_index.json` | `StaticIndex` → `FullName` map (`.mxd` build-file resolution layer) |
| `powers.json` | Every power of a Primary, Secondary, Ancillary, Pool, Inherent, Incarnate, Accolade or Temp powerset: `FullName`, `NIDPower`, `StaticIndex`, `PowerType`, `Level`, `SetTypes`, `Enhancements`, `RequiresPowers`/`RequiresPowersNot`, `RequiresClass`/`RequiresClassNot`, `PowerSetType` — what a bare `.mbd` resolves against (no effects; those stay per build) |
| `enums.json` | Name → ordinal for every enum the engine indexes arrays by (`eDamage`, `eEffectType`, `eStatType`, `eEnhance`, `eSchedule`, `eSetType`, …) |
| `config.json` | Config state totals are computed under (`Suppression`, `DisablePvE`, `ForceLevel`, `ScalingToHit`) |
| `builds/<name>/powers_effects.json` | Per parity build (4th CLI arg): the build's resolved DB powers with the full effect field set |
| `builds/<name>/slots.json` | Per parity build: the resolved per-power slot layout (`Level`, `Enh` nID, `Grade`, `IOLevel`, `RelativeLevel`) and the entry's `ProcInclude` |
| `builds/<name>/power_assembly.json` | Per parity build: what assembly reads about each build power beyond `powers_effects.json` — entity flags, `PowerSetType`, `BaseRechargeTime`, `AoEModifier`, the absorption flags, `IgnoreEnh`, sub-powers, and per effect `Summon`/`nSummon`/`Duration`/`EffectId`/`Reward`/`ActiveConditionals` |
| `builds/<name>/assembled_powers.json` | Per parity build: Mids' assembled effect list of each StatInclude power, the reference the engine's assembly is tested against |
| `builds/<name>/enhanced_powers.json` | Per parity build: each power's enhanced scalars + effect `Math_Mag` (`GetEnhancedPower`), with the unenhanced `Base` scalars. Adds `CastTime` and `DamagePerActivation` (`FXGetDamageValue` under the `config.json` damage mode) for the CP6.1 derived-stat parity |
| `builds/<name>/set_bonus_virtual_power.json` | Per parity build: the assembled `SetBonusVirtualPower` effects (structural key + `Mag`) — the golden for the port's set-bonus assembly |
| `builds/<name>/incarnates.json` | Per parity build: each StatInclude `Incarnate.*` power resolved to its GrantPower-granted sub-powers (`fx.nSummon` → `db.Power[nSummon]`), with each sub-power's accept-gate `Enhancements` class list and its `Enhancement`/`DamageBuff` effects (the `IgnoreED` pre/post-ED split). Empty `[]` for a build with no incarnate |
| `builds/<name>/totals.json` | Per parity build: `Totals` + `TotalsCapped` after `GenerateBuffedPowerArray()` |
| `builds/<name>_clicksoff/*` | Every parity build reloaded with `StatInclude` cleared on its self-targeted `DamageBuff` click powers (Aim, Build Up, ...) and recomputed — the clicks-off baseline |
| `builds/<name>_exemplar<N>/*` | A designated build recomputed at a lowered `ForceLevel` (N) — the exemplar-parity fixtures; the set-bonus gate keys on `PickLevel`, not the DB minimum, so bonuses from powers picked above N drop |

Mids treats every `StatInclude`d click power as always-on, so `DamagePerActivation` and `totals.json` of a build
with Aim or Build Up carry that damage buff permanently. The `_clicksoff` variant switches those clicks off (toggles,
Hasten and other powers stay untouched; one stdout line per build lists what was switched off), so the chain planner
can apply buff uptime itself on top of a clicks-off baseline.

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
