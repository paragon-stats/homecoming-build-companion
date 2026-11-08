# 3.x versus 4.0 reference dump drift

Generated 2026-11-08 by `scripts/dev/diff-goldens.cs`; first baseline, informational only.
The tables below are the script's `--summary` output, unedited.
The full line-by-line listing is not committed; it is regenerated in the dedicated re-baseline PR.

## Inputs

- 3.x dumps: `src/coh_engine/tests/fixtures/mids`, Homecoming database 2026.1.1242.
  They predate the current `tools/mids-dump/Program.cs` (see the harness README, "Shapes").
- 4.0 dumps: `tests/fixtures/mids4`, Homecoming database 2026.6.1303 as shipped in the 4.0 branch, commit `24ee176f4`.
- The trees differ in database version too, so a differing field is code drift, data drift or both until a justification says which.

## How fields are matched

- `totals.json` is compared by JSON path.
- `enhanced_powers.json` is keyed by power `FullName`, because 4.0 remaps and reorders powers.
  A repeated name gets `#2`, `#3`.
  A power in only one tree is counted as "only in 3.x" or "only in 4.0", not field by field.
- Within a power, an effect is keyed by (`EffectType`, `MezType`, `ETModifies`) plus its occurrence number among effects with the same tuple.
  `DamageType` is left out of the key because 4.0 fills it on effects that carry none in 3.x (for example `Melee` on `JumpHeight`).
- `BuildIndex` and `Index` are positions and are not compared.
- Every other numeric field is compared after rounding both sides to float32.
  A field present on one side only counts as a difference.
- `null` on the 4.0 side is a field with no 4.0 equivalent (harness README, "Fields with no 4.0 equivalent").

## Regenerate

```bash
dotnet run scripts/dev/diff-goldens.cs -- --summary
dotnet run scripts/dev/diff-goldens.cs
```

The second command prints one line per difference: `build/file.json:path 3x=<value> 4.0=<value>`.

## Summary

Compared 44 file pairs and 664 powers present in both trees.

| File | Differing fields | Powers only in 3.x | Powers only in 4.0 |
| --- | --- | --- | --- |
| totals.json | 1411 | | |
| enhanced_powers.json | 12426 | 94 | 62 |

| Build | totals.json | enhanced_powers.json | Powers only in 3.x | Powers only in 4.0 |
| --- | --- | --- | --- | --- |
| `Hollowmind - Dominator (Darkness Control - Psionic Assault)` | 57 | 509 | 4 | 4 |
| `Laser - ALT+F4 - Controller (Illusion Control - Time Manipulation)` | 57 | 551 | 4 | 3 |
| `Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training)` | 81 | 594 | 3 | 2 |
| `Laser - Accretion - Scrapper (Dark Melee - Shield Defense)` | 87 | 592 | 4 | 3 |
| `Laser - Fallout - Sentinel (Fire Blast - Radiation Armor)` | 12 | 206 | 1 | 1 |
| `Laser - HAZMAT - PvE - Brute (Radiation Melee - Fiery Aura)` | 86 | 1204 | 9 | 3 |
| `Laser - NUKE - Corruptor (Fire Blast - Radiation Emission)` | 71 | 621 | 5 | 4 |
| `Laser - PRIME - Blaster (Energy Blast - Energy Manipulation)` | 77 | 602 | 6 | 5 |
| `Laser - QUANTUM - Tanker (Super Reflexes - Energy Melee)` | no 4.0 dump | no 4.0 dump | | |
| `Melee Arachnos Widow (Night Widow Training - Widow Teamwork)` | 97 | 671 | 3 | 2 |
| `Petless Mastermind (Demon Summoning - Force Field)` | 88 | 805 | 3 | 2 |
| `Stalker (Fiery Melee - Invulnerability)` | 78 | 576 | 4 | 5 |
| `arsenal_dominator_cardiac` | no 4.0 dump | no 4.0 dump | | |
| `arsenal_dominator_incarnate` | 46 | 644 | 6 | 4 |
| `arsenal_dominator_incarnate_slotted` | 46 | 644 | 6 | 4 |
| `arsenal_dominator_noslots` | 44 | 569 | 6 | 4 |
| `hasten_2rech` | 53 | 407 | 4 | 3 |
| `shield_scrapper_global_endrdx` | 56 | 408 | 4 | 2 |
| `shield_scrapper_mixed_sets` | 60 | 431 | 4 | 2 |
| `shield_scrapper_noslots` | 51 | 401 | 4 | 3 |
| `shield_scrapper_set_bonuses` | 62 | 513 | 4 | 1 |
| `shield_scrapper_set_bonuses_exemplar25` | 60 | 455 | 4 | 1 |
| `shield_scrapper_slotted` | 53 | 407 | 4 | 3 |
| `wpss_tanker_leadership` | 89 | 616 | 2 | 1 |

Builds with no 4.0 dump are listed in [SKIPPED.md](./SKIPPED.md).
Builds whose load remapped powers are listed in [REMAPPED.md](./REMAPPED.md); their differences include the remap.
