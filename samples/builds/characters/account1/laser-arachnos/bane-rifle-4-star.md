# Bane Spider Soldier-Bane Spider Training Arachnos Soldier - Ranged Rifle - 4-Star

## Checks run on this file

Every check below was run on this build itself. No figure is borrowed from another variant.

1. **Repository validator** (`python -m coh_engine validate`): no violations.
2. **Slotting check** (`CohEngine.Cli.exe validate --mids-dir tests/fixtures/mids --slotting`): no violations.
3. **Mids dump harness:** totals written; the expected E21 and E14 exits only. A second dump of the delivered `.mbd` gives totals
   byte-identical to the delivered `.mids-totals.json`.
4. **Hit chance against +4:** 95% on the six attacks on keys 1 to 6, Surveillance, Wide Area Web Grenade and Boxing (lowest before
   the cap: 99.1%). Brawl, never pressed, 93.9% with its plain Accuracy IO.
5. **Chains:** single target and 10-target spawn; bars as measured and with the proc estimate.
6. **Enhancement Diversification audit:** every power, every aspect, measured on this build.
7. **Exemplar:** rows at 25, 35, 45 and 50. Parity at 50 against the harness matches every field but hit points (0.10 apart). The
   hand-worked check passes: +42.5% recharge at exactly exemplar 47 (five level 50 Luck of the Gambler globals and Adjusted Targeting's
   five-piece bonus), nothing at 42 to 46 or 48 to 49, under both floor modes.
8. **Survival score:** both tiers, both damage mixes, against the previously delivered build, with the hand check.
9. **Boost evidence:** each boosted piece's value before and after (all ×1.25). Set bonuses and globals are byte-identical. 180 boosters.
10. **Slot check** against the previously delivered file: no slot moved; 67 added slots.
11. **Granted slots:** every slot of every power is filled (none empty).
12. **File order:** the level 1 primary pick (Single Shot) comes before the secondary pick (Bane Spider Armor Upgrade), and the pools
    are listed in the order they are first picked (Leaping 6, Speed 14, Fighting 28, Leadership 38): passes. Only the pool order changed
    against the file delivered with these decisions; its totals are byte-identical and no slot or piece moved.

Before any change, the previously delivered build was rebuilt from its spec: the rebuilt `.mbd` is byte-identical to the delivered one,
and its dump is byte-identical to the delivered `.mids-totals.json`.

## What this revision changes

Against the previously delivered build. **No slot moves; no set changes.**

| Measure | Previously delivered | **This build** |
| --- | --- | --- |
| Defense melee / ranged / AoE | 59.30 / 61.17 / 61.17 | **60.94 / 62.81 / 62.81** |
| Typed defense S/L, F/C, E/N, Tox/Psi | 53.98, 62.42, 54.92, 37.85 | 55.63, 64.06, 56.56, 38.83 |
| Resistance S/L, F/C, E/N, Tox, Psi | 51.26, 35.98, 23.23, 27.73, 32.73 | 50.85, 35.41, 22.66, 27.16, 32.16 |
| Hit points / regeneration (`HPRegen`) | 1,542.2 / 1.67 | 1,542.2 / 1.67 |
| Knockback protection | 0 | **4 points** |
| Hit chance vs +4 | 95% on every attack | 95% on every attack (lowest before the cap 99.1%) |
| Survival score, team simple / logs, 4-star simple / logs | 100 / 100 / 100 / 100 | **99.2 / 99.2 / 106.4 / 106.4** |
| Bars, single / group (as measured) | 103.8 / 90.1 s | **119.2 / 101.8 s** |
| Bars with the procs (estimate) | 294 / 206 s | 466 / 278 s |
| Damage, single / group | 217.1 / 1,026.8 dmg/s | 219.4 / 1,028.3 dmg/s |
| Toggle cost `EndUse` / recovery | 1.3615 / 3.17 end/s | 1.3208 / 3.22 end/s |
| Recharge, Hasten | +162.5%, 125.6 s | +162.5%, 125.6 s |
| Standing to-hit | +31.75% | +25.86% (Kismet is out) |
| Enhancement Boosters | 40 (8 common IOs) | 180 (8 common IOs, 28 set pieces) |

What changes, power by power:

- **Combat Jumping:** Blessing of the Zephyr: Knockback Reduction (crafted 10) in place of Kismet: Accuracy +6%. Knockback protection
  0 → 4. Every attack, Surveillance and Wide Area Web Grenade stay at 95% against +4 without Kismet.
- **Granted slots that were empty:** Swift (Run Speed IO), Hurdle (Jump IO), Brawl (Accuracy IO), Sprint (Run Speed IO), Rest
  (Endurance Modification IO), all plain level 50 IOs.
- **Unique and global levels** set to the best level by the wiki's exemplar rules: Steadfast Protection 10, Unbreakable Guard +Max HP 20,
  Aegis 25, Reactive Defenses 20, Preventive Medicine 20, Regenerative Tissue 10, Panacea attuned. Luck of the Gambler pieces stay at 50.
- **Boosts:** 28 set pieces, where at least half the boost survives Enhancement Diversification (see "Enhancement Boosters").
- **Unchanged by the operator's choice:** Frag Grenade keeps Superior Dominion of Arachnos and its knockback; Heavy Burst keeps Ragnarok
  five plus the Annihilation proc; Stamina keeps two pieces; every Luck of the Gambler piece is crafted at 50.

**What it costs.** Resistance falls 0.41 S/L and 0.57 on the other types: Steadfast Protection at level 10 (+5) holds less resistance
than the level 30 piece. Standing to-hit falls 6 points with Kismet out. The team-tier survival score falls 0.8 for that resistance.
140 more boosters.

**What it buys.** +1.64 defense in every position; 4 points of knockback protection; longer bars (119.2 / 101.8 s against
103.8 / 90.1 s); exemplared, +3% defense from 7 (Steadfast at 10), +Max HP from 17 and the absorb from 17.

## Measured alternatives (not taken)

Each is a full harness run on this build with the one change. Survival columns are team simple / team logs / 4-star simple / 4-star logs
at 10/45/45, with this file = 100.

| Alternative | Survival score | Bars single / group | Dmg/s single / group |
| --- | --- | --- | --- |
| **This file** | 100 / 100 / 100 / 100 | 119.2 / 101.8 s | 219.4 / 1,028.3 |
| Frag Grenade knockdown (Overwhelming Force 6, Kismet back) | 97.7 / 96.5 / 100.2 / 99.0 | 124.8 / 112.8 s | 215.8 / 1,006.0 |
| Luck of the Gambler pieces at 25 | 100.0 / 100.0 / 97.0 / 97.0 | 119.2 / 101.8 s | 219.4 / 1,028.3 |
| Ragnarok's sixth piece in place of the Annihilation proc | 101.2 / 102.6 / 101.2 / 102.6 | 119.2 / 101.8 s | 219.4 / 1,028.3 |
| Stamina's third piece (Tactical Training: Leadership 6 → 5; one slot moves) | 98.3 / 99.4 / 98.3 / 99.4 | 141.3 / 117.5 s | 219.4 / 1,028.3 |

