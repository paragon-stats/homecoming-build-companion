# Issue 29 Page 1 open beta: what changes in player powers

This is read-only intelligence. It records what the Homecoming Issue 29 Page 1 open-beta client changes in player powers relative to the live client,
decoded from the game files. Nothing here enters `data/canonical/`, `tests/fixtures/` or any file the engine reads until the release ships.

## Inputs

| Client | Folder | Date of `bin.pigg` |
| --- | --- | --- |
| Live | `assets/live` | 2026-10-17 |
| Open beta | `assets/beta` | 2026-11-03 |

SHA-256 of the decode inputs (`sha256sum`, run from `C:\Games\Homecoming\assets`):

```text
ae5752be81cc73eeb297c3216633f54c45eec263fb9f5e06e58c9ea103c5d089  live/bin.pigg
cdacbd77e1029369c659c4783bc08d37c0c0941917b77074b96ebd86391b3a51  live/bin_powers.pigg
d9613446c7c20bd25a1f685cae6599d431c730c83d50ba38af56f9b87dd5acee  live/bin_fxinfo.pigg
36ccd0364c5b6e6c27063f7e1b10f88cc27b0067f213ede69e82d876ee33ecb3  beta/bin.pigg
26c58790228b59d5e4b64dd1b2d5322f8033fe2d487430da75bb7065207fb7d2  beta/bin_powers.pigg
5c9e31edab55d699701330138243e6361ae088fc6de8cbabbf866e5ca611671c  beta/bin_fxinfo.pigg
```

Decoder: the CoH Sidekick bin-crawler (`tools/bin-crawler`, commit `7e2db70`, AGPL-3.0, standard library only, no network).
The decoder's source registry names no install on this workstation.
A root for `C:/Games/Homecoming/assets` was added to its `homecoming` and `brainstorm` datasets in a scratch clone, and nothing else was changed.
The open beta is the decoder's `brainstorm:open_beta` ring.

```bash
python -I bin_crawler/export_powers.py --source homecoming:live --output-dir <scratch>/live
python -I bin_crawler/export_powers.py --source brainstorm:open_beta --output-dir <scratch>/beta
```

Both runs used the decoder's default player categories. A scratch script then walked the two export trees.
It keyed each power by its full name.
It keyed each effect by the lower-cased group requirement, attributes, aspect, type, target, application type and an ordinal.
It compared recharge, endurance, activation time, range, radius, arc, targets, accuracy, available level and requirements.
For each effect it compared table, scale, magnitude, duration, target, aspect and stacking.
Display text and effects were ignored.
Requirement text differs between the clients only in letter case (`Cur.` against `cur.`), so it was compared lower-cased.

## Limits of this decode

The decoder reads the live file cleanly and drops 89 power records from the beta file (`Read of 4 bytes ... would exceed record boundary`).
The beta file therefore uses a record layout the decoder does not yet read for those powers.
A power that is in the live export and missing from the beta export is not a removal. It is a power the decoder could not read.
The 43 unreadable player powers are listed below, because they are the powers the beta touched in a way the decoder cannot see.
Their numbers cannot be compared, so this document says nothing about their values.

`Epic.Sentinel_Electricity_Mastery.Rehabilitating_Circuit` parsed partly (effect groups and post-effect tail failed), so its row here is not trustworthy.

Effect comparison covers PvE (critter) values. An effect keyed on `enttype player` is the PvP value and is mentioned only when it is the sole change.

## Summary

Counts are for the two exports. "Player" means archetype power sets, pools, epic pools, inherents and incarnates.
"Supporting" means redirects, pets, villain pets, mission-maker attacks and temporary powers.

| Measure | Player | Supporting |
| --- | --- | --- |
| Powers decoded, live / beta | 5494 / 5451 | 5795 / 5781 |
| Powersets added | 0 | 1 (`Redirects.Spines`) |
| Powersets removed | 0 | 0 |
| Powersets with changed numbers | 15 | 10 |
| Powers added | 0 | 5 |
| Powers removed | 0 | 0 |
| Powers changed | 85 | 32 |
| Powers in live, unreadable in beta (not removals) | 43 | 19 |
| Powers differing only in letter case of the name | 6 (Alpha Silent) | 0 |

Totals over everything the decoder exported: 11289 live powers, 11232 beta powers, 2248 and 2249 powersets, 117 changed powers.

Powers added (all supporting): `Redirects.Spines.SpinePoison`, `Redirects.Rad_Melee.Atom_Smasher_AoE`,
`Villain_Pets.Dominator_RadioactiveAssault_Snipe.Atom_Smasher_AoE`, `Redirects.Sonic_Melee.Assassins_Whisper`, `Temporary_Powers.Temporary_Powers.Poison`.

Player powers the decoder could not read in the beta file:

