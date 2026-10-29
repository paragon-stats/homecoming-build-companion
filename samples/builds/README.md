# Sample builds

Real builds saved by MidsReborn, kept as fixtures.
One build per archetype is curated; the rest of the folder holds the four community captures the `.mxd` reader is checked against.

## Coverage

| Archetype | File | Origin |
| --- | --- | --- |
| Arachnos Soldier | `operator-builds/Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training).mbd` | operator build, local capture |
| Arachnos Widow | `community/Mids Archives/Widow/Melee Arachnos Widow (Night Widow Training - Widow Teamwork).mbd` | Mids archive |
| Blaster | `operator-builds/Laser - PRIME - Blaster (Energy Blast - Energy Manipulation).mbd` | operator build, local capture |
| Brute | `operator-builds/Laser - HAZMAT - PvE - Brute (Radiation Melee - Fiery Aura).mbd` | operator build, local capture |
| Controller | `operator-builds/Laser - ALT+F4 - Controller (Illusion Control - Time Manipulation).mbd` | operator build, local capture |
| Corruptor | `operator-builds/Laser - NUKE - Corruptor (Fire Blast - Radiation Emission).mbd` | operator build, local capture |
| Dominator | `operator-builds/Hollowmind - Dominator (Darkness Control - Psionic Assault).mbd` | operator build, local capture |
| Mastermind | `community/Mids Archives/Mastermind/Petless Mastermind (Demon Summoning - Force Field).mbd` | Mids archive |
| Scrapper | `operator-builds/Laser - Accretion - Scrapper (Dark Melee - Shield Defense).mbd` | operator build, local capture |
| Sentinel | `operator-builds/Laser - Fallout - Sentinel (Fire Blast - Radiation Armor).mbd` | operator build, local capture |
| Stalker | `community/Mids Archives/Stalker/Stalker (Fiery Melee - Invulnerability).mbd` | Mids archive |
| Tanker | `operator-builds/Laser - QUANTUM - Tanker (Super Reflexes - Energy Melee).mbd` | operator build, local capture |

Defender, Peacebringer and Warshade have no sample: the archive holds only `.mxd` files for them.

## What was left out, and why

- The Mastermind archive builds other than the Petless one carry pet powers the current MidsReborn database does not define, so they cannot resolve to powers.
- An earlier HAZMAT Brute placed slots the level schedule had not granted; its PvE variant replaced it.
- The remaining 3,133 `.mxd` files and the other archive `.mbd` files stay in the shared stash for the `.mxd` to `.mbd` conversion (issue 22).

## How they are exercised

- `test_mbd.py` parses every `.mbd` under this folder and resolves the Arachnos Soldier build to powers and slots.
- `test_mbd_writer.py` round-trips every build through `dump_mbd` and the format invariants.
- `test_sample_builds.py` runs, for each curated build, the round trip, both validator registries, and the unenhanced totals against the archetype caps.
  Each build's Mids dump is committed under `src/coh_engine/tests/fixtures/mids/builds/<file name without .mbd>`.
  Three builds are listed there as still refused by the engine (an unported expression evaluator or movement split).
- `python -m coh_engine validate "<file>.mbd"` validates one file against the committed dump tables.