- **Knockdown:** Toxic and Psionic resistance −6, HP −16, recharge −10%; ranged +1.25 and E/N +2.5 defense. Not taken: the operator's
  call, as most four-star enemies are immune to knockdown. It keeps Kismet: without Superior Dominion's +15% accuracy, Surveillance and
  Wide Area Web Grenade drop to 91.7% against +4 if the knockback piece stays (measured); Overwhelming Force gives the 4 points instead.
- **Luck of the Gambler at 25:** defense −0.70 in every position at 50; +37.5% recharge from exemplar 22 to 46. Not taken: the operator
  chose level 50 pieces.
- **Ragnarok's sixth piece:** Toxic and Psionic resistance +6, the higher score; the team's −12.5% resistance proc is lost. Not taken:
  the operator keeps the proc.
- **Stamina's third piece:** S/L resistance −3.75, HP +20, the longest bars. Not taken: the operator keeps Stamina at two pieces.

## Corrections to the previous write-up

- **Piece kinds.** It said "set pieces are level 50, attuned or superior". The Luck of the Gambler, Ragnarok, Apocalypse, Adjusted
  Targeting, Analyze Weakness, Unbreakable Guard, Gaussian's and Health pieces are crafted, not attuned (Panacea alone is attuned, as in
  game). The slotting list below names each piece's kind and level.
- **Uniques.** Its "Unique placement" table listed Luck of the Gambler: Defense/Increased Global Recharge Speed and Performance Shifter:
  Chance for +End as uniques. Neither carries the data's one-per-character `slot_requires` in `data/canonical/boost_sets`.
