# keywords_endcost_owner_arch

A Mids reference dump of a small synthetic Blaster build, kept to pin two expression keywords the engine reads:
`power.base>endcost` and `source.owner>arch(...)`.

## The build

`keywords_endcost_owner_arch.mbd` was written by the engine's own writer (`Mbd.WriteMbd`, then `Mbd.Serialize`) from a spec in code.
It has six picks on the schedule's levels:

| Level | Power | Slots |
| --- | --- | --- |
| 1 | `Blaster_Ranged.Electrical_Blast.Lightning_Bolt` | three `Crafted_Endurance_Discount` |
| 1 | `Blaster_Support.Energy_Manipulation.Energy_Punch` | none |
| 2 | `Blaster_Ranged.Electrical_Blast.Ball_Lightning` | none |
| 4 | `Pool.Sorcery.Arcane_Bolt` | one `Crafted_Accuracy` |
| 6 | `Blaster_Ranged.Electrical_Blast.Short_Circuit` | none |
| 8 | `Pool.Speed.Hasten` | none |

Lightning_Bolt has an effect whose magnitude is `power.base>endcost/2*modifier>current`, and the three endurance reductions cut its cost.
Arcane_Bolt has effects whose magnitude ends in `source.owner>arch(Scrapper)` and its siblings.
Lightning_Bolt and Arcane_Bolt count toward totals.
The build is not a real character and holds no personal data.

## How the dump was made

The Mids dump harness under `tools/mids-dump`, from that directory:

```text
dotnet run -c Debug --no-build -- <fork>/MidsReborn/Databases/Homecoming <out> - <directory holding the .mbd>
```

- Mids fork: `MidsReborn` at commit `dd189d08d`, with only `MidsReborn/clsToonX.cs` modified.
  That is the uncommitted `gbpa-sequential.patch`, and `git apply --reverse --check` on the patch succeeded.
  The harness's unpatched-fork refusal (E15) did not fire.
- Database version: `2026.1.1242` (`version.json` of the same run).
- Dumped on 2026-11-10.
- The files here are the eight per-build files the harness wrote, unchanged.
  They are `powers_effects`, `slots`, `power_assembly`, `assembled_powers`, `enhanced_powers`, `set_bonus_virtual_power`, `incarnates` and `totals`.
  No value was edited by hand or by script.
  The harness's `forum_export.txt` and the `_clicksoff` variants are not kept, as for the other dumps.

## Why it is not under `builds/`

The parity suites over `builds/` also hold per-build values from the Python engine (magnitude digests and loader values) and Python CLI captures.
Those do not exist for this build, so it stays here and `KeywordsBuildTests` checks it against the Mids values.
