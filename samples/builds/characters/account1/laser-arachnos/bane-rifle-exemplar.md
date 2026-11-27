# Bane Spider Soldier-Bane Spider Training Arachnos Soldier - Ranged Rifle - Exemplar

## Buy these pieces attuned: the file holds crafted stand-ins

This build is designed around the pieces below being **attuned** in game. The Mids file cannot mark them attuned: an `.mbd` marks a piece
attuned only by its identifier (`Attuned_...`), and the Mids 3.8.6 database the file is written against (2026.5.1337) has attuned
identifiers only for archetype, Winter and attuned-only sets. The file therefore holds each of these 29 pieces as the crafted piece at the
level shown. Buy them attuned, except Regenerative Tissue: it is a proc, and the crafted 30 piece works as well (see the last note below).

| Power | Pieces to buy attuned | In the file |
| --- | --- | --- |
| Combat Jumping | Luck of the Gambler: Defense/Increased Global Recharge Speed, Defense/Endurance | crafted 50 |
| Tactical Training: Maneuvers | Luck of the Gambler: Defense/Increased Global Recharge Speed, Defense/Endurance, Defense/Endurance/Recharge, Endurance/Recharge | crafted 50 |
| Cloaking Device | Luck of the Gambler: the same four | crafted 50 |
| Weave | Luck of the Gambler: the same four | crafted 50 |
| Surveillance | Achilles' Heel: Chance for Res Debuff, Defense Debuff, Defense Debuff/Recharge | crafted 20 |
| Tough | Unbreakable Guard: Resistance, Resistance/Endurance, RechargeTime/Resistance, Resistance/Endurance/RechargeTime, +Max HP | crafted 50 |
| Tough | Aegis: Psionic/Status Resistance | crafted 50 |
| Build Up | Gaussian's Synchronized Fire-Control: To Hit Buff, To Hit Buff/Recharge, To Hit Buff/Recharge/Endurance, Chance for Build Up | crafted 50 |
| Health | Regenerative Tissue: +Regeneration | crafted 30, which works the same (a proc) |
| Health | Preventive Medicine: Chance for +Absorb | crafted 50 |

**Which figures assume attuned.** Every per-exemplar-level table below (totals, hit chance, damage, endurance) was computed with these
pieces attuned. At level 50 an attuned piece and a crafted 50 piece have the same values, so the level 50 row, the Mids totals
(`.mids-totals.json`), the survival score, the boost evidence and the Enhancement Diversification audit describe the file exactly.