- **Exemplar levels.** Its table used the power's pick level and the slot's level (Mids' rule). By the wiki's rules a crafted piece works
  from its own level − 3, whatever the slot: the level 50 globals work only from 47, not from 22 to 33.
- **Preventive Medicine absorb:** at most once every **120 s** in the data (see "What the Health and Stamina procs give").
- **Endurance bars** are now shown with and without the Panacea and Performance Shifter procs.
- **Hasten** is a click power that Mids counts as always on (see "What the totals count").

## Key map

The operator's tray has four rows: keys 1 to 6 for the attack chain, the ALT row for opportunity clicks, the CTRL row for self-buffs,
the Hybrid and the Destiny, and a fourth bar for toggles and travel. Hasten is on auto-fire (key 0). The 1 to 6 and ALT rows hold
9 click powers, against a limit of 12.

| Row | Key | Power | What it is for |
| --- | --- | --- | --- |
| Primary | 1 | Single Shot | Fastest filler: 1.12 s recharge. −7.5% defense on the target |
| Primary | 2 | Burst | Best rifle hit per second (293.0 per activation). −7.5% defense |
| Primary | 3 | Poisonous Ray | Heaviest single-target hit (344.5, toxic). −7.5% defense for 15 s. Range 48 ft |
| Primary | 4 | Heavy Burst | Cone, 10 targets; Annihilation's −12.5% resistance proc |
| Primary | 5 | Venom Grenade | −20% resistance (−40% toxic) for 16 s on up to 10 targets; Superior Frozen Blast's immobilize proc |
| Primary | 6 | Frag Grenade | AoE (228.2 per target), knockback; Superior Dominion's −damage and fear proc |
| ALT | ALT+1 | Surveillance | −20% resistance and −15% defense for 20 s on one target; recharges in 14.3 s |
| ALT | ALT+2 | Wide Area Web Grenade | Immobilize magnitude 3 on up to 16 foes; stops their knockback; −50% speed |
| ALT | ALT+3 | Ion Core Final Judgement | Nuke: 674.6 per activation, 90 s recharge |
| ALT | ALT+4 to ALT+6 | empty | All 24 picks are in use |
| CTRL | CTRL+1 | Build Up | +80% damage and to-hit for 10 s; recharges in 25.5 s |
| CTRL | CTRL+2 | Assault Radial Embodiment | Hybrid; on for the hard fights |
| CTRL | CTRL+3 | Barrier Core Epiphany | Destiny, on the team's Barrier rotation |
| Auto-fire | 0 | Hasten | +70% recharge for 120 s; recharges in 125.6 s, about 96% uptime (arithmetic) |
| Fourth bar | — | Tactical Training: Maneuvers, Assault and Leadership; Maneuvers, Assault, Tactics | Leadership toggles |
| Fourth bar | — | Tough, Weave, Cloaking Device, Combat Jumping | Survival toggles; Combat Jumping is also the travel power |
| Fourth bar | — | Sprint | Inherent travel toggle |
| — | — | Boxing | Prerequisite for Tough, not on a key |

Auto powers need no key: Bane Spider Armor Upgrade, Combat Training: Defensive and Mental Training.

## Slotting rules applied

- **Hasten.** Two Recharge Reduction IOs at +5: the operator's rule, the stated exception to the plain-IO rule.
- **Luck of the Gambler.** Every piece crafted at 50 (the operator's choice). Combat Jumping, Weave, Cloaking Device and Maneuvers hold
  three pieces each, Tactical Training: Maneuvers five. No defense toggle holds a lone piece.
- **Other globals and uniques at the best level** by the wiki's exemplar rules (table under "Unique placement").
- **Health survivability pieces.** Numina's Convalescence, Miracle, Panacea, Preventive Medicine: Chance for +Absorb and Regenerative Tissue.
- **Sets outrank diversification.** No purple, Winter or archetype set is shortened.
- **Boosts where they earn their boosters** (see "Enhancement Boosters").
- **Single-slot powers and granted slots take the most useful enhancement that adds value** (table below).
- **Survivability is layers that multiply** (`docs/survivability-planning.md`); variants are compared by the survival score.

### Single-slot powers and granted slots

Allowed enhancement types and set types are from `boosts_allowed` in `data/canonical/powers` and the Mids fixture's `SetTypes`.

| Power | Slotted | Why it is the most useful choice |
| --- | --- | --- |
| Combat Training: Defensive | Shield Wall: +Res (Teleportation), +5% Res (All), crafted 50 | The PvP global: +5% resistance to every type at every level |
| Tactical Training: Assault, Assault, Tactics | Endurance Reduction IO, crafted 50, +5 | They take only Endurance Reduction or Recharge; recharge does nothing on a toggle |
| Mental Training | Run Speed IO, crafted 50, +5 | Takes only Run Speed or Fly Speed; no sets; this build has no flight |
| Boxing | Accuracy IO, crafted 50, +5 | Never pressed; as delivered |
| Wide Area Web Grenade | Accuracy IO, crafted 50, +5 | Without Kismet the unboosted IO gives 93.9% (arithmetic); the +5 gives 95% (99.1% before the cap) |
| **Brawl** | **Accuracy IO, crafted 50** | It was empty (78.6% against +4); never pressed, so a plain IO (93.9%) |
| **Swift** | **Run Speed IO, crafted 50** | Takes Run Speed or Fly Speed; no flight here |
| **Hurdle** | **Jump IO, crafted 50** | Takes only Jump; Combat Jumping is the travel power |
| **Sprint** | **Run Speed IO, crafted 50** | Takes Run Speed, Jump or Endurance Reduction; Sprint is off in combat, so travel speed is its use |
| **Rest** | **Endurance Modification IO, crafted 50** | Takes Endurance Modification, Heal or Interrupt; the bar is the weak point between fights |

Rest and Sprint are off in the totals. The prestige travel power is not in the Mids file.

### Knockback protection

The previously delivered build had none. Sprint cannot take Blessing of the Zephyr (a Universal Travel set; the validator refuses it
there, H-ENH-001, measured); Swift, Hurdle and Mental Training take no sets. Combat Jumping's fourth slot takes it in place of Kismet:
every survival score column is unchanged, knockback protection is 4 points, and every attack, Surveillance and Wide Area Web Grenade stay
at 95% against +4 (lowest before the cap 99.1%). The piece is not a unique by the data; crafted at 10 it works from exemplar 7.

## What the totals count

- **Counted as always on:** every toggle and auto power, Health, Stamina, and **Hasten**, a click power whose +70% recharge Mids counts at
  all times. It recharges in 125.6 s for a 120 s buff, about 96% uptime (arithmetic). The chains take it as always on, as Mids does.
- **Not counted:** Build Up, Barrier Core Epiphany and Assault Radial Embodiment (set to off in the file), and the damage procs.
- **No proc is miscounted.** Gaussian's Chance for Build Up sits in Build Up, which is off in the totals.
- **The harness exit.** The Mids dump harness ends with E21 and E14 only because its built-in exemplar build is not in a one-build folder.

## Targets and achieved numbers

**Resistance cap: 85%** (Mids fixture `tests/fixtures/mids/archetypes.json`, `"ResCap": 0.85` for `Class_Arachnos_Soldier`).

| Target | Goal | Previously delivered | **Final** | Met |
| --- | --- | --- | --- | --- |
| Melee / ranged / AoE defense | ≥ 45% with margin | 59.30 / 61.17 / 61.17% | 60.94 / 62.81 / 62.81% | yes |
| Resistance S/L, F/C, E/N, Tox, Psi | as high as allowed | 51.26, 35.98, 23.23, 27.73, 32.73% | 50.85, 35.41, 22.66, 27.16, 32.16% | noted |
| Hit points | as high as allowed | 1,542.2 | 1,542.2 (64% of the 2,409.5 cap) | noted |
| Survivability procs | in Health | absorb, Panacea, Regenerative Tissue | the same | yes |
| Knockback protection | report | 0 | **4 points** | noted |
| Hit chance vs +4, +25% defense | 95% on every attack | 95% | **95% on every attack** | yes |
| Endurance, toggles only | positive | recovery 3.17, toggles 1.36 end/s | recovery 3.22, toggles 1.32 end/s: net +1.90 | yes |
| Bar, group chain | past 108 s | 90.1 s | **101.8 s**; about 278 s with the procs (estimate) | **no** as measured |
| Bar, single-target chain | past 146 s | 103.8 s | **119.2 s**; about 466 s with the procs (estimate) | **no** as measured |

### Hit chance

min(95%, accuracy multiplier × (75% + 25.86% − 25%) × 0.65), the check the other 4-star write-ups use. The multiplier is Mids' enhanced
`Accuracy` divided by (0.75 + to-hit). The 0.65 level factor for a +4 foe is approximate.

| Key | Power | Accuracy multiplier | Hit chance (before the cap) | Recharge (s) | End cost |
| --- | --- | --- | --- | --- | --- |
| 1 | Single Shot | 2.566 | 95% (126.5%) | 1.12 | 2.64 |
| 2 | Burst | 2.295 | 95% (113.2%) | 2.21 | 4.32 |
| 3 | Poisonous Ray | 2.267 | 95% (111.8%) | 2.52 | 4.95 |
| 4 | Heavy Burst | 2.159 | 95% (106.5%) | 3.36 | 6.28 |
| 5 | Venom Grenade | 2.444 | 95% (120.5%) | 7.16 | 5.25 |
| 6 | Frag Grenade | 2.295 | 95% (113.2%) | 4.42 | 7.70 |
| ALT+1 | Surveillance | 2.010 | 95% (99.1%) | 14.26 | 6.13 |
| ALT+2 | Wide Area Web Grenade | 2.010 | 95% (99.1%) | 7.62 | 10.58 |
| — | Boxing / Brawl | 2.010 / 1.904 | 95% (99.1%) / 93.9% | 0.95 / 0.76 | 2.68 / 0 |

## Survival score

The simplified method of `docs/survivability-planning.md`, run by the same script as the Crab revision:

- **Chance to be hit, per position:** min(95%, bounded(enemy base chance to hit − defense, 5% to 95%) × 2.0). Team at +3/+4: base 50%.
  Four-star: base 80%. Positions weighted melee 10%, ranged 45%, AoE 45% (20/40/40 also run; it moves no column by more than 0.1).
- **Defense down:** a tenth of attacks land with defense taken as zero.
- **Damage let through:** 1 − resistance, by the simple mix (half smashing and lethal, a quarter energy and negative, the rest split
  between fire, cold, toxic and psionic) and the logs mix (lethal 22, toxic 22, negative 17, energy 12, smashing 11, fire 6.5, psionic 6,
  cold 0.6, unresistable 3).
- **Index** = hit points ÷ (chance to be hit × damage let through), with the previously delivered build = 100 (no Serum on this build).

| Build | Team, simple | Team, logs | 4-star, simple | 4-star, logs | Bars single / group | With procs | Dmg/s single / group | KB prot |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Previously delivered | 100.0 | 100.0 | 100.0 | 100.0 | 103.8 / 90.1 | 294 / 206 | 217.1 / 1,026.8 | 0 |
| **This file** | **99.2** | **99.2** | **106.4** | **106.4** | 119.2 / 101.8 | 466 / 278 | 219.4 / 1,028.3 | 4 |

**Hand check** on this file, four-star, simple mix, 10/45/45:

- melee min(95, (80 − 60.94) × 2) = 38.12%; ranged (80 − 62.81) × 2 = 34.38%; AoE the same, 34.38%;
- blended 0.10 × 38.12 + 0.45 × 34.38 + 0.45 × 34.38 = 34.75%; with the allowance 0.9 × 34.75 + 0.1 × 95 = 40.78%;
- let through 0.25 × (1 − 0.5085) × 2 + 0.125 × (1 − 0.2266) × 2 + 0.0625 × [(1 − 0.3541) × 2 + (1 − 0.2716) + (1 − 0.3216)] = 60.78%;
- index 1,542.2 / (0.4078 × 0.6078) = 6,223; the previously delivered build's is 5,850; 6,223 / 5,850 = 106.4, as the table shows.

**Team content** falls 0.8: defense there is already past the point where the enemy sits at the 5% floor, so only Steadfast's lower
resistance shows. **Four-star content** gains 6.4 from the defense boosts.

### Boosts that free a slot (screening)

Arithmetic through the engine's Diversification function on the unboosted harness slot values; each power with one set piece fewer and
the rest at +5. Every freed slot costs a set bonus; the best use found for one (Stamina's third piece) lowers every survival column
(alternative row above), so no slot moves.

| Power | Unboosted, after ED | One fewer, rest +5 | Set bonus lost |
| --- | --- | --- | --- |
| Tactical Training: Maneuvers (5 → 4) | Defense 59.92% | 60.75% | +3.75% S/L resistance (five-piece) |
| Tactical Training: Leadership (6 → 5) | To-hit 57.52% | 57.76% | +3.75% S/L resistance (six-piece) |
| Tough (5 Unbreakable Guard → 4) | Resistance 57.52% | 57.76% | +3.75% Tox/Psi resistance (five-piece) |
| Build Up (6 → 5) | To-hit 57.52% | 57.76% | +2.5% defense in every position (six-piece) |
| Three-piece Luck of the Gambler toggles (3 → 2) | Defense 44.21% | 39.84% | +1.13% HP (three-piece); value not held |

## Exemplar

The exemplar script starts from the engine's level 50 recompute (`compute_base_totals`) on this build's harness dumps and applies the
wiki's rules for exemplar level N: powers usable while their pick level ≤ N + 5 (every slot in a usable power works); set bonuses and
globals piece by piece with the rule of five (purple, PvP, archetype and Winter pieces always; attuned pieces from the set's minimum level
− 3; crafted pieces from their own level − 3; procs whenever their power is usable); each enhancement value scaled by the wiki's table;
base hit points and magnitudes from the level N tables; incarnates from 45.

**Parity at 50.** The script matches the harness on every defense, resistance and mez field, and on regeneration, recovery, endurance,
recharge, to-hit, damage and accuracy. Hit points differ by 0.10 (1,542.09 against 1,542.19).

**Hand check.** Worked from the data, not from the script: the five level 50 Luck of the Gambler globals (7.5% each) and Adjusted
Targeting's five-piece bonus (+5% recharge, all its pieces crafted at 50) switch on at 50 − 3 = 47, a step of 42.5%. The script moves
recharge by exactly 42.5 at 47 and by nothing at 42 to 46 or 48 to 49, under both floor modes (Hasten left out).

Recharge is Mids' figure, with Hasten counted as always on.

| Build, level | Melee / Ranged / AoE defense | S/L, F/C, E/N, Tox, Psi resistance | HP | Regen | Recharge | `EndUse` | KB protection |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Previously delivered, 45 | 53.67 / 58.67 / 58.67 | 40.76, 32.98, 14.98, 20.98, 20.98 | 1,346 | 1.17 | +120% | 1.41 | 0 |
| Previously delivered, 35 | 53.67 / 58.67 / 58.67 | 37.11, 31.48, 13.48, 19.48, 19.48 | 1,112 | 1.17 | +120% | 1.29 | 0 |
| Previously delivered, 25 | 27.49 / 32.49 / 32.49 | 36.95, 31.48, 13.48, 19.48, 19.48 | 757 | 1.17 | +120% | 0.73 | 0 |
| **Final, 50** | 60.94 / 62.81 / 62.81 | 50.85, 35.41, 22.66, 27.16, 32.16 | 1,542 | 1.67 | +162.5% | 1.32 | 4 |
| **Final, 45** | 55.31 / 60.31 / 60.31 | 43.35, 35.41, 17.41, 23.41, 28.41 | 1,425 | 1.17 | +120% | 1.37 | 4 |
| **Final, 35** | 55.31 / 60.31 / 60.31 | 39.70, 33.91, 15.91, 21.91, 26.91 | 1,177 | 1.17 | +120% | 1.21 | 4 |
| **Final, 25** | 30.72 / 35.72 / 35.72 | 39.55, 33.91, 15.91, 21.91, 26.91 | 801 | 1.17 | +120% | 0.71 | 4 |

- **Below 47** the crafted level 50 set pieces (Luck of the Gambler globals included) lose their set bonuses and globals; Superior Frozen
  Blast, Superior Dominion, the Winter and archetype sets, Ragnarok and Apocalypse keep theirs at every level.
- **The final build** keeps +3% defense from Steadfast Protection down to 7, Unbreakable Guard's +Max HP and the absorb down to 17, and
  Aegis down to 22. With the Luck of the Gambler globals at 25 it would also keep +157.5% recharge from 22 (alternative row above).
- **Defense at 25 is low** (30.7 / 35.7 / 35.7): Weave, Cloaking Device and Maneuvers are not usable there (picks 32 to 38).

### Unique placement

The lowest exemplar level at which each piece's own bonus works, by the rules above ("unique" is the data's one-per-character
`slot_requires`):

| Piece | Power | As slotted | Unique | Works from exemplar | In game |
| --- | --- | --- | --- | --- | --- |
| Steadfast Protection: Resistance/+Def 3% | Bane Spider Armor Upgrade | crafted 10, +5 | yes | 7 | crafted 30 (works from 27) |
| Gladiator's Armor: TP Protection +3% Def (All) | Bane Spider Armor Upgrade | crafted 50 | yes | every level (PvP) | crafted 50 |
| Shield Wall: +Res (Teleportation), +5% Res (All) | Combat Training: Defensive | crafted 50 | yes | every level (PvP) | — |
| Luck of the Gambler: Defense/Increased Global Recharge Speed | five toggles | crafted 50 | **no** | 47 | crafted 50 in Combat Jumping |
| Blessing of the Zephyr: Knockback Reduction (4 points) | Combat Jumping | crafted 10 | no | 7 | Kismet, crafted 30 |
| Unbreakable Guard: +Max HP | Tough | crafted 20 | yes | 17 | — |
| Aegis: Psionic/Status Resistance | Tough | crafted 25 | yes | 22 | — |
| Reactive Defenses: Scaling Resist Damage | Cloaking Device | crafted 20 | yes | 17 | — |
| Preventive Medicine: Chance for +Absorb | Health | crafted 20 | yes | 17 | crafted 20 |
| Numina's Convalescence: +Regeneration/+Recovery | Health | crafted 50 | yes (proc) | every level | crafted 50 |
| Miracle: +Recovery | Health | crafted 40 | yes (proc) | every level | crafted 40 |
| Panacea: +Hit Points/Endurance | Health | attuned | yes (proc) | every level | attuned |
| Regenerative Tissue: +Regeneration | Health | crafted 10 | yes (proc) | every level | crafted 10 |
| Performance Shifter: Chance for +End | Stamina | crafted 50 | **no** (proc) | every level | crafted 50 |
| Achilles' Heel: Chance for Res Debuff | Surveillance | crafted 20 | no (proc) | 19 (Surveillance usable) | crafted 20 |
| Annihilation: Chance for Res Debuff | Heavy Burst | crafted 50 | no (proc) | 3 (Heavy Burst usable) | — |

"—" means the screenshots did not show that piece's level. **Against the game, measured:** Steadfast Protection at 10 (+5) in place of 30
costs 0.41 points of S/L and 0.57 of the other resistances at 50 and gains +3% defense in every position from 7 to 26.

**What the Health and Stamina procs give.** From `data/canonical/powers/boosts/` and `set_bonus/set_bonus/preventive_medicine_proc.json`:

- **Preventive Medicine absorb.** It checks every 5 s: a 10% chance at 31 to 75% health, 100% below 31%. Each trigger grants absorb equal
  to 20% of max HP for 30 s, **at most once every 120 s**: about 308 absorb at 1,542 HP, at most 308 / 120 = 2.6 HP/s (arithmetic).
- **Panacea.** 3 PPM in an auto power, checked every 10 s: a 50% chance per check, +7.5% endurance: 0.5 × 7.77 / 10 = **0.39 end/s**.
- **Performance Shifter: Chance for +End.** 1.5 PPM, a 25% chance per 10 s check, +10% endurance: 0.25 × 10.36 / 10 = **0.26 end/s**.
- **None of these procs is modelled by Mids.** "Endurance against real encounter lengths" shows the bars with and without them.

## Enhancement Boosters

**The rule.** A boost raises the piece's own enhancement values by 5% each (25% at +5). It does not raise global bonuses, set bonuses or
procs, and attuned pieces cannot be boosted. Sources: the Homecoming wiki's Enhancement Booster Salvage page, the data's `boostable` flag,
and the harness measurement below. Mids applies it as the piece's value × 1.25 at +5 (`docs/engine/mids-port-spec.md`, step 1).

**Which pieces.** A piece is boosted when at least half of its boost survives Diversification in the aspects that change a figure of this
build (recharge on toggles, and accuracy on attacks already past 95%, are left out of that share). **Luck of the Gambler pieces** are
boosted only where their defense value survives: in Combat Jumping, Weave, Cloaking Device and Maneuvers it keeps 79% (+8.88 of +11.17
points of defense enhancement); in Tactical Training: Maneuvers it keeps 15%, so none of its five pieces is boosted. Hasten's two IOs are
the operator's rule. Brawl's IO is plain.

**What is boosted** (28 set pieces and 8 common IOs; 36 pieces × 5 = **180 boosters**). Values are each piece's own enhancement before
ED, from the harness slots of this build without and with every boost. Every ratio is exactly 1.2500.

| Power | Pieces (level) | Enhancement value, unboosted → +5 | Kept after ED, by aspect |
| --- | --- | --- | --- |
| Bane Spider Armor Upgrade | Steadfast Protection: Resistance/+Def 3% (10) | Resistance 4.37 → 5.47% | Resistance 100% |
| Combat Jumping, Weave, Cloaking Device, Maneuvers | Luck of the Gambler: Def/+Recharge (50) | Defense 15.94 → 19.92% | Defense 79% (the three together) |
| the same four | Luck of the Gambler: Defense/Endurance (50) | Defense 15.94 → 19.92%; End 26.50 → 33.12% | Defense 79%, End 100% |
| the same four | Luck of the Gambler: Def/End/Recharge (50) | Defense 12.80 → 16.00%; End, Recharge 21.20 → 26.50% | Defense 79%, End 100% |
| Tough | Unbreakable Guard: Res/End and Res/End/Recharge (50) | Res 15.94 → 19.92%, 12.75 → 15.94%; End × 1.25 | End 100%, Resistance 15% |
| Tactical Training: Leadership | Adjusted Targeting: ToHit/End/Rech, End/Rech, ToHit/End (50) | each value × 1.25 | End 87%, to-hit 15% |
| Surveillance | Analyze Weakness: Accuracy/Endurance/Recharge (50) | each 21.20 → 26.50% | End 100% |
| Heavy Burst, Poisonous Ray | Ragnarok and Apocalypse: Recharge/Accuracy, Damage/Endurance (50) | 33.12 → 41.41% each | End 100%, Recharge 63%, Damage 15% |
| Build Up | Gaussian's: ToHit/Rech, ToHit/Rech/End, Rech/End, ToHit/End (50) | each value × 1.25 | End 87%, Recharge 87%, to-hit 15% |
| Stamina | Performance Shifter: EndMod (50) | EndMod 42.40 → 53.00% | 100% |
| Boxing, Wide Area Web Grenade | Accuracy IO (50) | 42.40 → 53.00% | 100% |
| Tactical Training: Assault, Assault, Tactics | Endurance Reduction IO (50) | 42.40 → 53.00% | 100% |
| Mental Training | Run Speed IO (50) | 42.40 → 53.00% | 100% |
| Hasten | two Recharge Reduction IOs (50) | 42.40 → 53.00% each | the operator's rule |

**What the boosts move, from the harness** (this build without any boost against this file): the set-bonus virtual power is
byte-identical; `HPMax`, `HPRegen`, `BuffHaste`, `BuffAcc`, `BuffDam`, mez protection and mez resistance are identical; defense +1.64 in
every position; resistance +0.24 S/L and +0.08 other types (Steadfast Protection and Tough); `EndRec` 0.751 → 0.778; `EndUse`
1.389 → 1.321; standing to-hit +25.75 → +25.86%; Hasten 130.1 → 125.6 s, Build Up 26.8 → 25.5 s.

**Not boosted, by the rule:** the five Luck of the Gambler pieces in Tactical Training: Maneuvers (defense 15% kept); Unbreakable Guard:
Resistance and Recharge/Resistance (15%); Adjusted Targeting: To Hit Buff, To Hit Buff/Recharge and Recharge (0 to 15%); Analyze
Weakness: Accuracy/Recharge (no figure moves); Ragnarok and Apocalypse Damage/Recharge/Accuracy (43%), Damage/Recharge (39%), Damage
(15%); Gaussian's: To Hit Buff (15%). **Passing and left unboosted:** the travel IOs in Swift, Sprint and Hurdle, Rest's IO and Brawl's
IO (no measured figure moves; 25 boosters).

## Enhancement Diversification audit

Every power of this build, measured with `coh_engine.enh_pipeline.aggregate_and_ed` on its harness slots. These aspects pass the last
threshold (100% on schedule A, 60% on schedule B):

| Power | Aspect | Pre | Post | What holds the slots |
| --- | --- | --- | --- | --- |
| Single Shot | Accuracy / Damage / Recharge | 109.3 / 142.4 / 106.0% | 96.4 / 101.4 / 95.9% | Superior Winter's Bite (Winter set) |
| Burst | Damage / Recharge | 142.4 / 132.5% | 101.4 / 99.9% | Superior Spider's Bite (archetype set) |
| Heavy Burst | Damage / Recharge | 154.0 / 101.0% | 103.1 / 95.2% | Ragnarok (purple, five pieces and the Annihilation proc) |
| Tactical Training: Maneuvers | Defense | 86.1% | 59.9% | Luck of the Gambler: +9% accuracy (4), +3.75% S/L resistance (5) |
| Venom Grenade | Accuracy / Damage | 109.3 / 142.4% | 96.4 / 101.4% | Superior Frozen Blast (Winter set) |
| Hasten | Recharge | 106.0% | 95.9% | Two +5 Recharge IOs, the operator's rule for Hasten |
| Frag Grenade | Damage / Recharge | 142.4 / 132.5% | 101.4 / 99.9% | Superior Dominion of Arachnos (archetype set) |
| Tactical Training: Leadership | Recharge / To-hit | 128.5 / 77.3% | 99.3 / 58.6% | Adjusted Targeting: +3.75% S/L resistance (6) |
| Poisonous Ray | Damage / Recharge | 154.0 / 101.0% | 103.1 / 95.2% | Apocalypse (purple, five slots) |
| Tough | Resistance | 77.3% | 58.6% | Unbreakable Guard: +3.13% melee defense (4), +3.75% Tox/Psi resistance (5) |
| Build Up | To-hit | 81.3% | 59.2% | Gaussian's: +2.5% defense in every position (6) |

Every other aspect of every power is under its last threshold, including the three-piece Luck of the Gambler toggles (defense 55.8% →
53.1%) and the single IOs at 42.4 to 53.0%.

## Recommended attack chains

From the chain planner on this file's dump (`coh_engine chain <dump> --stance ranged`). The planner applies Build Up at its uptime (×1.123)
and Surveillance at 100% uptime. It fires every power the instant it recharges, so its endurance figures are worst cases.

- **Single hard target:** 3 Poisonous Ray → 2 Burst → 1 Single Shot. Cycle 4.36 s, gap 0.27 s. **219.4 dmg/s.** Chain 2.30 end/s,
  toggles 1.32, other clicks 0.47, recovery 3.22: net −0.87 end/s, the bar lasts **119.2 s**.
- **Groups (10 targets):** 5 Venom Grenade → 6 Frag Grenade → 4 Heavy Burst → 2 Burst → 1 Single Shot. Cycle 9.01 s, gap 0.43 s.
  **1,028.3 dmg/s** with Venom Grenade and Surveillance stacked at −40%. Net −1.02 end/s; the bar lasts **101.8 s**.
- **Openers:** Surveillance (ALT+1), Venom Grenade, Build Up (CTRL+1), then the chain. Judgement on the largest pull; Wide Area Web
  Grenade first on a spawn that will scatter.

## Endurance against real encounter lengths

The operator's combat logs cover 2,972 non-farming engagements over 40 hours: typical fight 30 s at the median and 108 s at the 90th
percentile; long-lived target 103 s at the median, 146 s at the 75th and 210 s at the 90th; pause between fights 31 s at the median.

Time to empty with the procs = 103.6 / (measured drain − 0.65 end/s) (estimate):

| Chain | As measured | With the Panacea and Performance Shifter procs (estimate) |
| --- | --- | --- |
| Single target | 119.2 s | about 466 s |
| Group | 101.8 s | about 278 s |

- **Group chain:** 6.2 s short of the 108 s 90th percentile of typical fights, as measured; far past it with the procs.
- **Single-target chain:** past the 103 s median of long fights and 26.8 s short of the 146 s 75th percentile, as measured.
- **Refill between fights** (arithmetic): 3.22 − 1.32 = 1.90 end/s, about 2.55 with the procs. The 31 s median pause returns 59 (57%).

These are the planner's worst case: the logs show a player uses each power two to three times less often than the planner. The logs come
from the operator's other characters and from all content.

## Team value

| Source | Effect | Uptime |
| --- | --- | --- |
| Tactical Training: Maneuvers | +16.0% defense, every position and type | toggle, always |
| Maneuvers (Leadership pool) | +5.2% defense | toggle, always |
| Tactical Training: Assault, Assault | +15% damage each | toggle, always |
| Tactical Training: Leadership / Tactics | +15.86% / +10% to-hit, perception | toggles, always |
| Surveillance | −20% resistance (all types), −15% defense, 20 s, one target | 100%: 14.3 s recharge under its 20 s duration |
| Venom Grenade | −20% resistance (−40% toxic), 16 s, up to 10 targets | 100% in the group chain |
| Procs | Annihilation in Heavy Burst (−12.5% resistance), Achilles' Heel in Surveillance, Reactive Radial Interface | chance-based |
| Control | Wide Area Web Grenade (immobilize magnitude 3, 16 foes); Venom Grenade's immobilize proc | on demand |

Not covered: heals, absorb or resistance buffs for others, regeneration debuffs, recharge or endurance buffs, holding aggro.

## Survival against the Crab

The weak spot of this style is resistance: no Fortification, no Serum. Against the Crab's revised build (its write-up): S/L resistance
50.85% against 72.32%; HP 1,542 against 1,973 with Serum. The cover is defense well past the soft cap (61 to 63% in every position),
range, Health's survivability pieces, Barrier, Cloaking Device's stealth before the pull, and the team.

## Pick order

Unchanged. Later than they open: Combat Training: Defensive (opens 2) at 4; Combat Jumping (4) at 6; Hasten (4) at 14; Poisonous Ray (12)
at 26; Boxing (4) at 28; Tough and Weave (14) at 30 and 32; Cloaking Device (24) at 35; Maneuvers (4) at 38, Assault (4) at 47, Tactics (14)
at 49; Build Up (6) at 41 and Wide Area Web Grenade (6) at 44.

## Powers by level, with slotting

Each piece is named with its kind and level: **superior attuned** (Superior Winter's Bite, Superior Spider's Bite, Superior Frozen Blast,
Superior Dominion of Arachnos; no level), **attuned** (Panacea), **crafted** at the level shown. "Invention" pieces are common IOs.
`(A)` is the free slot; numbers in brackets are slot levels. "+5" marks a boost.

- **1 Single Shot (key 1):** Superior Winter's Bite, superior attuned: Accuracy/Damage (A), Accuracy/Damage/Recharge (3),
  Accuracy/Damage/Endurance (5), Damage/RechargeTime (29), Damage/Endurance/Accuracy/RechargeTime (40), Recharge/Chance for -Speed &
  -Recharge (46).
- **1 Bane Spider Armor Upgrade:** Steadfast Protection: Resistance/+Def 3%, crafted 10, +5 (A); Gladiator's Armor: TP Protection +3% Def
  (All), crafted 50 (3).
- **2 Burst (key 2):** Superior Spider's Bite, superior attuned: Accuracy/Damage (A), Accuracy/Damage/RechargeTime (5), Damage/RechargeTime
  (7), Damage/Endurance/RechargeTime (31), Accuracy/Damage/Endurance/RechargeTime (40), RechargeTime/Global Toxic (48).
- **4 Combat Training: Defensive:** Shield Wall: +Res (Teleportation), +5% Res (All), crafted 50 (A).
- **6 Combat Jumping:** Luck of the Gambler, crafted 50: Defense/Increased Global Recharge Speed +5 (A), Defense/Endurance +5 (7),
  Defense/Endurance/Recharge +5 (9); Blessing of the Zephyr: Knockback Reduction (4 points), crafted 10 (21).
- **8 Heavy Burst (key 4):** Ragnarok, crafted 50: Damage/Recharge/Accuracy (A), Recharge/Accuracy +5 (9), Damage/Recharge (19),
  Damage/Endurance +5 (33), Damage (43); Annihilation: Chance for Res Debuff, crafted 50 (48).
- **10 Tactical Training: Maneuvers:** Luck of the Gambler, crafted 50: Defense/Increased Global Recharge Speed (A), Defense (11),
  Defense/Endurance (11), Defense/Endurance/Recharge (37), Defense/Recharge (46).
- **12 Venom Grenade (key 5):** Superior Frozen Blast, superior attuned: Accuracy/Damage (A), Accuracy/Damage/Recharge (13),
  Accuracy/Damage/Endurance (23), Damage/Endurance/Accuracy/RechargeTime (34), Damage/Endurance (43), Recharge/Chance for Immobilize (48).
- **14 Hasten (auto-fire):** Invention: Recharge Reduction, crafted 50, +5 (A), (15).
- **16 Tactical Training: Assault:** Invention: Endurance Reduction, crafted 50, +5 (A).
- **18 Frag Grenade (key 6):** Superior Dominion of Arachnos, superior attuned: Accuracy/Damage (A), Accuracy/Damage/Recharge (19),
  Damage/Recharge (25), Damage/Endurance/Recharge (34), Accuracy/Damage/Endurance/Recharge (43), Recharge/Chance for -Dmg and Terrorize (50).
- **20 Tactical Training: Leadership:** Adjusted Targeting, crafted 50: To Hit Buff (A), To Hit Buff/Recharge (21), To Hit
  Buff/Endurance/Recharge +5 (27), Endurance/Recharge +5 (37), To Hit Buff/Endurance +5 (45), Recharge (50).
- **22 Mental Training:** Invention: Run Speed, crafted 50, +5 (A).
- **24 Surveillance (ALT+1):** Analyze Weakness, crafted 50: Accuracy/Recharge (A), Accuracy/Endurance/Recharge +5 (25); Achilles' Heel:
  Chance for Res Debuff, crafted 20 (37).
- **26 Poisonous Ray (key 3):** Apocalypse, crafted 50: Damage/Recharge/Accuracy (A), Recharge/Accuracy +5 (27), Damage/Recharge (29),
  Damage/Endurance +5 (39), Damage (46).
- **28 Boxing (prerequisite, not on a key):** Invention: Accuracy, crafted 50, +5 (A).
- **30 Tough:** Unbreakable Guard: Resistance, crafted 50 (A); Unbreakable Guard: +Max HP, crafted 20 (31); Aegis: Psionic/Status
  Resistance, crafted 25 (31); Unbreakable Guard, crafted 50: Resistance/Endurance +5 (33), Resistance/Endurance/RechargeTime +5 (34),
  RechargeTime/Resistance (36).
- **32 Weave:** Luck of the Gambler, crafted 50: Defense/Increased Global Recharge Speed +5 (A), Defense/Endurance +5 (33),
  Defense/Endurance/Recharge +5 (39).
- **35 Cloaking Device:** Reactive Defenses: Scaling Resist Damage, crafted 20 (A); Luck of the Gambler, crafted 50: Defense/Increased
  Global Recharge Speed +5 (36), Defense/Endurance +5 (36), Defense/Endurance/Recharge +5 (45).
- **38 Maneuvers:** Luck of the Gambler, crafted 50: Defense/Increased Global Recharge Speed +5 (A), Defense/Endurance +5 (39),
  Defense/Endurance/Recharge +5 (40).
- **41 Build Up (CTRL+1):** Gaussian's Synchronized Fire-Control, crafted 50: To Hit Buff (A), To Hit Buff/Recharge +5 (42), To Hit
  Buff/Recharge/Endurance +5 (42), Recharge/Endurance +5 (42), To Hit Buff/Endurance +5 (45), Chance for Build Up (50). Off in the totals.
- **44 Wide Area Web Grenade (ALT+2):** Invention: Accuracy, crafted 50, +5 (A).
- **47 Assault:** Invention: Endurance Reduction, crafted 50, +5 (A).
- **49 Tactics:** Invention: Endurance Reduction, crafted 50, +5 (A).
- **Health:** Numina's Convalescence: +Regeneration/+Recovery, crafted 50 (A); Miracle: +Recovery, crafted 40 (13); Panacea: +Hit
  Points/Endurance, attuned (15); Preventive Medicine: Chance for +Absorb, crafted 20 (17); Regenerative Tissue: +Regeneration, crafted 10 (23).
- **Stamina:** Performance Shifter: Chance for +End, crafted 50 (A); Performance Shifter: EndMod, crafted 50, +5 (17).
- **Brawl:** Invention: Accuracy, crafted 50 (A). **Sprint:** Invention: Run Speed, crafted 50 (A). **Rest:** Invention: Endurance
  Modification, crafted 50 (A). **Swift:** Invention: Run Speed, crafted 50 (A). **Hurdle:** Invention: Jumping, crafted 50 (A).

### Slot accounting

67 added slots, 24 picks, 4 pools. Every power's slot levels are identical to the previously delivered file, checked slot by slot.
Every slot of every power and inherent is filled: 0 empty (the previously delivered file had 5 empty granted slots).

### Verbatim MidsReborn export (power lines)

The export prints neither the +5 boosts nor the piece levels; the `.mbd` carries both.

```text
Level 1:	Single Shot			SprWntBit-Acc/Dmg(A), SprWntBit-Acc/Dmg/Rchg(3), SprWntBit-Acc/Dmg/EndRdx(5), SprWntBit-Dmg/Rchg(29), SprWntBit-Dmg/EndRdx/Acc/Rchg(40), SprWntBit-Rchg/SlowProc(46)
Level 1:	Bane Spider Armor Upgrade		StdPrt-ResDam/Def+(A), GldArm-3defTpProc(3)
Level 2:	Burst				SprSpdBit-Acc/Dmg(A), SprSpdBit-Acc/Dmg/Rchg(5), SprSpdBit-Dmg/Rchg(7), SprSpdBit-Dmg/EndRdx/Rchg(31), SprSpdBit-Acc/Dmg/EndRdx/Rchg(40), SprSpdBit-Rchg/Global Toxic(48)
Level 4:	Combat Training: Defensive		ShlWal-ResDam/Re TP(A)
Level 6:	Combat Jumping			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(7), LucoftheG-Def/EndRdx/Rchg(9), BlsoftheZ-ResKB(21)
Level 8:	Heavy Burst			Rgn-Acc/Dmg/Rchg(A), Rgn-Acc/Rchg(9), Rgn-Dmg/Rchg(19), Rgn-Dmg/EndRdx(33), Rgn-Dmg(43), Ann-ResDeb%(48)
Level 10:	Tactical Training: Maneuvers		LucoftheG-Def/Rchg+(A), LucoftheG-Def(11), LucoftheG-Def/EndRdx(11), LucoftheG-Def/EndRdx/Rchg(37), LucoftheG-Def/Rchg(46)
Level 12:	Venom Grenade			SprFrzBls-Acc/Dmg(A), SprFrzBls-Acc/Dmg/Rchg(13), SprFrzBls-Acc/Dmg/EndRdx(23), SprFrzBls-Dmg/EndRdx/Acc/Rchg(34), SprFrzBls-Dmg/EndRdx(43), SprFrzBls-Rchg/ImmobProc(48)
Level 14:	Hasten				RechRdx-I(A), RechRdx-I(15)
Level 16:	Tactical Training: Assault		EndRdx-I(A)
Level 18:	Frag Grenade			SprDmnofA-Acc/Dmg(A), SprDmnofA-Acc/Dmg/Rchg(19), SprDmnofA-Dmg/Rchg(25), SprDmnofA-Dmg/EndRdx/Rchg(34), SprDmnofA-Acc/Dmg/EndRdx/Rchg(43), SprDmnofA-Rchg/DmgFear%(50)
Level 20:	Tactical Training: Leadership		AdjTrg-ToHit(A), AdjTrg-ToHit/Rchg(21), AdjTrg-ToHit/EndRdx/Rchg(27), AdjTrg-EndRdx/Rchg(37), AdjTrg-ToHit/EndRdx(45), AdjTrg-Rchg(50)
Level 22:	Mental Training			Run-I(A)
Level 24:	Surveillance			AnlWkn-Acc/Rchg(A), AnlWkn-Acc/Rchg/EndRdx(25), AchHee-ResDeb%(37)
Level 26:	Poisonous Ray			Apc-Acc/Dmg/Rchg(A), Apc-Acc/Rchg(27), Apc-Dmg/Rchg(29), Apc-Dmg/EndRdx(39), Apc-Dmg(46)
Level 28:	Boxing				Acc-I(A)
Level 30:	Tough				UnbGrd-ResDam(A), UnbGrd-Max HP%(31), Ags-Psi/Status(31), UnbGrd-ResDam/EndRdx(33), UnbGrd-ResDam/EndRdx/Rchg(34), UnbGrd-Rchg/ResDam(36)
Level 32:	Weave				LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(33), LucoftheG-Def/EndRdx/Rchg(39)
Level 35:	Cloaking Device			Rct-ResDam%(A), LucoftheG-Def/Rchg+(36), LucoftheG-Def/EndRdx(36), LucoftheG-Def/EndRdx/Rchg(45)
Level 38:	Maneuvers			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(39), LucoftheG-Def/EndRdx/Rchg(40)
Level 41:	Build Up			GssSynFr--ToHit(A), GssSynFr--ToHit/Rchg(42), GssSynFr--ToHit/Rchg/EndRdx(42), GssSynFr--Rchg/EndRdx(42), GssSynFr--ToHit/EndRdx(45), GssSynFr--Build%(50)
Level 44:	Wide Area Web Grenade		Acc-I(A)
Level 47:	Assault				EndRdx-I(A)
Level 49:	Tactics				EndRdx-I(A)
Level 1:	Brawl				Acc-I(A)
Level 1:	Sprint				Run-I(A)
Level 2:	Rest				EndMod-I(A)
Level 1:	Swift				Run-I(A)
Level 1:	Hurdle				Jump-I(A)
Level 1:	Health				NmnCnv-Regen/Rcvry+(A), Mrc-Rcvry+(13), Pnc-Heal/+End(15), Prv-Absorb%(17), RgnTss-Regen+(23)
Level 1:	Stamina				PrfShf-End%(A), PrfShf-EndMod(17)
```

## Incarnates (all tier 4; Lore left empty, as 4-star bans pets)

- **Alpha: Cardiac Core Paragon.** On the previously delivered slotting Agility Core gave 5 to 6.5 points more defense, cost 3.65 points of
  S/L resistance and cut both bars to under half. Agility was not re-run on this layout.
- **Judgement: Ion Core Final Judgement** (ALT+3). **Interface: Reactive Radial Flawless Interface**, whose −resistance stacks with
  Venom Grenade and Surveillance.
- **Destiny: Barrier Core Epiphany** (CTRL+3). Off in the totals. Six of eight on a team take Barrier, two at once every 30 seconds; this
  build is one of the six, and for this Bane it is the main cover for the resistance gap.
- **Hybrid: Assault Radial Embodiment** (CTRL+2). The petless pick in the VEAT handbook. Off in the totals.

## Pets and the epic pool: left out

Summon Spiderlings and Call Reinforcements are not taken, and Lore pets are banned in 4-star play. No epic pool: Spirit Shark lost to
Poisonous Ray on single-target damage (an earlier measurement). No mace beams, as the style requires.

## 4-star play

- Stay at range. On an arch-villain: Surveillance, Venom Grenade, Build Up, then Poisonous Ray → Burst → Single Shot. Recast Surveillance
  whenever it is up and Venom Grenade within 16 s.
- On groups: Wide Area Web Grenade if the spawn will scatter, then Venom Grenade → Frag Grenade → Heavy Burst, Burst and Single Shot between.
- Keep all six team toggles on. Hasten fires itself on auto.
- **Below 31% health** the Preventive Medicine absorb fires: about 308 points, at most once every 120 s.
- Stagger Barrier with the team's other Barrier holders, and hit it early: this build has no self-heal.

## What I would verify

- **The 0.65 level factor** in the hit-chance check is approximate; the margin is 4 points on Surveillance and Wide Area Web Grenade.
- **Cloaking Device's defense while attacking:** Mids counts it at all times.
- **The boost value on the level 10 piece** (Mids' 1.25 times the piece's own value).
- **The absorb, Panacea and Performance Shifter procs:** none is modelled by Mids.
- **The survival score's planning figures** and **exemplar totals**, computed by the wiki's rules piece by piece, not measured in game.

## Guides used (paraphrased, short quotes only)

Under `homecoming-source-material/community/archetypes/`:

- `arachnos/the-spider-s-handbook-an-incomplete-guide-to-veats.md` — "The Spider's Handbook: An Incomplete Guide to VEATs."
- `arachnos/to-poison-ray-or-not-that-is-the-question.md` — "To Poison Ray or not? That is the question."
- `arachnos/extremely-violent-team-support-a-huntsman-build.md` — "Extremely Violent Team Support - A Huntsman Build."
- `arachnos/critique-my-hunstman-build.md` — "Critique my Hunstman build."
- `tanker/inv-rad-hardmode-version-i-hope.md` — "Inv/Rad: Hardmode version (I hope?)."
- The Homecoming wiki's Enhancement Booster Salvage page, for the boost rule.

## Files

Sibling files in this folder, all named "bane-rifle-4-star" plus an
ending: `.mbd`, `.md`, `.mids-totals.json`.
