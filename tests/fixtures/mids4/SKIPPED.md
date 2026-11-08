# Builds the Mids 4.0 harness could not load

Mids 4.0 rewrites every Homecoming build through `LegacyLoadMap.mhd` on load and refuses a build whose powerset or power it cannot map.
Each build below loaded under Mids 3.x and has dumps in `src/coh_engine/tests/fixtures/mids/builds/`, but has no 4.0 dump.

| Build | Error from `BuildManager.LoadFromFile` |
| --- | --- |
| `arsenal_dominator_cardiac` | Powerset `Epic.Dark_Mastery_Dominator` could not be mapped to the current Homecoming database (legacy version 2026.5.1337). |
| `Laser - QUANTUM - Tanker (Super Reflexes - Energy Melee)` | Power `Inherent.Inherent.Energy_Focus` could not be mapped; no valid current Homecoming power target exists (legacy version 2024.5.759). |

Dumped at Mids 4.0 commit `24ee176f4` with `tools/mids-dump-4/patches/pipeline-sequential.patch` applied.
