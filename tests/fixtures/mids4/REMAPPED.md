# Builds Mids 4.0 loaded after remapping powers

`LegacyLoadMap.mhd` rewrote these builds on load, so each 4.0 dump describes the remapped build, not the file as saved.
Their differences from the 3.x dumps include the remap.
Each row is the compatibility summary `BuildManager.LoadFromFile` raised for the build (lineage Homecoming in every case).
No build moved Fitness powers or removed the legacy Fitness pool.
The remaps are the source of renamed powers in the "only in 3.x" and "only in 4.0" columns of [DIFF-REPORT.md](./DIFF-REPORT.md).

| Build | Legacy tag | Remapped powers | Remapped enhancements |
| --- | --- | --- | --- |
| `Hollowmind - Dominator (Darkness Control - Psionic Assault)` | 2026.5.1337 | 1 | 0 |
| `Laser - ALT+F4 - Controller (Illusion Control - Time Manipulation)` | 2024.5.759 | 2 | 0 |
| `Laser - Accretion - Scrapper (Dark Melee - Shield Defense)` | 2026.5.1337 | 2 | 0 |
| `Laser - HAZMAT - PvE - Brute (Radiation Melee - Fiery Aura)` | 2024.5.759 | 2 | 0 |
| `Laser - NUKE - Corruptor (Fire Blast - Radiation Emission)` | 2024.5.759 | 3 | 0 |
| `Laser - PRIME - Blaster (Energy Blast - Energy Manipulation)` | 2024.5.759 | 3 | 6 |
| `Melee Arachnos Widow (Night Widow Training - Widow Teamwork)` | 2024.5.759 | 1 | 0 |
| `Petless Mastermind (Demon Summoning - Force Field)` | 2024.5.759 | 1 | 0 |
| `Stalker (Fiery Melee - Invulnerability)` | 2024.5.759 | 1 | 0 |
| `arsenal_dominator_incarnate` | 2026.1.1242 | 3 | 0 |
| `arsenal_dominator_incarnate_slotted` | 2026.1.1242 | 3 | 0 |
| `arsenal_dominator_noslots` | 2026.1.1242 | 3 | 0 |
| `hasten_2rech` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_global_endrdx` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_mixed_sets` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_noslots` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_set_bonuses` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_slotted` | 2026.5.1337 | 2 | 0 |

Builds that loaded without a remap: `Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training)`, `Laser - Fallout - Sentinel (Fire Blast - Radiation Armor)` and `wpss_tanker_leadership`.
The `shield_scrapper_set_bonuses_exemplar25` dump is a reload of `shield_scrapper_set_bonuses` and shares its row.