**Attuned floors are the game data's.** Each piece's `boost_info.min_bonus_level` in
`data/canonical/powers/boosts/attuned_<piece>/attuned_<piece>.json` holds the lowest exemplar level at which its set bonus and global work, stored as the level minus one.
The evidence for that reading: `min_slot_level` is stored the same way, and the Superior archetype and Winter pieces, which can be slotted only at 50, store 49.
The data floors in this build are 19 for Luck of the Gambler and Aegis, 14 for Unbreakable Guard, Gaussian's and Preventive Medicine, and 4 for Achilles' Heel and Steadfast Protection.
The wiki and `docs/survivability-planning.md` state the set minimum − 3 instead: 22, 17 (Unbreakable Guard, Preventive Medicine), 18 (Gaussian's) and 7. Neither figure has been checked in game.
Pieces that ignore the exemplar rule (`bonuses_ignore_exemplar: true`: the Superior archetype and Winter sets, Overwhelming Force) and procs are unaffected.
**The per-level tables and the cost table below were computed with the wiki's floors** and have not been recomputed:
under the data floors the exemplar 15 rows gain the Unbreakable Guard, Gaussian's and Preventive Medicine bonuses, and the 20 and 21 rows gain Luck of the Gambler's and Aegis'.

**What the file costs if slotted as written** (crafted, at the levels above), against the per-level tables [A]. A crafted piece keeps its
set bonus and global down to three levels below its own level (the wiki's rule; the data has no field for it); an attuned piece down to its data floor.
Values are the set-bonus and global figures in the Mids data; hit points are a share of the
class's base hit points at that level. The rule of five was not re-run for these differences.

| Exemplar | Defense | Resistance | Hit points | Recharge (Mids-style) | Accuracy | Also lost |
| --- | --- | --- | --- | --- | --- | --- |
| 10-15 | AoE −1.9 (Fire/Cold −0.9): Achilles' Heel three-piece | — | — | — | — | damage −1.5% (Achilles' Heel two-piece) |
| 20 | melee −3.1 (S/L −1.6): Unbreakable Guard four-piece | E/N −2.25, Toxic and Psionic −3.75 (Unbreakable Guard) | −9.4% of base, 639 → about 597: the +Max HP global and Gaussian's three-piece | — | — | endurance cost −2.5%, recovery −2.5%, the Preventive Medicine absorb |
| 25-45 | melee −3.1 (S/L −1.6) | E/N −2.25, Toxic −3.75, Psionic −8.75 (with Aegis' 5) | −12.75% of base, adding Luck of the Gambler's three-piece ×3: 854 → about 779 at 25, 1,061 → 968 at 30, 1,255 → 1,145 at 35, 1,414 → 1,290 at 40, 1,518 → 1,385 at 45 | −30%: the four +7.5% globals | −27% (+42% → +15%): Luck of the Gambler's four-piece ×3 | as at 20, plus regeneration −40% (Luck of the Gambler's two-piece ×4) and mez resistance −20% (Aegis) |

- **Where the losses start:** at the data floors, 14 (Unbreakable Guard, Preventive Medicine, Gaussian's) and 19 (Luck of the Gambler, Aegis);
  Achilles' Heel's bonuses are lost below 17. From 47 the file and the design agree.
- **Recharge** is Mids' figure, Hasten not counted. The "Recharge" columns below are time-averages with Hasten at its real uptime;
  30% less recharge also shortens Hasten's uptime, so the time-average falls by more. The hit-chance tables at 25-45 use the +42%
  accuracy, so they fall too. Neither was re-measured.
- **Proc or global, by the game data.** Each piece's `boost_info` in
  `data/canonical/powers/boosts/crafted_<piece>/crafted_<piece>.json` says which it is.
  - **Procs** (`is_proc: true`, effect in the piece itself, no `global_bonuses`) work at any piece level while their power is usable:
    Achilles' Heel's Chance for Res Debuff, Gaussian's Chance for Build Up and Regenerative Tissue's +Regeneration. Regenerative Tissue
    is a lone piece, so crafted 30 costs nothing: 28 of the 29 pieces cost something, it does not. The Achilles' Heel and Gaussian's
    procs stay on the list because they count toward their sets' bonuses, which follow the level rule.
  - **Preventive Medicine's Chance for +Absorb is a global** (`is_proc: false`; its absorb comes from the `global_bonuses` power
    `set_bonus.set_bonus.preventive_medicine`, which fires by chance). It follows the level rule: from 14 attuned (the data floor), from 47 as the file
    holds it. It counts in no total here.

## Checks run on this file

Every check below was run on this build itself (the file in this folder). No figure is borrowed from another variant.

1. **Repository validator** (`python -m coh_engine validate`): no violations.
2. **Slotting check** (`CohEngine.Cli.exe validate --mids-dir tests/fixtures/mids --slotting`): no violations.
3. **Mids dump harness:** totals written; the expected E21 and E14 exits only. A second dump of the delivered `.mbd` gives totals
   byte-identical to the delivered `.mids-totals.json`.
4. **Hit chance:** at 50, 95% against +4 on the six keyed attacks, Surveillance, Wide Area Web Grenade and Boxing; Brawl 91.1%.
   By exemplar level 10 to 50 and enemy level +0 to +4 for every keyed attack and Surveillance: tables in "Hit chance by level".
5. **Chains:** single target and 10-target spawn at every exemplar level 10 to 50 (Hasten at its real uptime), and at 50 as Mids counts
   Hasten; bars as measured and with the proc estimate.
6. **Enhancement Diversification audit:** every power, every aspect, measured on this build.
7. **Exemplar:** the full table at 10, 15, 20, 25, 30, 35, 40, 45 and 50, re-measured on this file with the pieces in "Buy these pieces
   attuned" treated as attuned, not as the file's crafted stand-ins. Parity at 50 against the harness matches every field but hit points
   (0.10 apart). The hand-worked check passes: +30.0% recharge at exactly exemplar 22 under the wiki floor the tables use (19 under the data floor).
8. **Survival score:** both tiers, both damage mixes, at 50, against the previous delivery (22 November) and against the final
   Bane 4-star build, with the hand check.
9. **Boost evidence:** each boosted piece's value before and after (all ×1.25). Set bonuses and globals are byte-identical. 95 boosters.
10. **Slot check** against the previous delivery (22 November): no slot moved; two pieces swapped places (Kismet into Maneuvers,
    Shield Wall's +5% resistance piece into Combat Training: Defensive); 67 added slots.
11. **Granted slots:** every slot of every power is filled (none empty).
12. **File order:** the level 1 primary pick (Single Shot) comes before the secondary pick (Bane Spider Armor Upgrade), and the pools
    are listed in the order they are first picked (Leadership 4, Leaping 6, Speed 28, Fighting 30): passes.
13. **Piece kinds against the file:** the kind of every piece in the slotting table was compared with the `.mbd`. They agree except the
    29 pieces in "Buy these pieces attuned": the table gives them as attuned, the in-game kind, and the file holds them crafted, because
    the Mids 3.8.6 database has no attuned form of a regular set. One of them, Regenerative Tissue, is a proc, so its difference costs
    nothing.

Before any change, the previously delivered build was rebuilt from its spec: the rebuilt `.mbd` is byte-identical to the delivered one,
and its dump is byte-identical to the delivered `.mids-totals.json`.

## What this revision changes

**Against the previous delivery (22 November): one swap.** Kismet: Accuracy +6% (crafted 10) moves from Combat Training: Defensive
(pick 47, usable from 42) into Maneuvers (pick 4), and Shield Wall: +Res (Teleportation), +5% Res (All) moves the other way. Kismet is
a proc by the data, so its +6% to-hit works whenever its power is usable: now from exemplar 1.
Shield Wall's +5% resistance is a PvP-set piece: its everyday global, which works on every map, ignores the exemplar level rule, so it works wherever it sits.
The level 1 primary pick now comes first and the pools are in first-pick order (no effect on totals).

| Measure | Previous delivery | **This build** |
| --- | --- | --- |
| Standing to-hit at exemplar 10 / 15-35 / 40-50 | 0 / +10 / +20 to +26% | **+6 / +16 / +26%** |
| Lowest keyed-attack hit chance, the four-star check, at 10 / 20 / 30 / 40 | 44.0 / 59.1 / 80.0 / 94.8% | **49.3 / 65.1 / 88.0 / 95%** |
| Surveillance, the four-star check, at 25 / 35 / 45 | 67.2 / 71.6 / 90.6% | **74.0 / 78.7 / 90.6%** |
| Damage overstated at +2 / +4 by ignoring misses, exemplar 10 (single chain) | 20.5 / 44.7% | **12.0 / 36.1%** |
| Shield Wall pieces in Maneuvers | five (five-piece bonus: +2.5% damage) | four: the five-piece bonus is lost |
| Standing damage at 50 | +49.0% | +46.5% |
| Chain damage, single / spawn | 228.1 / 1,053.2 dmg/s at 50 | 226.6 / 1,046.8: about 1% lower at every level 10 to 50 |
| Defense, resistance, hit points, recovery, recharge, knockback (11), bars | — | identical at every exemplar level |
| Survival score at 50 | 100 | 100.0 in every column |

The table below records the earlier revision against the file delivered on 21 November.


Against the previously delivered build. **One slot moves**, five boosts are dropped, and the five empty granted slots are filled.

| Measure | Previously delivered | **This build** |
| --- | --- | --- |
| Surveillance | Achilles' Heel ×3 (proc, Defense Debuff, Defense Debuff/Recharge) | the same three, **plus an Accuracy IO, crafted 50, +5** |
| Surveillance hit chance vs +4 at 50 | 70.1% | **95%** (96.3% before the cap) |
| Combat Training: Defensive | Kismet (crafted 10), Karma: Knockback Protection (attuned) | Kismet only; its level 48 slot went to Surveillance |
| Knockback protection | 15 at every level | **11** at every level |
| Boosts dropped (keep under half after ED) | Gladiator's Armor Resistance and Recharge/Resistance; Apocalypse Damage/Recharge/Accuracy, Damage/Recharge, Damage | all five at +0 |
| Granted slots | Brawl, Swift, Hurdle, Sprint, Rest empty | Accuracy, Run Speed, Jump, Run Speed, Endurance Modification IOs, crafted 50, none boosted |
| Resistance at 50, every type | 43.69, 29.91, 24.66, 31.41, 36.41 | 43.57, 29.79, 24.54, 31.29, 36.29 (−0.12: the dropped boosts) |
| Defense, hit points, recovery, recharge | — | unchanged at every exemplar level |
| Single-target damage at 50 (Hasten at its real uptime) | 221.0 dmg/s, bar 170.1 s | 219.0 dmg/s, bar 172.8 s |
| Survival score at 50, every column | 100 | 99.8 |
| Enhancement Boosters | 115 (23 pieces) | 95 (19 pieces) |

**Why this fix for Surveillance.** The Achilles' Heel set has no accuracy piece in the data (Defense Debuff, Defense Debuff/Recharge and
the proc). Every arrangement inside the three existing slots that reaches 95% drops an Achilles' Heel piece, which loses the set's
three-piece bonus: AoE defense −1.88 and Fire/Cold defense −0.94 at every level, four-star score 96.8 (simple and logs).
A fourth slot keeps that bonus. The cheapest donor measured is Combat Training: Defensive's Karma piece: it costs 4 of the 15 points of
knockback protection and nothing in the survival score or the per-level tables but knockback (see "His call").

## About this build

This build is for a third build slot. Its job is playing exemplared task forces from 10 to 45, and it must also be a build worth logging in on at 50.

- **Branch:** Bane Spider Soldier / Bane Spider Training with the rifle attacks, and the Leaping, Speed, Leadership and Fighting pools. No epic pool.
- **Layout:** level 50 after the forced respec at 24, which lets the Bane Spider powers sit in the early picks.
- **What it is:** a re-ordered version of the delivered 4-star Bane. The team auras come early, and every set is chosen for exemplaring.
- **This is the third version.**
  - The first version was rejected (a one-slot attack on the key row, Hasten at 47, chains at level 50 recharge).
  - The second version had Hasten at pick 8 and Heavy Burst at 28. It is kept below as a labelled comparison row.
  - This version swaps those two picks, as the operator chose: **Heavy Burst at 8, Hasten at 28**. Every other pick and every piece is the same.

Loadable file: `bane-rifle-exemplar.mbd`, stamped with Mids database 2026.5.1337. It holds crafted stand-ins for the attuned pieces
(see "Buy these pieces attuned").

Every figure carries one of these marks:

- **[H] harness-measured:** the Mids dump harness on the copied installed database; `.mids-totals.json` is its `totals.json`.
- **[S] script-computed from the rules:** the engine's recompute with the Homecoming wiki's exemplar rules applied (see "How the exemplar figures were computed").
- **[P] planner-measured:** the repository's chain planner, run on a copy of the dump rewritten to each exemplar level.
- **[A] arithmetic.**

## What the swap changed, per level

Both builds are identical from exemplar 23 up: both picks are usable there, so every figure from 25 to 50 is the same [S, P].
At 10-20 this build has Heavy Burst and no Hasten; the Hasten-at-8 build had Hasten and no Heavy Burst.

| Exemplar | Single target DPS [P]: this / Hasten at 8 | Spawn of 10 DPS [P]: this / Hasten at 8 | Idle per cycle, single [P]: this / Hasten at 8 | Full bar, single / spawn [P]: this / Hasten at 8 |
| --- | --- | --- | --- | --- |
| 10 | not usable | +20% | 15.9, 6.66 / 4.42 s | 67.2, 17.58 / 10.84 s |
| 15 | not usable | +20% | 28.7, 15.82 / 7.24 s | 173.8, 15.82 / 7.24 s |
| 20 | not usable | +40% | 47.8, 13.12 / 4.54 s | 288.9, 13.12 / 4.54 s |
| 25 to 50 | same | same | same | same |

- **Bought:** spawn damage +59% at 10, +28% at 15 and +29% at 20, and key 4 (Heavy Burst) live from exemplar 3.
- **Cost:**
  - single-target damage −12% at 10, −7% at 15 and −4% at 20;
  - Hasten only from exemplar 23;
  - shorter endurance bars at 15-20.
- **Idle time:** at 15 and 20 the single-target chain is the same five-power rotation as the spawn chain.
  A longer cycle with more attacks in it idles more seconds per cycle but a smaller share of the time.

**Does endurance still hold at 10 to 20? Yes.**

- **Exemplar 10:** the bar lasts indefinitely.
- **Exemplar 15 and 20, worst case:** 135 s and 109 s. That covers the typical fight at the median (30 s) and at the 90th percentile (108 s, by 1 s at exemplar 20).
- **With the proc estimate:** the bar lasts about 860 s and 345 s [A].
- **Pauses:** the median 31 s pause refills 58-60 endurance [A].

No fix is needed at 10-20.

## Level 50

The previously delivered build and the Hasten-at-8 build gave the same harness totals at 50 [H].

- **Defense** [H]: 55.2 melee, 58.4 ranged, 59.0 AoE.
- **Attacks:** six on keys 1 to 6, each six-slotted, each hitting a +4 enemy 95% of the time [S]. Surveillance, Wide Area Web
  Grenade and Boxing also reach 95% [H]; Brawl 91.1% [H].
- **Chains** [P], with Hasten at its real 71% uptime:
  - single target 218 DPS, a full bar lasts 173 s;
  - 10-target spawn 1,021 DPS, bar 112 s.

## Damage, Hasten and endurance per level (this build)

Hasten's recharge and uptime at each level come from its own recharge with the recharge that survives there [S].

| Exemplar | Hasten recharge / uptime | Global recharge | Single DPS, cycle / idle [P] | Spawn DPS, cycle / idle [P] |
| --- | --- | --- | --- | --- |
| 10 | not usable | +20% | 16.1, 6.66 / 4.42 s | 68.0, 17.58 / 10.84 s |
| 15 | not usable | +20% | 29.0, 15.82 / 7.24 s | 175.4, 15.82 / 7.24 s |
| 20 | not usable | +40% | 48.2, 13.12 / 4.54 s | 291.3, 13.12 / 4.54 s |
| 25 | 195 s / 61% | +113% | 90.3, 9.69 / 1.11 s | 547.8, 9.69 / 1.11 s |
| 30 | 182 s / 66% | +116% | 111.1, 9.48 / 0.90 s | 675.3, 9.48 / 0.90 s |
| 35 | 179 s / 67% | +117% | 170.2, 4.73 / 0.64 s | 800.1, 9.43 / 0.05 s |
| 40 | 179 s / 67% | +117% | 197.4, 4.73 / 0.64 s | 927.9, 9.43 / 0.05 s |
| 45 | 179 s / 67% | +117% | 210.4, 4.73 / 0.64 s | 989.2, 9.43 / 0.05 s |
| 50 | 169 s / 71% | +119% | 217.7, 4.71 / 0.62 s | 1,021.0, 9.37 / 0.00 s |

**What Hasten is worth once it is usable** [P]: +13% to +16% damage at 25-50 against the same build without Hasten. That is measured on the Hasten-at-8 build, which is identical from 23 up.

**Endurance per level**, single-target chain / spawn chain, in end/s [P]:

| Exemplar | Recovery | Toggles | Chain | Other clicks | Net | Full bar | Full bar with procs (estimate) | Refilled in a 31 s pause [A] |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | 28.8 / 27.5 / 35.6 | 14, 26, 18, 24, 24 | 299 | 0 | +0.28 / +0.25 | 4 / 4 / 9.6 / 11 | lasts / lasts | 65 |
| 15 | 29.8 / 28.6 / 36.7 | 15, 27, 19, 24, 24 | 434 | 0 | −0.77 / −0.77 | 4 / 4 / 10.2 / 11 | about 860 s | 58 |
| 20 | 43.5 / 39.1 / 47.2 | 15, 27, 22, 29, 29 | 639 | 0 | −0.95 / −0.95 | 4 / 4 / 10.8 / 11 | about 345 s | 60 |
| 25 | 48.0 / 43.6 / 51.8 | 16, 28, 23, 30, 35 | 854 | 0.49 | −1.69 / −1.69 | 4 / 4 / 11.4 / 11 | 100 / 100 s | 64 |
| 30 | 55.2 / 50.9 / 59.0 | 40, 29, 23, 30, 35 | 1,061 | 0.49 | −1.77 / −1.77 | 4 / 4 / 12.0 / 11 | 92 / 92 s | 56 |
| 35 | 55.2 / 50.9 / 59.0 | 40, 29, 23, 30, 35 | 1,255 | 0.49 | −1.63 / −1.97 | 4 / 4 / 12.6 / 11 | 106 / 78 s | 58 |
| 40 | 55.2 / 50.9 / 59.0 | 40, 29, 23, 30, 35 | 1,414 | 0.61 | −1.90 / −2.22 | 4 / 4 / 13.2 / 11 | 83 / 66 s | 50 |
| 45 | 55.2 / 58.4 / 59.0 | 44, 30, 25, 31, 36 | 1,518 | 0.43 | −0.67 / −0.99 | 4 / 4 / 13.8 / 11 | lasts / 305 s | 61 |
| 50 | 3.37 | 1.30 | 2.23 / 2.55 | 0.43 | −0.60 / −0.93 | 173 / 112 s | lasts / 370 s | 64 |

**Re-measured on this file** at every level. From 10 to 30 every figure is as before; from 35 up (Poisonous Ray usable) the dropped
Apocalypse boosts cost 1 to 2 dmg/s on the single target and lengthen its cycle by 0.02 s, which lengthens the single-target bar.

**Known weak point: endurance at exemplar 25 to 40 on long fights** (this file, [P]; the proc column is 103.6 / (drain − 0.65) [A]):

| Exemplar | Bar, single / spawn | With the procs (estimate) | Against the fight lengths |
| --- | --- | --- | --- |
| 25 | 61.4 / 61.4 s | about 99 / 99 s | covers the 30 s median fight; short of 108 s (typical, 90th percentile) and 103 s (long, median) |
| 30 | 58.4 / 58.4 s | about 92 / 92 s | the same |
| 35 | 63.5 / 52.6 s | about 106 / 78 s | the same; the single-target bar with procs passes the 103 s long-fight median |
| 40 | 54.6 / 46.6 s | about 83 / 66 s | the shortest bars in the build; the spawn bar is under half the 108 s mark |

From 45 Cardiac Core's endurance reduction lifts the bars to 154.6 / 104.2 s, and at 50 to 172.8 / 111.8 s.

**The Hasten-at-8 build's endurance at 10-20**, for comparison [P]:

| Exemplar | Net, single / spawn | Bar, single / spawn |
| --- | --- | --- |
| 10 | +0.01 / +0.62 | lasts / lasts |
| 15 | −0.26 / −0.50 | 398 / 207 s |
| 20 | −0.36 / −0.65 | 288 / 159 s |

From 25 up it is the same as this build.

How these figures are built:

- **Recovery** is Mids' formula as the planner uses it: (1 + EndRec) × 1.05 × 1.6667 × (1 + EndMax/100).
  - Base recovery and max endurance do not change with level in the class data.
  - It includes Stamina with its slotting scaled, Numina's +10% and Miracle's +15% recovery, and the set bonuses that survive.
- **Proc estimate** [A]: Performance Shifter (1.5 per minute) and Panacea (3.0 per minute) in always-on powers fire on a 10 s tick with chance = rate × 10/60.
  That is about +0.26 and +0.39 end/s; neither is counted by Mids or the planner.
- **The weak band is still 25-44.** The chain has five or six attacks and +113-117% recharge, but Cardiac's endurance reduction starts only at 45.
  - The bars there (47-63 s worst case, 66-105 s with procs) cover the median typical fight (30 s).
  - They fall short of the 90th percentile (108 s) and of the median long fight (103 s) on spawns.
  - For long fights there, leave Frag Grenade out of the single-target chain, or use a blue inspiration.
- **Fixes already in the build:**
  - Stamina with the proc plus two EndMod pieces. Recovery at 40 is +64.5% with the proc alone, +74.9% with one EndMod and +83.0% with two [S].
  - Sprint kept out of the totals.
  - End/Recharge pieces in place of pure-Defense pieces in four toggles.
- **In play:** the planner fires every power the instant it recharges. The 4-star write-up records that real play fires each power two to three times less often, so these bars are worst cases.

## Totals at 50 and at each exemplar level

All [S] except the 50 row [H]. The rows below 50 assume the attuned pieces; "Buy these pieces attuned" gives what the file as written
loses.

- **Recharge** is the time-average with Hasten at its real uptime [S]. Mids' own figure, which counts Hasten as always on, is +140% at 50.
- **Mez protection** is hold, stun, immobilize and knockback.
- **Team to-hit** is the auras' share (0 / 10 / 20 by level); the script's aura difference now also counts Kismet (+6, self only),
  because it sits in Maneuvers.

| Level | Defense M / R / AoE | Res S/L, F/C, E/N, Tox, Psi | HP | Regen HP/s | Recharge | Mez prot. H/S/I/KB | Attacks usable | Team def / dmg / to-hit |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | 28.8 / 27.5 / 35.6 | 14, 26, 19, 24, 24 | 299 | 1.39 | +20% | 4 / 4 / 9.6 / 15 | Single Shot, Burst, Heavy Burst, Venom | 15.1 / 15 / 0 |
| 15 | 29.8 / 28.6 / 36.7 | 15, 27, 19, 25, 25 | 434 | 1.39 | +20% | 4 / 4 / 10.2 / 15 | + Frag (5) | 16.0 / 30 / 10 |
| 20 | 43.5 / 39.1 / 47.2 | 16, 28, 22, 29, 29 | 639 | 1.39 | +40% | 4 / 4 / 10.8 / 15 | 5 | 17.1 / 30 / 10 |
| 25 | 48.0 / 43.6 / 51.8 | 17, 29, 23, 30, 35 | 854 | 1.79 | +113% | 4 / 4 / 11.4 / 15 | 5 | 19.8 / 30 / 10 |
| 30 | 55.2 / 50.9 / 59.0 | 40, 29, 24, 30, 35 | 1,061 | 1.79 | +116% | 4 / 4 / 12.0 / 15 | 5 | 19.8 / 30 / 10 |
| 35 | 55.2 / 50.9 / 59.0 | 40, 29, 24, 30, 35 | 1,255 | 1.79 | +117% | 4 / 4 / 12.6 / 15 | + Poisonous Ray (6) | 19.8 / 30 / 10 |
| 40 | 55.2 / 50.9 / 59.0 | 40, 29, 24, 30, 35 | 1,414 | 1.79 | +117% | 4 / 4 / 13.2 / 15 | 6 | 19.8 / 30 / 20 |
| 45 | 55.2 / 58.4 / 59.0 | 44, 30, 25, 31, 36 | 1,518 | 1.79 | +117% | 4 / 4 / 13.8 / 15 | 6 | 19.8 / 30 / 20 |
| 50 [H] | 55.23 / 58.35 / 58.98 | 43.57, 29.79, 24.54, 31.29, 36.29 | 1,578.3 | 1.79 | +119% (Mids +140%) | 4 / 4 / 14.4 / 11 | 6 | 19.8 / 30 / 20 [S] |

- **The swap changes only recharge (at 10-20) and the attacks usable** in this table. Defense, resistance, hit points and team buffs are the same as the Hasten-at-8 build at every level [S].
- **Typed defense at 50** [H]: S/L 51.2, F/C 60.5, E/N 54.6, Toxic 36.3, Psionic 41.3.
- **Team debuffs** [H], each from the exemplar level shown:
  - Venom Grenade, from 7: −20% resistance for 16 s, recharging in 7 s;
  - Surveillance, from 21: −20% resistance and −21% defense for 20 s, recharging in 17 s;
  - the rifle attacks: −7.5% defense on every hit;
  - the Achilles' Heel proc in Surveillance and the Dominion proc in Venom Grenade (both chance-based).

**What the defense means** [A]. With the planning figures (team content at +3/+4: 10% to be hit at 45%, each point short adds 2):

| Exemplar | Ranged | AoE |
| --- | --- | --- |
| 10-15 | 43-45% to be hit | 27-29% |
| 20 | 22% | 10% |
| 25 | 13% | 10% |
| 30 and up | 10% | 10% |

**Hit chance against +4, the four-star check** (the 4-star write-ups' formula, with each level's scaled accuracy and to-hit) [S + A]:

| Exemplar | To-hit | Lowest keyed attack | Frag Grenade | Surveillance (usable from 21) | Lowest keyed attack, previous delivery |
| --- | --- | --- | --- | --- | --- |
| 10 | +6% | 49.3% | not usable below 13 | not usable | 44.0% |
| 15 | +16% | 60.4% | 60.4% | not usable | 55.0% |
| 20 | +16% | 65.1% | 65.1% | not usable | 59.1% |
| 21 | +16% | — | 69.3% | 59.8% | — |
| 25 | +16% | 83.9% | 83.9% | 74.0% | 76.3% |
| 30 | +16% | 88.0% | 88.0% | 77.6% | 80.0% |
| 35 | +16% | 89.3% | 89.3% | 78.7% | 81.2% |
| 40 | +26% | 95% | **95%** | 90.6% | 94.8% |
| 45 | +26% | 95% | 95% (102.9%) | 90.6% | 95% |
| 50 | +26% | 95% | 95% (102.9%) | **95% (96.3%)** | 95% |

Frag Grenade now reaches 95% at 40 (it was 94.8%): Kismet is live there. Surveillance's Accuracy IO is capped at 41.5% at 45 and below,
so it reaches 95% under this check only at 50.

### Hit chance by level

Each keyed attack and Surveillance, at each exemplar level from the level it is usable, against enemies +0 to +4 [S + A]:

- hit = bounded 5% to 95% of accuracy × (base chance for the level difference + to-hit), with enemy defense taken as zero;
- base chance from Arcanaville's "Guide to ToHit and Defense" (the purple-patch section, quoting a developer post), the guide that
  `docs/survivability-planning.md` cites: +0 75%, +1 65%, +2 56%, +3 48%, +4 39%;
- to-hit and accuracy as live at each level: Kismet +6% from 1, Tactical Training: Leadership +10% from 15, Tactics +10% from 39;
  global accuracy +15%, then +42% from 22 in these tables (the attuned Luck of the Gambler four-piece bonuses; 19 by the data floor); slotted accuracy scaled and capped.

**Two formulas are in use across these write-ups.** These tables use the guide's level-difference figures. The level 50 hit-chance
line and the table above use the stricter four-star check, accuracy × (0.75 + to-hit − 0.25) × 0.65. At +4 the two differ by about a
quarter: at 50 the check's term is 0.494 against the guide's 0.65 (0.39 + 0.26 to-hit). Neither has been checked against the game.

Cells under 95% are in bold; "(low)" marks those under 75%; "n/u": not usable yet.

#### Against +0

| Exemplar | To-hit | Single Shot | Burst | Poisonous Ray | Heavy Burst | Venom Grenade | Frag Grenade | Surveillance |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | +6% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | n/u | n/u |
| 15 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | n/u |
| 20 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | n/u |
| 25 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 30 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 35 | +16% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 40 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 45 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 50 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |

#### Against +1

| Exemplar | To-hit | Single Shot | Burst | Poisonous Ray | Heavy Burst | Venom Grenade | Frag Grenade | Surveillance |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | +6% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | n/u | n/u |
| 15 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | n/u |
| 20 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | n/u |
| 25 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 30 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 35 | +16% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 40 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 45 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 50 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |

#### Against +2

| Exemplar | To-hit | Single Shot | Burst | Poisonous Ray | Heavy Burst | Venom Grenade | Frag Grenade | Surveillance |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | +6% | **88.1** | **88.0** | n/u | **88.0** | **83.9** | n/u | n/u |
| 15 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | n/u |
| 20 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | n/u |
| 25 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 30 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 35 | +16% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 40 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 45 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 50 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |

#### Against +3

| Exemplar | To-hit | Single Shot | Burst | Poisonous Ray | Heavy Burst | Venom Grenade | Frag Grenade | Surveillance |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | +6% | **76.8** | **76.6** | n/u | **76.6** | **73.1** (low) | n/u | n/u |
| 15 | +16% | 95.0 | 95.0 | n/u | 95.0 | **94.3** | **90.2** | n/u |
| 20 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | n/u |
| 25 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 30 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 35 | +16% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 40 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 45 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 50 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |

#### Against +4

| Exemplar | To-hit | Single Shot | Burst | Poisonous Ray | Heavy Burst | Venom Grenade | Frag Grenade | Surveillance |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | +6% | **64.0** (low) | **63.9** (low) | n/u | **63.9** (low) | **60.9** (low) | n/u | n/u |
| 15 | +16% | **85.1** | **86.8** | n/u | **86.8** | **81.1** | **77.5** | n/u |
| 20 | +16% | **92.9** | 95.0 | n/u | 95.0 | **88.4** | **83.4** | n/u |
| 25 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | **94.8** |
| 30 | +16% | 95.0 | 95.0 | n/u | 95.0 | 95.0 | 95.0 | 95.0 |
| 35 | +16% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 40 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 45 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |
| 50 | +26% | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 | 95.0 |

#### Damage overstated by ignoring misses (single / spawn chains)

| Exemplar | +0 | +1 | +2 | +3 | +4 |
| --- | --- | --- | --- | --- | --- |
| 10 | 5.0 / 5.0% | 5.0 / 5.0% | 12.0 / 13.8% | 23.3 / 24.9% | 36.1 / 37.4% |
| 15 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 6.0 / 6.7% | 16.3 / 17.9% |
| 20 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 8.7 / 10.7% |
| 25 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% |
| 30 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% |
| 35 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% |
| 40 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% |
| 45 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% |
| 50 | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% | 5.0 / 5.0% |

The damage figures in this write-up assume every attack lands. The last table weights each chain attack's planner damage by its hit chance;
its 5% floor is the 95% cap. **His logged low task forces:** solo at +0 misses nothing; a team at +2 to +3 misses at exemplar 10 to 15.

**What the swap bought, per level:** +6 to-hit at every level. At exemplar 10, +1 enemies reach 95% (from 88.0 to 92.4%) and +2 rise
by about 8 points; at 15 and 20, +3 reaches 95% except Venom Grenade (94.3%) and Frag Grenade (90.2%) at 15; +4 rises by 8.5 to 10 points
at 15 and 20; Surveillance against +4 reaches 95% from 30 (94.8% at 25). From 40 the guide's figures were already at 95%.

## At 50 against the 4-star Bane

Against the revised 4-star Bane (its write-up, delivered with this one). Both columns are harness totals and the planner on each file's
own dump, with Hasten counted as always on, as Mids counts it [H, P].

| Measure | This build at 50 | Bane 4-star, final |
| --- | --- | --- |
| Defense melee / ranged / AoE | 55.23 / 58.35 / 58.98 | 60.94 / 62.81 / 62.81 |
| Resistance S/L, F/C, E/N | 43.57, 29.79, 24.54 | 50.85, 35.41, 22.66 |
| Resistance Toxic / Psionic | 31.29 / 36.29 | 27.16 / 32.16 |
| Hit points / regeneration (Mids `HPRegen`) | 1,578.3 / 1.79 | 1,542.2 / 1.67 |
| Knockback protection | 11 | 4 |
| Recharge (Mids, Hasten counted on) | +140%, Hasten 134.0 s | +162.5%, Hasten 125.6 s |
| Hit chance vs +4 | 95% on the six keyed attacks and Surveillance | 95% on every attack, Surveillance included |
| Single-target chain | 226.6 dmg/s, bar 149.0 s | 219.4 dmg/s, bar 119.2 s |
| 10-target spawn chain | 1,046.8 dmg/s, bar 123.6 s | 1,028.3 dmg/s, bar 101.8 s |
| Toggles / recovery | 1.30 / 3.37 end/s | 1.32 / 3.22 end/s |
| Frag Grenade | knockdown (Overwhelming Force) | knockback |
| Survival score, team simple / logs | **97.0 / 100.7** | 100 / 100 |
| Survival score, 4-star simple / logs | **81.5 / 84.6** | 100 / 100 |

### Survival score at 50

The method of the 4-star write-ups (`docs/survivability-planning.md`, simplified): chance to be hit per position = min(95%,
bounded(enemy base chance − defense, 5% to 95%) × 2.0), team base 50%, four-star base 80%; positions 10/45/45 (20/40/40 also run); a tenth
of attacks with defense taken as zero; damage let through by the simple and the logs mixes; index = hit points ÷ (chance to be hit ×
damage let through). This build has no Serum, so hit points are Mids' figure.

| Build (at 50) | Team, simple | Team, logs | 4-star, simple | 4-star, logs |
| --- | --- | --- | --- | --- |
| Previous delivery (22 November) | 100.0 | 100.0 | 100.0 | 100.0 |
| **This file** | **100.0** | **100.0** | **100.0** | **100.0** |
| **This file, against the Bane 4-star final** | **97.0** | **100.7** | **81.5** | **84.6** |

The 20/40/40 weighting moves no column by more than 0.4 point.

**Hand check** on this file, four-star, simple mix, 10/45/45:

- melee min(95, (80 − 55.23) × 2) = 49.54%; ranged (80 − 58.35) × 2 = 43.30%; AoE (80 − 58.98) × 2 = 42.04%;
- blended 0.10 × 49.54 + 0.45 × 43.30 + 0.45 × 42.04 = 43.36%; with the allowance 0.9 × 43.36 + 0.1 × 95 = 48.52%;
- let through 0.25 × (1 − 0.4357) × 2 + 0.125 × (1 − 0.2454) × 2 + 0.0625 × [(1 − 0.2979) × 2 + (1 − 0.3129) + (1 − 0.3629)] = 64.13%;
- index 1,578.3 / (0.4852 × 0.6413) = 5,072; the Bane 4-star final's is 6,223; 5,072 / 6,223 = 81.5, as the table shows.

**What the exemplar build gives up at 50**, against the 4-star final:

- 3.8 to 5.7 points of defense by position;
- 7.3 points of S/L and 5.6 of F/C resistance (E/N is 1.9 and Toxic and Psionic 4.1 higher here);
- in the score: 3.0 points in team content on the simple mix (0.7 ahead on the logs mix), 18.5 and 15.4 points in four-star content;
- 22.5% recharge.

**What it gains:** 36 more hit points, knockback protection 11, knockdown on Frag Grenade, longer bars (149.0 / 123.6 s against
119.2 / 101.8 s) and slightly more damage per second; below 50, the team auras by exemplar 9 and set bonuses that survive exemplaring.

**Incarnates at 45 and above:** Cardiac Core Paragon, Ion Core Final Judgement, Reactive Radial Flawless Interface, Barrier Core
Epiphany and Assault Radial Embodiment. None is counted below 45.

## Pick order, and the level each power becomes usable when exemplared

| Pick | Power | Usable from exemplar | Why here |
| --- | --- | --- | --- |
| 1 | Single Shot | 1 | Key 1 |
| 1 | Bane Spider Armor Upgrade | 1 | Mag 4 mez protection, resistance to every damage type; holds the uniques |
| 2 | Burst | 1 | Key 2 |
| 4 | Maneuvers | 1 | Team defense |
| 6 | Combat Jumping | 1 | The travel power, defense |
| 8 | Heavy Burst | 3 | Key 4: the third attack, +28-59% spawn damage at 10-20 (above) |
| 10 | Tactical Training: Maneuvers | 5 | Team defense |
| 12 | Venom Grenade | 7 | Key 5: −20% resistance on the spawn |
| 14 | Assault | 9 | Team damage +15% |
| 16 | Tactical Training: Assault | 11 | Team damage +15% |
| 18 | Frag Grenade | 13 | Key 6; knockback turned to knockdown |
| 20 | Tactical Training: Leadership | 15 | Team to-hit |
| 22 | Mental Training | 17 | At the level it opens |
| 24 | Cloaking Device | 19 | About +12 defense: AoE past 45% from 20, melee from 25 |
| 26 | Surveillance | 21 | ALT+1: −20% resistance, −21% defense on the target |
| 28 | Hasten | 23 | Auto-fire, two Recharge IOs; +13-16% damage once usable |
| 30 | Boxing | 25 | Needed for Tough; never pressed |
| 32 | Tough | 27 | +23 points of smashing/lethal resistance from 30 [S] |
| 35 | Weave | 30 | Defense; with Tough, takes ranged defense past 45 from 30 |
| 38 | Poisonous Ray | 33 | Key 3: the hardest single-target hit |
| 41 | Build Up | 36 | CTRL+1 |
| 44 | Tactics | 39 | Team to-hit |
| 47 | Combat Training: Defensive | 42 | Ranged defense; holds Shield Wall's +5% resistance global |
| 49 | Wide Area Web Grenade | 44 | ALT+2: control |

## Slotting, with the attuned-or-crafted decision per piece

The swap moved no piece. Mids placed some added slots at different levels; that does not matter when exemplared, because every slot in a usable power counts.

| Power | Pieces | Kind (in game; the file's where it differs) | Why |
| --- | --- | --- | --- |
| Bane Spider Armor | Gladiator's Armor +3% Def | crafted 10 | PvP-set piece: its everyday global ignores the exemplar level rule |
| | Steadfast Protection Res/+3% Def, Knockback Protection | crafted 10 | Set 10-30: crafted at 10 keeps them to 7; attuned would keep them to 4, the data floor |
| | Gladiator's Armor Res, Res/Rech, Res/End/Rech | crafted 50; only Res/End/Rech +5 | PvP-set pieces: their everyday bonuses ignore the exemplar level rule (4 pieces: knockback 3, toxic/psionic resistance) |
| Single Shot | Superior Spider's Bite, 6 | attuned (only form) | Archetype set: bonuses at every level |
| Burst | Superior Winter's Bite, 6 | attuned (only form) | Winter set: ranged, E/N, F/C and AoE defense at every level |
| Maneuvers | Shield Wall Def/End, Def/Rech, Def/End/Rech, End/Rech | crafted 50, +5 | PvP-set pieces: their everyday bonuses ignore the exemplar level rule; End/Rech in place of pure Defense for endurance |
| | Kismet +ToHit (moved here) | crafted 10 | Proc by the data: +6% to-hit whenever Maneuvers is usable, so from exemplar 1 |
| Combat Jumping | Luck of the Gambler +Recharge, Def/End | attuned; file: crafted 50 | Global and 2-piece bonus from 19 (data floor) |
| Heavy Burst | Superior Frozen Blast, 6 | attuned (only form) | AoE and F/C defense at every level |
| Tactical Training: Maneuvers | Luck of the Gambler +Recharge, Def/End, Def/End/Rech, End/Rech | attuned; file: crafted 50 | Bonuses from 19 (data floor) |
| Venom Grenade | Superior Dominion of Arachnos, 6 | attuned (only form) | Archetype set; −damage/fear proc |
| Assault, Tactical Training: Assault and Leadership, Tactics | Endurance Reduction | crafted 50, +5 | No set pays for slots there |
| Frag Grenade | Overwhelming Force, 6 | attuned (only form) | Knockback to knockdown; knockback protection 4 at every level |
| Mental Training | Run Speed | crafted 50, +5 | |
| Cloaking Device | Luck of the Gambler +Recharge, Def/End, Def/End/Rech, End/Rech | attuned; file: crafted 50 | Bonuses from 19 (data floor) |
| Surveillance | Achilles' Heel −Res proc, Def Debuff, Def Debuff/Rech | attuned; file: crafted 20 | Proc whenever Surveillance is usable; bonuses from 4 (data floor) |
| | Accuracy (new, fourth slot, level 48) | crafted 50, +5 | 95% against +4 at 50; the +5 is needed (91.1% at +0) |
| Hasten | Recharge Reduction x2 | crafted 50, +5 | The norm |
| Boxing | Accuracy | crafted 50, +5 | One slot: a prerequisite, not on a key |
| Tough | Unbreakable Guard Res, Res/End, Res/Rech, Res/End/Rech, +Max HP | attuned; file: crafted 50 | Set 20-50: bonuses and the +HP global from 14, the data floor (crafted 50 would stop below 47) |
| | Aegis +Res Mez/Psionic | attuned; file: crafted 50 | Global from 19 (data floor) |
| Weave | Luck of the Gambler +Recharge, Def/End, Def/End/Rech, End/Rech | attuned; file: crafted 50 | Bonuses from 19 (data floor) |
| Poisonous Ray | Apocalypse Acc/Dmg/Rech, Acc/Rech, Dmg/Rech, Dmg/End, Dmg | crafted 50; only Acc/Rech and Dmg/End +5 | Purple: bonuses at every level |
| | Apocalypse −Energy proc | crafted 50 | Proc |
| Build Up | Gaussian's ToHit, Rech/ToHit, End/Rech/ToHit, Build Up proc | attuned; file: crafted 50 | Bonuses from 14 (data floor). In Build Up so Mids does not count the proc as always on |
| Combat Training: Defensive | Shield Wall +5% Res (all) (moved here) | crafted 50 | PvP-set piece: its everyday global ignores the exemplar level rule, wherever it sits |
| | (Karma Knockback Protection, removed) | — | Its slot went to Surveillance's Accuracy IO; knockback protection 15 → 11 |
| Wide Area Web Grenade | Accuracy | crafted 50, +5 | One slot (on the ALT row, not the key row) |
| Health | Panacea +HP/End, Numina's +Regen/Recovery (30), Miracle +Recovery (20) | crafted | Procs by the game data (`is_proc: true`): any level works; lowest craftable |
| | Regenerative Tissue +Regen | attuned; file: crafted 30, which works the same | Proc by the game data (`is_proc: true`): any level works |
| | Preventive Medicine +Absorb | attuned; file: crafted 50 | Global by the game data (`is_proc: false`, `global_bonuses`): from 14 attuned (data floor), from 47 crafted 50 |
| Stamina | Performance Shifter +End proc | crafted 21 | Proc; lowest craftable |
| | Performance Shifter EndMod, EndMod/Rech | crafted 50, +5 | The dual-aspect piece keeps its boost at every level. Attuned would add only the 3-piece +1.88% HP at 18-46 |
| **Brawl** (new) | Accuracy | crafted 50 | Takes Accuracy, Damage or Recharge; never pressed; empty it hit +4 at 70.1%, now 91.1% at 50 [H] |
| **Swift** (new) | Run Speed | crafted 50 | Takes Run Speed or Fly Speed; no flight here |
| **Hurdle** (new) | Jumping | crafted 50 | Takes only Jump; Combat Jumping is the travel power |
| **Sprint** (new) | Run Speed | crafted 50 | Takes Run Speed, Jump or Endurance Reduction; Sprint is off in combat and out of the totals, so travel speed is its use |
| **Rest** (new) | Endurance Modification | crafted 50 | Takes Endurance Modification, Heal or Interrupt; endurance is this build's weak band between fights |

**Rules behind the kinds:**

- **Sets that ignore exemplaring** (purple, PvP, archetype, Winter, Overwhelming Force) come first.
  Their everyday bonuses ignore the exemplar level rule, so purple and PvP-set pieces are crafted at 50 and boosted.
- **Regular sets are attuned**, keeping bonuses down to the data floor (`min_bonus_level`; the floors are listed under "Buy these pieces attuned"). Crafted at 50 they stop below 47.
  The Mids file cannot hold an attuned regular-set piece, so it holds the crafted piece at the level in the table's "file:" note.
- **PvP-set pieces count only their everyday bonuses.** Each tier of a PvP set from two pieces up in `data/canonical/boost_sets/<set>.json` has two bonuses:
  one with `is_pvp_bonus: false`, which works on every map, and one with `is_pvp_bonus: true` and `requires: isPVPMap?`, which works only on PvP maps.
  The unique globals (Shield Wall +5% resistance, Gladiator's Armor +3% defense) are `is_pvp_bonus: false`.
  No PvP-only bonus is counted in any figure here (the engine computes in PvE mode), and none is a reason for a pick.
- **Globals crafted at the set minimum** equal attuned under the wiki's rule; under the data floors an attuned piece works three or more levels lower.
- **Procs** ignore the exemplar floors.
- **Single-aspect IOs boosted to +5** gain nothing at 45 and below, because the cap is 41.5% [A].

**The levels from his in-game builds**, as measured on the first version (the same pieces are kept here):

| Piece at his level | Verdict |
| --- | --- |
| Luck of the Gambler at 25 | Works from 22; attuned works from 19 by the data floor |
| Steadfast at 10 | Best |
| Steadfast at 30 | Costs −3.1 defense and −4 knockback protection at 10-25 |
| Impervium at 15 | Best |
| Gladiator's Armor | Any level works |
| Health procs, Performance Shifter at 21, Achilles' Heel at 20 | Any level works |
| Regenerative Tissue | Any level works: a proc in the game data (`is_proc: true`). The first version's verdict, "use 10 or attuned, not 30", treated it as a global |
| Preventive Medicine | Attuned (from 14 by the data floor) or crafted 20 (from 17), not 40 |
| Kismet at 30 | Fine by the data, which flags it as a proc; 10 is safe either way |
| A unique in Tough at 25 | Works from 22: Aegis attuned works from 19 and Unbreakable Guard +HP attuned from 14, by the data floors |

**Data floors in use:** the attuned Luck of the Gambler +Recharge global (piece F) and the set's two- to six-piece bonuses work from 19.
The floor is `boost_info.min_bonus_level` (18) in `data/canonical/powers/boosts/attuned_luck_of_the_gambler_f/attuned_luck_of_the_gambler_f.json`.
Every Luck of the Gambler piece stores the same value.
The hand-worked check shows the +30% recharge jump at 19 under the data floor and at 22 under the wiki's rule, which the per-level tables still use.

### Slot accounting and granted slots

The build uses 67 added slots, 24 picks and 4 pools. Every power's slot levels are identical to the previously delivered file, checked slot
by slot, except Combat Training: Defensive's level 48 slot, now Surveillance's. Every slot of every power and inherent is filled: 0 empty.

## Enhancement Boosters

**The rule** (the 4-star write-ups'): a boost raises only the piece's own enhancement values, 25% at +5; never globals, set bonuses or
procs; attuned pieces cannot be boosted. Mids applies it as the piece's value × 1.25. A piece is boosted when at least half its boost
survives Diversification in the aspects that change a figure, and when the boost buys something; Hasten's two IOs are the operator's norm.

**What is boosted** (19 pieces × 5 = **95 boosters**; the previously delivered file had 115). Values are each piece's own enhancement
before ED, from the harness slots of this build without and with every boost; every ratio is exactly 1.2500. "Kept" is the share of the
boost that survives Diversification at 50 (arithmetic through the engine's ED function).

| Power | Pieces (level) | Enhancement value, unboosted → +5 | Kept at 50 |
| --- | --- | --- | --- |
| Bane Spider Armor Upgrade | Gladiator's Armor: Resistance/Rech/End (50) | Res 12.75 → 15.94%; End, Recharge 21.20 → 26.50% | 77% |
| Maneuvers | Shield Wall: Def/End, Def/Rech, Def/End/Rech, End/Rech (50) | each value × 1.25 | 90% each |
| Poisonous Ray | Apocalypse: Recharge/Accuracy, Damage/Endurance (50) | 33.12 → 41.41% | 63%, 58% |
| Stamina | Performance Shifter: EndMod, EndMod/Recharge (50) | EndMod 42.40 → 53.00%, 26.50 → 33.12% | 91%, 92% |
| Hasten | two Recharge Reduction IOs (50) | 42.40 → 53.00% each | the operator's norm |
| Assault, Tactical Training: Assault and Leadership, Tactics | Endurance Reduction IO (50) | 42.40 → 53.00% | 100% |
| Mental Training | Run Speed IO (50) | 42.40 → 53.00% | 100% |
| Boxing, Wide Area Web Grenade | Accuracy IO (50) | 42.40 → 53.00% | 100% |
| **Surveillance** (new) | Accuracy IO (50) | 42.40 → 53.00% | 100%; it lifts Surveillance from 91.1% to 95% at 50 |

**Dropped** (each keeps under half at 50; 25 boosters): Gladiator's Armor: Resistance (27%) and Recharge/Resistance (35%), Apocalypse:
Damage/Recharge/Accuracy (43%), Damage/Recharge (39%) and Damage (15%). Measured cost: resistance −0.12 every type, single-target
damage at 50 −1.9 dmg/s, survival score 99.8 in every column. **Not boosted:** Steadfast Protection (crafted 10), and Brawl's Accuracy IO
(Brawl is never pressed).

**What the boosts move, from the harness** (this build without any boost against this file): the set-bonus virtual power is
byte-identical; `HPMax`, `HPRegen`, `BuffHaste`, `BuffAcc`, `BuffToHit`, `BuffDam` and mez are identical; defense +0.31 in every position
(Maneuvers' Shield Wall pieces); resistance +0.04 every type (Gladiator's Armor); `EndRec` 0.817 → 0.856; `EndUse` 1.343 → 1.303.

**Below 50** the values are scaled and capped at 41.5% per aspect at 45 and below [S]: the single-aspect IOs (Surveillance's included)
give 41.5% there whether boosted or not.

## Enhancement Diversification audit

Every power of this build at 50, measured with `coh_engine.enh_pipeline.aggregate_and_ed` on its harness slots. These aspects pass the
last threshold (100% on schedule A, 60% on schedule B); every other aspect of every power is under it:

| Power | Aspect | Pre | Post | What holds the slots |
| --- | --- | --- | --- | --- |
| Bane Spider Armor Upgrade | Resistance | 61.8% | 56.3% | Gladiator's Armor (PvP set, everyday bonuses): knockback protection (3), Toxic/Psionic resistance (4) |
| Single Shot | Damage / Recharge | 142.4 / 132.5% | 101.4 / 99.9% | Superior Spider's Bite (archetype set) |
| Burst | Accuracy / Damage / Recharge | 109.3 / 142.4 / 106.0% | 96.4 / 101.4 / 95.9% | Superior Winter's Bite (Winter set) |
| Heavy Burst | Accuracy / Damage | 109.3 / 142.4% | 96.4 / 101.4% | Superior Frozen Blast (Winter set) |
| Venom Grenade | Damage / Recharge | 142.4 / 132.5% | 101.4 / 99.9% | Superior Dominion of Arachnos (archetype set) |
| Frag Grenade | Damage | 114.0% | 97.1% | Overwhelming Force: knockdown, knockback protection 4 |
| Hasten | Recharge | 106.0% | 95.9% | Two +5 Recharge IOs, the operator's norm (a plain IO past ED) |
| Tough | Resistance | 70.1% | 57.5% | Unbreakable Guard, attuned in game (bonuses from 14 by the data floor); crafted 50 in the file, the same values at 50 |
| Poisonous Ray | Damage / Recharge | 182.2 / 115.9% | 107.3 / 97.4% | Apocalypse (purple, six pieces) |

## His call

- **Knockback protection 15 → 11** is the price of Surveillance's fourth slot. Measured alternatives (each a full harness run):

| Surveillance arrangement | Slot moved | Survival score at 50 (team / 4-star, simple mix) | Surveillance at 45 / 40 / 35 / 25 | Other cost |
| --- | --- | --- | --- | --- |
| **This file: Achilles' Heel ×3 + Accuracy +5, Karma's slot** | 1 | 99.8 / 99.8 | 90.6 / 83.5 / 71.6 / 67.2% | knockback 15 → 11 |
| Achilles' Heel proc + two Accuracy IOs (+0), in the existing three slots | 0 | 99.8 / 96.8 | 95 / 95 / 87.2 / 79.1% | AoE −1.88, F/C defense −0.94 at every level; damage −1.5% |
| Achilles' Heel proc + Accuracy IO + Analyze Weakness: Acc/DefDebuff | 0 | 99.8 / 96.8 | 95 / 95 / 81.9 / 74.8% | the same as above |
| Achilles' Heel proc + Analyze Weakness: Acc/DefDebuff, Acc/Rech (attuned) | 0 | 99.8 / 96.8 | 95 / 88.7 / 76.1 / 70.5% | the same as above; spawn damage 1,045.4 against 1,053.2 dmg/s |
| Achilles' Heel proc, Defense Debuff/Recharge + Accuracy +5 | 0 | 99.8 / 96.8 | 90.6 / 83.5 / 71.6 / 67.2% | AoE −1.88, F/C defense −0.94 at every level |
| Achilles' Heel ×3 + Accuracy +5, Kismet's slot (Karma kept) | 1 | 99.8 / 99.8 | 83.5 / 83.5 / 71.6 / 67.2% | fails: to-hit −6 from 42; Surveillance 88.7%, Frag Grenade 94.8% at 50 |

  The scores in this table are against the previously delivered file. The in-slot arrangements reach 95% from 40, but each drops an
  Achilles' Heel piece and its three-piece bonus. Shield Breaker pieces (a 10-30 set) give 91.6% at 50 and were not taken.
- **Steadfast Protection: Resistance/+Def 3%** (crafted 10) passes the boost rule and stays unboosted, as asked: 5 boosters.

## Key map

The rows follow his tray: keys 1-6 for the attack chain, the ALT row for opportunity clicks, the CTRL row for self-buffs, and a fourth bar for toggles.

| Row | Key | Power | Live from exemplar |
| --- | --- | --- | --- |
| Primary | 1 | Single Shot | 1 |
| Primary | 2 | Burst | 1 |
| Primary | 3 | Poisonous Ray | 33 |
| Primary | 4 | Heavy Burst | 3 |
| Primary | 5 | Venom Grenade | 7 |
| Primary | 6 | Frag Grenade | 13 |
| ALT | ALT+1 | Surveillance | 21 |
| ALT | ALT+2 | Wide Area Web Grenade | 44 |
| ALT | ALT+3 | Ion Core Final Judgement | 45 |
| CTRL | CTRL+1 / +2 / +3 | Build Up / Assault Radial Embodiment / Barrier Core Epiphany | 36 / 45 / 45 |
| Auto-fire | 0 | Hasten | 23 |
| Fourth bar | | Maneuvers, Combat Jumping, Tactical Training: Maneuvers, Assault | 1-9 |
| Fourth bar | | Tactical Training: Assault, Tactical Training: Leadership, Cloaking Device | 11-19 |
| Fourth bar | | Tough, Weave, Tactics | 27, 30, 39 |

Keys live by exemplar level:

- **Exemplar 10:** keys 1, 2, 4 and 5.
- **Exemplar 20:** keys 1, 2, 4, 5 and 6.
- **Exemplar 30:** keys 1, 2, 4, 5 and 6, ALT+1, and Hasten on auto-fire.
- **Exemplar 35 and up:** all six keys.

## Measured alternatives

- **Hasten at 8 and Heavy Burst at 28 (the second version, kept as a comparison row).** Its damage, idle time and endurance per level sit beside this build in the tables above.
  - It gives +4% to +14% single-target damage and longer bars at 15-20, but 22-37% less spawn damage at 10-20.
  - Key 4 is dead until 23. Identical from 23 up.
- **Fighting pool (Boxing, Tough, Weave) against Web Cocoon with Superior Entomb, Tactics earlier, Spirit Shark and Arctic Breath.**
  Measured on the Hasten-at-8 order; the Fighting pool's picks are the same in both [S, P].
  - **Without the Fighting pool:**
    - at 10-25, ranged defense is +2.5 and the survival index is 10% higher at 10-20;
    - at 30-45, defense is 45 / 46 / 54 against 55 / 51 / 59, smashing/lethal resistance 17 against 40, and the survival index is 69-101 against 87-129;
    - at 50, the chains are 212 / 1,009 DPS against 221 / 1,028;
    - toggles cost 0.37 end/s less at 30-44.
  - **Kept:** the Fighting pool.
- **Super Jump and Acrobatics (the first version) against Combat Jumping as travel.**
  - Acrobatics gave hold protection 6 instead of 4 and knockback protection 24 instead of 15, from 27.
  - Here knockback protection is 11 at every level from pieces (Steadfast, Overwhelming Force, Gladiator's Armor; Karma's slot now
    serves Surveillance), and hold protection is 4 from the armor.
- **Stamina:** proc alone, proc + EndMod, proc + two EndMods. Recovery at 40 is +64.5%, +74.9% and +83.0% [S]. Kept: three.
- **First-pass branch candidates and the earlier files** (survival index [A] at 10 / 20 / 30, from the totals by the same rules):

| Build | Exemplar 10 / 20 / 30 |
| --- | --- |
| Crab | 7 / 23 / 85 |
| Bane-rifle | 9 / 40 / 69 |
| Huntsman-Crab | 7 / 23 / 85 |
| First delivered version | 11 / 42 / 70 |
| Second version (Hasten at 8) | 9 / 37 / 87 |
| Earlier "Bane v1 - Exemplar" file (Rest switched off in a copy) | 7 / 25 / 72 |
| **This version** | **9 / 37 / 87** (the swap does not move defense, resistance or hit points) |

## What still bends one of his rules

- **Hasten is usable only from exemplar 23** (pick 28). This is his call, made with the numbers above. At 10-20 global recharge is +20-40% instead of +46-75%.
- **Build Up comes at 41 (usable from 36).** The exemplar-10 checklist asks for it by 15. Picks 1-14 hold the attacks, travel and the team auras.
- **Keys at low levels.** At exemplar 10, keys 3 and 6 are dead (Poisonous Ray and Frag Grenade come later); a level 10 window holds nine picks.
- **Endurance at 25-44 runs short on long fights** if the chain is fired flat out (above). At 10-20 it holds.
- **Combat Training: Defensive at 47.** Its +7.5% ranged defense would help most at 10-20, where ranged defense is lowest (27.5-28.6).
  No pick at 14 or below was free without dropping a team aura, travel or an attack.
- **Surveillance reaches 95% against +4 under the four-star check only at 50** (90.6% at 40 and 45); see "Hit chance by level".

## What could not be measured

- **Nothing here comes from the game.** No combat log confirms the wiki's exemplar rules.
- **Damage at each level is scaled by arithmetic.** It is Mids' per-activation figure × (damage table at that level ÷ at 50) × (slotted plus standing damage at that level ÷ at 50).
  The timing, idle time and endurance come from the planner.
- **The hit-chance check is approximate,** with a fixed 0.65 factor for +4. The planner does not cost misses.
- **Proc endurance is an estimate:** expected values from the data's per-minute rates.
- **Cloaking Device's defense is counted as always on.** Whether it holds in combat is not checked.
- **Enemy mez magnitudes** in low task forces are not in the data.

## How the exemplar figures were computed

The engine's `compute_base_totals` on the harness dump, with each power assembled as Mids does.
This is the recipe of the repository's sample-build parity test, and it adds the Health and Stamina pieces' standing effects. At exemplar level N:

1. **Usable powers:** those picked at N + 5 or lower. All their slots count.
2. **Set bonuses and globals:** taken from every power, usable or not.
   - Pieces that ignore exemplaring always count.
   - Attuned pieces count down to the set minimum − 3 in these tables, the wiki's rule; the build now uses the data floors (see "Buy these pieces attuned"), which the tables do not yet reflect.
     Crafted pieces count down to their level − 3.
   - The pieces in "Buy these pieces attuned" are taken as attuned, as designed, although the file holds them crafted.
   - Regenerative Tissue's +Regeneration was described as an attuned global (from 7). The game data makes it a proc (`is_proc: true`),
     which counts at every level Health is usable; either way it counts at every tabulated level (10 and up), so no figure moves.
   - Procs count only in a usable power.
   - The rule of five is applied as Mids applies it.
3. **Values:** each piece's values are scaled by the wiki's table (unchanged at 5% or less, the 10%/20% thresholds, the 41.5% cap), and the class tables and hit points are taken at level N.
4. **Chains:**
   - Each power's recharge and endurance cost come from the engine's per-power stats at N.
   - Hasten enters at its real uptime, from its own recharge at N with the recharge that survives there.
   - The planner then runs on a copy of the dump rewritten to those figures.

**Checks:**

- **Parity at 50:** the script matches the harness on every compared field. Hit points differ by 0.1 because of the previous-recompute term.
- **Hand-worked case:** the build's four Luck of the Gambler +Recharge globals, taken as attuned, switch on at 19 by the data floor (22 by the wiki's rule, which the tables use),
  a jump of exactly 30.0 recharge. Hasten is left out of that check: from 23 it adds its own step.
  The file's crafted 50 stand-ins switch on only at 47.

## Where the repository docs disagree with the wiki

- **Slots placed above the exemplar level + 5:** the exemplar-10 rule and the slotting guide say they do not exist. The wiki says they still work.
- **Bonuses in greyed-out powers:** the docs do not say that set bonuses and globals stay on there.
- **Attuned values:** the slotting guide says attuned values avoid the exemplar penalty. The wiki scales every enhancement.
- **Superior archetype and Winter pieces:** the data gives them a minimum slot level of 50, not 10.
- **Attuned floors:** the wiki states the set minimum − 3; the data's `min_bonus_level` gives a lower floor, which this build uses. Neither has been checked in game.