| Power set | Archetypes | Power |
| --- | --- | --- |
| Electrical Blast | Blaster, Corruptor, Defender, Sentinel | Tesla Cage |
| Psychic Blast | Blaster, Corruptor, Defender | Scramble Thoughts |
| Storm Blast | Blaster, Corruptor, Defender, Sentinel | Chain Lightning |
| Electrical Melee | Brute, Scrapper, Stalker, Tanker | Chain Induction |
| Kinetic Attack | Brute, Scrapper, Stalker, Tanker | Focused Burst |
| Shock Therapy | Controller, Corruptor, Defender, Mastermind | Empowering, Energizing, Insulating, Rejuvenating Circuit |
| Electric Control | Controller, Dominator | Jolting Chain, Synaptic Overload |
| Pyrotechnic Control | Controller, Dominator | Sparkling Field |
| Martial Assault | Dominator | Trick Shot |
| Inherent | all | Vulnerability |

## Spines (Brute, Stalker, Tanker) and Quills (Scrapper)

The poison damage-over-time effect (0.8 chance, 1 s ticks) is removed from every attack in the set.
A new poison mechanic takes its place. `Redirects.Spines.SpinePoison` sets a 10 s mode and grants a power.
`Temporary_Powers.Temporary_Powers.Poison` is an automatic counter, and its help text says various powers are enhanced by the stacks on you.
Direct damage rises and some attacks cost more recharge and endurance. Values are PvE.

| Power | Archetypes | Change |
| --- | --- | --- |
| Barb Swipe | all four | Toxic DoT 0.1 over 3.1 s removed |
| Throw Spines, Quill Throwing | Scrapper, Stalker, Tanker | Lethal 1.09 to 0.901; Toxic DoT 0.1 over 7.1 s removed |
| Throw Spines | Brute | Toxic DoT 0.1 over 7.1 s removed; damage unchanged |
| Spine Burst, Fling Quills | all four | Recharge 10 to 13 s; endurance 10.192 to 12.688 |
| Spine Burst, Fling Quills | all four | Lethal 0.6 to 0.7507; Fire 0.2719 to 0.3385 (not Stalker); Toxic DoT 0.1 over 6.1 s removed |
| Impale | all four | Recharge 10 to 9 s; endurance 10.192 to 9.359; activation 2.23 to 1.433 s; grants a power |
| Impale | all four | Lethal 1.64 to 1.8; Fire 0.882 to 0.81 (not Stalker); Toxic DoT 0.2 over 15.1 s removed |
| Lunge | all four | Recharge 5 to 6 s; endurance 6.032 to 6.864; Lethal 1.08 to 1.32; Fire 0.522 to 0.594 (not Stalker) |
| Lunge | all four | 14 s mode 57 and 16 s Global_Chance_Mod 0.15 removed; Toxic DoT 0.1 over 6.1 s removed |
| Ripper | all four | Toxic DoT 0.2 over 7.1 s removed; grants a power |
| Assassin's Impaler | Stalker | Slow enhancements allowed (Slow Movement set category added) |
| Assassin's Impaler (villain pet) | supporting | Gains -Speed 0.5 over 15 s, -Jump 0.5 and -Recharge 0.2 over 10 s |

## Radiation Melee, Radioactive Assault, Radiation Manipulation

Archetypes: Brute, Scrapper, Stalker and Tanker (Radiation Melee), Dominator (Radioactive Assault), Blaster (Radiation Manipulation).
Contamination is reworked. Every attack gains a 10 s marker effect gated on you not holding `Radiation_Melee_Contaminated` or its immunity.
The Radiation Siphon revoke is replaced by a -Regeneration effect (scale -1.0, 10 s).
`Radiation_Melee_Contaminated` has its grant chance cut from 1.0 to 0.0.
For Scrapper, the secondary Smashing and Toxic parts of each attack are merged into Energy. The total scale is unchanged.

| Power | Archetypes | Change |
| --- | --- | --- |
| Atom Smasher | all, plus Blaster | Activation 2.93 to 2.8 s |
| Atom Smasher | Scrapper | Energy 0.3875 to 1.55; Toxic 1.1625 removed |
| Contaminated Strike | Scrapper | Smashing 0.21 and Energy 0.63 become Energy 0.84 |
| Devastating Blow | Scrapper | Smashing 0.77 and Energy 2.31 become Energy 3.08 |
| Radioactive Smash | Scrapper | Smashing 0.37 and Energy 1.11 become Energy 1.48 |
| Radiation Siphon | all | Activation 2.23 to 1.93 s |
| Radiation Siphon | Scrapper | Smashing 0.49 and Energy 1.47 become Energy 1.96 |
| Proton Sweep | Scrapper | Toxic DoT 0.2681 and Energy DoT 0.0894 (3.1 s) become Energy 1.43, direct |
| Electron Haze | Dominator | Activation 2.37 to 2.17 s; knockback table Ranged_Knockback 1.0 to Ranged_Ones 0.67 |
| X-Ray Beam | Dominator | Activation 1.67 to 1.33 s |
| Irradiated Ground (pet) | all | Fire damage becomes Toxic damage at the same scales |

