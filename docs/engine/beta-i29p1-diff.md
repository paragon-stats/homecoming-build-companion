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

The unreadable powers are almost all chain or jump-target powers: Tesla Cage, Chain Induction, Jolting Chain, Synaptic Overload, the Shock Therapy circuits, Chain Lightning, Scramble Thoughts and Vulnerability.
This is an inference, not a finding: it suggests Issue 29 Page 1 changed the chain-effect record layout rather than those powers' numbers.
The Sidekick decoder, built against the live schema, will need updating before those powers can be read.

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

Attune becomes a self-centred power that sets sonic modes on you and on nearby foes. Only its redirect copy changes type, from Toggle to Click.
Every attack then adds a 15 s effect, at scale -1.6, that lowers the target's resistance to debuffs.
It covers defense, endurance, to-hit, speeds, regeneration, recovery and recharge.

| Power | Archetypes | Change |
| --- | --- | --- |
| Attune | all four | Recharge 0.5 to 15 s; radius 0 to 20; targets 0 to 10; target Foe to Self (the redirect copy goes Toggle to Click) |
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

## Patch notes

Source: the opening post of the thread "Open Beta Patch Notes for November 3rd, 2026", posted 2026-11-03 by The Curator (Developer group):
<https://forums.homecomingservers.com/topic/66292-open-beta-patch-notes-for-november-3rd-2026/#findComment-826387>.
Only that post is used. Replies in the thread are not evidence and are not cited.
The page was fetched with the repo's crawler user agent and a 2 s delay.
The forum's signal is ai-train=no, use=reference, so the notes are paraphrased and only their numbers are repeated.
The collector `python -m scripts.forum_scrape.patch_notes --source forum-beta` wrote 0 entries on 2026-11-07, and its newest record is dated 2022-10-16.
No record is committed for this thread, so this section is the only record of it.

The notes cover graphics, character creation, tasks and fixes as well as powers.
For player powers they name three sets as reworked (Sonic Melee, Spines, Radiation Melee with Radioactive Assault).
They add an Atom Smasher cast-time change shared with Radiation Manipulation, and three fixes: Teleport, Group Fly and Light Affinity Spotlight range.
Each row below pairs the notes' statement with what the decode shows.

### Confirmed by the notes

| Finding | What the notes say | Decode |
| --- | --- | --- |
| Sonic: -1.6 debuff resistance | All Sonic Melee powers inflict scale -1.6 resistance versus debuffs for 15 s | Same value and duration on every attack |
| Sonic: Attune cost and recharge | Endurance 0.208 to 0.52 per second; recharge 0.5 to 15 s | Per-tick cost 0.104 to 0.13; recharge 0.5 to 15 s |
| Sonic: Attune on self | Toggle centred on self; more than one target can be Attuned | Self target, radius 20, 10 targets |
| Sonic Clap arc | 180 to 130 degrees | 3.1416 to 2.2689 rad |
| Sonic Clap and Deafening Wave echo | Echo in a 15 ft radius on up to 5 targets | Redirect radius 8 to 15 |
| Assassin's Whisper debuff | Resistance -1.2 to -1.3333 (also Sandman's Whisper) | Same on the villain-pet copies |
| Spines: Spine Poison | DoT removed from attacks; shared Spine Poison and a Poisoned status, up to 10 stacks of 15 s | DoT removed; `SpinePoison` redirect (10 s mode) and `Poison` temporary power added |
| Spines: Lunge | Recharge 6 s, damage 1.08 to 1.32, endurance 6.032 to 6.864 | Same |
| Spines: Spine Burst | Recharge 10 to 13 s, endurance to 12.688, damage 0.6 to 0.7507 | Same |
| Spines: Impale | Recharge 9 s, endurance 9.359, damage 1.8, cast 2.23 to 1.433 s, 2 stacks | Same numbers; a granted power added |
| Spines: Throw Spines | Damage 1.09 to 0.901 | Same |
| Spines: Assassin's Impaler | Slow and -Jump 0.5 for 15 s, -Recharge 0.2 for 10 s, accepts slow enhancements | Same effects and categories |
| Radiation: Siphon | No longer removes Contamination; -100% regeneration for 10 s, doubled when Contaminated | Revoke removed; two -Regeneration effects, one gated on Contaminated |
| Radiation: Scrapper criticals | 100% Energy; Proton Sweep one hit of scale 1.43 Energy | Smashing and Toxic parts merged into Energy; Proton Sweep Energy 1.43 direct |
| Radiation: Atom Smasher | Cast 2.93 to 2.8 s; own Contamination AoE, Toxic 0.155, 8 ft, main target plus up to 3 | `Atom_Smasher_AoE` redirect: radius 8, 4 targets, Toxic 0.155 |
| Radiation: Devastating Blow AoE | 15 ft, 11 targets | Radius 17.8 to 15, targets 5 to 11 |
| Radiation: other AoEs | Contamination AoEs hit the main target plus 5 others | Targets 5 to 6 |
| Radiation: casts | Siphon 1.93 s, X-Ray Beam 1.33 s, Electron Haze 2.17 s | Same |
| Radiation: Irradiated Ground | Deals Toxic instead of Fire | Fire becomes Toxic at the same scales |
| Radiation: Haze knockback | Knockback reduced to Knockdown | Knockback table scale 1.0 to 0.67 |

