# Builds Mids 4.0 loaded after remapping powers

`LegacyLoadMap.mhd` rewrote these builds on load, so each 4.0 dump describes the remapped build, not the file as saved.
Their differences from the 3.x dumps include the remap.
Each row is the compatibility summary `BuildManager.LoadFromFile` raised for the build (lineage Homecoming in every case).
No build moved Fitness powers or removed the legacy Fitness pool.
The remaps are the source of renamed powers in the "only in 3.x" and "only in 4.0" columns of [DIFF-REPORT.md](./DIFF-REPORT.md).

| Build | Legacy tag | Remapped powers | Remapped enhancements |
| --- | --- | --- | --- |
| `characters__account2__hollowmind__main` | 2026.5.1337 | 1 | 0 |
| `characters__account1__laser-alt-f4__main` | 2024.5.759 | 2 | 0 |
| `characters__account1__laser-accretion__main` | 2026.5.1337 | 2 | 0 |
| `characters__account1__laser-hazmat__pve` | 2024.5.759 | 2 | 0 |
| `characters__account1__laser-nuke__main` | 2024.5.759 | 3 | 0 |
| `characters__account1__laser-prime__main` | 2024.5.759 | 3 | 6 |
| `community__arachnos-widow__night-widow-melee` | 2024.5.759 | 1 | 0 |
| `community__mastermind__demon-force-field-petless` | 2024.5.759 | 1 | 0 |
| `community__stalker__fiery-invulnerability` | 2024.5.759 | 1 | 0 |
| `arsenal_dominator_incarnate` | 2026.1.1242 | 3 | 0 |
| `arsenal_dominator_incarnate_slotted` | 2026.1.1242 | 3 | 0 |
| `arsenal_dominator_noslots` | 2026.1.1242 | 3 | 0 |
| `hasten_2rech` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_global_endrdx` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_mixed_sets` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_noslots` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_set_bonuses` | 2026.5.1337 | 2 | 0 |
| `shield_scrapper_slotted` | 2026.5.1337 | 2 | 0 |

Builds that loaded without a remap: `characters__account1__laser-arachnos__bane-mace`, `characters__account1__laser-fallout__main` and `wpss_tanker_leadership`.
The `shield_scrapper_set_bonuses_exemplar25` dump is a reload of `shield_scrapper_set_bonuses` and shares its row.