Redirect powers (supporting) change as well.
Devastating Blow AoE: radius 17.8 to 15, targets 5 to 11, endurance 16.016 to 0.
Radiation Siphon AoE and Radioactive Smash AoE: targets 5 to 6, endurance to 0.
Contaminated Strike AoE: targets 5 to 6.
The new `Atom_Smasher_AoE` redirect has 22 s recharge, radius 8, 4 targets and Toxic 0.155.

## Sonic Melee (Brute, Scrapper, Stalker, Tanker)

Attune stops being a toggle and becomes a self-centred click that sets sonic modes on you and on nearby foes.
Every attack then adds a 15 s effect, at scale -1.6, that lowers the target's resistance to debuffs.
It covers defense, endurance, to-hit, speeds, regeneration, recovery and recharge.

| Power | Archetypes | Change |
| --- | --- | --- |
| Attune | all four | Toggle to Click; recharge 0.5 to 15 s; radius 0 to 20; targets 0 to 10; target Foe to Self |
| Attune | all four | Endurance 0.104 to 0.13 (Stalker 1.6309) |
| Sonic Thrust, Earsplitter, Strident Echo, Sonic Clap, Deafening Wave | all four | Adds the debuff-resistance effect and an `Execute_Power` |
| Sandman's Whisper | Brute, Scrapper, Tanker | Adds the debuff-resistance effect and an `Execute_Power` |
| Sonic Clap | all four | Arc 180 to 130 degrees |
| Sonic Clap, Deafening Wave (redirects) | supporting | Radius 8 to 15; target Foe to DeadOrAliveFoe |
| Assassin's Whisper | Stalker | New redirect; the meter-gated Energy 6.0 and sleep effects are removed |
| Assassin's Whisper (villain pet) | supporting | Resistance debuff -1.2 to -1.3333 |

Two Sonic Melee redirects (`Sonic_Thrust`, `Strident_Echo`) are among the powers the decoder could not read in the beta file.

## Sentinel Electricity Mastery (epic pool)

`Rehabilitating_Circuit` reads as recharge 32 to 80 s, endurance 13 to 1, range 80 to 0 and no heal effect.
The decoder reported a parse failure on this record, so treat these as unreliable.
Re-read the power once the decoder reads the beta layout.

## Supporting powers

Mission-maker attack copies (8 powers), villain-pet copies (15) and one pet mirror the changes above.
The Dominator Radioactive Assault villain-pet copy of Devastating Blow also changes radius 8 to 15 and targets 5 to 11.
Alpha Silent powers (`Defense_Debuff_*`, `ToHit_Debuff_*`) differ only in the letter case of their names. Their numbers were not compared.

## Patch notes cross-check

No Issue 29 Page 1 record exists.
`python -m scripts.forum_scrape.patch_notes --source forum-beta` ran on 2026-11-07, honoured robots.txt and a 2 s delay, and wrote 0 entries.
The newest `forum-beta` record in `data/patches/index.jsonl` is dated 2022-10-16, and no record carries an `issue-29` label.
There is nothing to cross-check the decode against yet. Re-run the collector when the open-beta thread is posted.

## What this means for v1.0.0

Spines, Quills, Radiation Melee, Radioactive Assault and Sonic Melee change numbers the CP6.2 family reads.
These are damage scales, damage types (Scrapper Radiation Melee becomes all Energy), recharge, endurance and activation time.
Activation time and recharge feed the chain planner directly.
Examples are Impale 2.23 to 1.433 s, Atom Smasher 2.93 to 2.8 s, Radiation Siphon 2.23 to 1.93 s and X-Ray Beam 1.67 to 1.33 s.
The removed poison and contamination damage-over-time effects, the new Poison stack counter and the gated effects reach proc and aspect handling, because they are conditional effects on a temporary power.

No powerset is new, but a Light Affinity-style supplement will be needed after release for the 43 powers the decoder could not read.
They cover Electrical Blast, Psychic Blast, Storm Blast, Electrical Melee, Kinetic Attack, Shock Therapy, Electric Control, Pyrotechnic Control, Martial Assault and the Vulnerability inherent.
The decoder needs updating for the beta record layout first.
The new Spines, Radiation Melee and Sonic Melee redirects and the Poison temporary power also need adding to canonical data at release.

The beta is read-only intelligence, and nothing in this document enters canonical data until release.