### Contradicted or unresolved

- **Attune is not a click.** The notes say Attune is a toggle on self.
  The decode shows Toggle to Click only on the redirect copy, so the first draft of this document wrongly applied it to the player power. That is corrected above.
- **Quills.** The notes say Spine Poison applies to all Spines attacks except Quills.
  The decode shows the Scrapper Quills attacks also lose their Toxic DoT and gain the same numeric changes as Spines.
  The notes do not say what Quills uses instead.
- **Spines Fire components.** The decode shows Fire scales rising with Lethal on Spine Burst, Impale and Lunge. The notes mention Lethal damage only.

### In the decode, not in the notes

- Endurance 0 on the Radiation Melee Devastating Blow, Siphon and Smash AoE redirects.
- The Lunge mode 57 and `Global_Chance_Mod` effects are removed.
- Sonic Clap and Deafening Wave redirects target DeadOrAliveFoe.
- The Sentinel Electricity Mastery `Rehabilitating_Circuit` values, which are unreliable (see above).

### In the notes, not found by the decode

These are candidates for the 89 unreadable records, or for fields the diff did not compare.

- **Sonic Melee:** Sonic Thrust knockdown chance 10% to 15%.
  Sonic Clap damage 0.7838 to 0.8844 total. Strident Echo Scrapper critical 1.16 to 1.32.
  Attune damage scale 0.25 to 0.2 (0.4 on repeat hits), tick 2 s to 1 s, -Res scale 0.6 to 1.2, and 10% potency.
  Assassin's Whisper (Stealth) grants Sound Booster.
  The Sonic Thrust and Strident Echo redirects are in the unreadable list, which fits.
- **Spines:** Ripper knockdown chance 60% to 100%.
  Ripper bonus Toxic by stacks (scale 0.13 at 1 stack to 0.85 at 10).
  Spine Poison base scale 0.025 per second for 9.1 s, rising 1.9x per stack to 0.25.
  Assassin's Impaler Stealth grants 6 stacks.
- **Radiation:** Contamination chance rises: Proton Sweep 21% to 70%, Electron Haze 34% to 50%, Proton Volley quick 30% to 66% and sniper 30% to 100%.
  Electron Haze knockdown chance 25% to 50%. Assassin's Corruption Irradiated Ground stacks.
  Chance changes appear in the decode only as paired add and remove entries on those attacks, which is consistent but not quantified.
- **Fixes:** Teleport and Anti-Teleport Grenade, Group Fly opt-out and Spotlight range.
  The decode found no change on these powers, so the fix is in a field the diff did not compare.

### Powers the notes do not mention

The notes announce no change to these sets, which are the ones the decoder could not read:
Electrical Blast, Psychic Blast, Storm Blast, Electrical Melee, Kinetic Attack, Shock Therapy, Electric Control, Pyrotechnic Control, Martial Assault and the Vulnerability inherent.
So the notes give no sign that their numbers changed.
That supports the inference above that the chain-effect record layout changed, though the notes do not say so.

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
