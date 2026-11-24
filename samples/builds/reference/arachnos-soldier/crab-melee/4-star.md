# Crab Spider Soldier-Crab Spider Training Arachnos Soldier - Melee - 4-Star

**This is the level 50 layout: the build after the forced respec at level 24, and it cannot be followed before level 24.** Before 24 only the base Arachnos Soldier and Training and Gadgets sets can be picked; Slice, Arm Lash, Frenzy and the Crab Spider Training powers arrive with the respec.

Level 50 Arachnos Soldier on the Crab Spider branch: Crab Spider Soldier / Crab Spider Training, with the Leaping, Speed, Leadership and Fighting pools and no ancillary pool.
Its job on a 4-star team of eight is close-range area damage with the backpack attacks (Slice, Arm Lash, Frenzy), the Crab's survival layer (Crab Spider Armor Upgrade, Fortification, Serum) and the Soldier's team auras: both Maneuvers, both Assaults, Tactical Training: Leadership and Tactics.
Loadable file: `4-star.mbd`. Every number below is from MidsReborn's own totals (`totals.json`, `TotalsCapped`, copied here as `.mids-totals.json`) or from the repository's chain planner, unless it is marked as arithmetic.

The `.mbd` is stamped with Mids database version **2026.5.1337**, the Homecoming database installed in the operator's Mids Reborn. The Mids totals, the chain figures and the planner runs in this document all come from Mids dumps of that database, except where a figure is marked as measured on the earlier database 2026.1.1242.

This version is reworked to the operator's slotting rules of 14 November 2026; see "Slotting rules applied".

## Key map

The operator's tray has four rows. Keys 1 to 6 hold the steady attack chain, ALT+1 to ALT+6 hold opportunity clicks, CTRL holds self-buffs, the Hybrid and the Destiny, and a fourth bar holds the toggles and travel powers, which are never pressed by key. Hasten is on auto-fire (key 0) and takes no slot.
The 1 to 6 and ALT rows hold 8 click powers here, against a limit of 12.

| Row | Key | Power | What it is for |
| --- | --- | --- | --- |
| Attacks | 1 | Slice | Melee single target, 246.7 per activation, recharge 1.73 s; −7.5% defense |
| Attacks | 2 | Longfang | Ranged single target, 333.8 per activation, recharge 2.91 s; the opener while closing in |
| Attacks | 3 | Arm Lash | Melee cone (10 ft, 5 targets), 421.3 per target with its four procs, recharge 3.69 s |
| Attacks | 4 | Frenzy | Point-blank area (10 ft, 10 targets), 244.6 per target with its proc, recharge 6.96 s |
| Attacks | 5 | Frag Grenade | Ranged area (10 targets), 228.8 per target, knockdown instead of knockback (Overwhelming Force) |
| Attacks | 6 | Suppression | Cone (10 targets), 218.4 per target, Magnitude 3 immobilize for 10 s |
| ALT | ALT+1 | Venom Grenade | Debuff click: −20% resistance (−40% toxic) on up to 10 foes for 16 s; recharge 7.11 s |
| ALT | ALT+2 | Ion Core Final Judgement | Nuke on a fresh spawn: 660.8 per activation, recharge 90 s |
| ALT | ALT+3 to ALT+6 | empty | No pick is free for another click; see "Omega Maneuver" under Trade-offs |
| CTRL | CTRL+1 | Aim | +50% to-hit and +50% damage for 10 s, recharge 29.95 s; before Arm Lash and Frenzy |
| CTRL | CTRL+2 | Serum | +637 maximum hit points for 120 s and a heal; recharge 119.8 s, so it is permanent with 0.2 s to spare |
| CTRL | CTRL+3 | Assault Radial Embodiment | Hybrid toggle, on for part of each cycle |
| CTRL | CTRL+4 | Barrier Core Epiphany | Destiny, staggered with the team's other Barrier holders |
| Auto-fire | 0 | Hasten | +70% recharge for 120 s; recharges in 131.0 s |
| Fourth bar | none | Combat Jumping, Maneuvers, Tactical Training: Maneuvers, Tactical Training: Assault, Tactical Training: Leadership, Fortification, Tough, Weave, Assault, Tactics, Sprint | Toggles and travel, switched on once and left on |
| None | none | Boxing | Prerequisite, not on a key: the Fighting pool's entry to Tough and Weave |

Crab Spider Armor Upgrade, Combat Training: Defensive and Mental Training are auto powers and sit on no bar.

**Attack or opportunity click.** The six attacks are the powers the chains below fire on cooldown. Venom Grenade does damage, but its job is the −20% resistance; it is pressed when a spawn or an arch-villain is fresh and again when the debuff runs out, so it sits on ALT. Ion Core Final Judgement recharges in 90 s and is spent on a fresh spawn, so it is an opportunity click.
Aim and Serum are self-buffs (CTRL). Boxing is never pressed.

## Slotting rules applied

One line per rule of the operator's review (14 November 2026), saying what changed from the version delivered on 13 November.

1. **Sets outrank diversification.** Longfang's Superior Winter's Bite is back to six pieces (the endurance pass had taken its sixth slot). Every Very Rare, Winter and archetype set now has five or six pieces: Superior Spider's Bite 6 (Slice), Superior Winter's Bite 6 (Longfang), Superior Dominion of Arachnos 5 (Frag Grenade), Ragnarok 5 (Venom Grenade), Superior Frozen Blast 6 (Suppression).
2. **No over-slotting with plain IOs.** The only plain IOs left are two Recharge IOs in Hasten and single IOs in Aim, the two Assaults, Tactics and Mental Training. None is past the last diversification threshold, and Stamina holds no Endurance Modification IOs.
3. **Hasten: two Recharge IOs, both +5.** Before: two unboosted IOs, picked at 32. After: two +5 IOs, picked at 30.
4. **Permanent Hasten is not a target.** It is gone from the target table. Hasten recharges in 131.0 s against its 120 s duration, about 92% uptime (arithmetic). No slot or set bonus is spent on recharge for its sake.
5. **Single-aspect IOs boosted to +5.** Seven IOs are set to `RelativeLevel: PlusFive` by a post-processing step in the scratch spec: both Hasten Recharge IOs, Aim's Recharge, the Endurance Reductions in Tactical Training: Assault and Assault, Tactics' To Hit Buff and Mental Training's Run Speed. The harness reads the boost: Hasten went from 136.0 s to 131.0 s and Tactics from +12.55% to +13.19% to-hit.
6. **Luck of the Gambler: no lone pieces.** Before: Combat Jumping and Combat Training: Defensive each held one Luck of the Gambler piece. After: Combat Jumping holds three plus Kismet: Accuracy +6%, and Combat Training: Defensive two plus Shield Wall's unique. Every Luck of the Gambler toggle carries the Defense/Endurance piece, most also Defense/Endurance/Recharge.
   The two-piece regeneration bonus counts five times (Combat Training: Defensive, Combat Jumping, Maneuvers, Tactical Training: Maneuvers, Weave) and the three-piece hit-point bonus four times, inside the rule of five.
7. **Uniques where the slot level makes them count.** The two +3% defense globals sit in Crab Spider Armor Upgrade at levels 1 and 3. Tough carries Unbreakable Guard four pieces (+Max HP among them, and the four-piece melee defense bonus), Aegis: Psionic/Status Resistance and Impervium Armor: Psionic Resistance. See the unique placement table below.
8. **Survivability pieces in Health.** Before: Numina's Convalesence, Miracle and Panacea. After: Preventive Medicine: Chance for +Absorb and Regenerative Tissue: +Regeneration added, all five placed at levels 1 to 17.
9. **Quality-of-life powers at the level they open.** Mental Training moved from 47 to 22, where it opens. Every later pick has its reason in "Pick order".
10. **Rarer sets measured.** The only common or uncommon set in the build is Steadfast Protection, used for its unique +3% defense piece alone; no rarer set offers that piece. Frenzy's two accuracy pieces were measured three ways: Obliteration (kept), Armageddon and Superior Avalanche. See "Measured alternatives".
11. **Endurance is not bought with bonuses or procs.** The 13 November endurance pass was reversed. Arm Lash has its four damage procs back, Longfang its sixth piece and Fortification its fifth. The Luck of the Gambler endurance pieces stay as the sanctioned cut. Frenzy carries one proc instead of four, but the slots went to survivability (Health, Tough's psionic uniques, the Luck of the Gambler pieces), which rank above damage, not to endurance.

### Unique placement

The lowest exemplar level is arithmetic: the highest of power pick level − 5, slot level − 5 and the set's minimum level − 3 (the exemplar rules in `docs/style-guides/build-rules/exemplar-10.md`; Mids itself does not apply the −3 rule).

| Unique | Power | Slot level | Lowest exemplar level at which it works |
| --- | --- | --- | --- |
| Steadfast Protection: Resistance/+Def 3% | Crab Spider Armor Upgrade | 1 (A) | 7 |
| Gladiator's Armor: TP Protection +3% Def (All) | Crab Spider Armor Upgrade | 3 | 7 |
| Shield Wall: +Res (Teleportation), +5% Res (All) | Combat Training: Defensive | 9 | 7 |
| Kismet: Accuracy +6% | Combat Jumping | 7 | 7 |
| Luck of the Gambler: Defense/Increased Global Recharge Speed | Combat Training: Defensive, Combat Jumping, Maneuvers, Tactical Training: Maneuvers (A slots at 4, 6, 8, 10); Weave (A at 44) | 4 to 10; 44 | 22; 39 for Weave's |
| Numina's Convalesence: +Regeneration/+Recovery | Health | 1 (A) | 27 |
| Preventive Medicine: Chance for +Absorb | Health | 9 | 17 |
| Panacea: +Hit Points/Endurance | Health | 13 | 8 |
| Regenerative Tissue: +Regeneration | Health | 15 | 10 |
| Miracle: +Recovery | Health | 17 | 17 |
| Performance Shifter: Chance for +End | Stamina | 1 (A) | 18 |
| Unbreakable Guard: +Max HP | Tough | 42 | 37 |
| Aegis: Psionic/Status Resistance | Tough | 43 | 38 |
| Impervium Armor: Psionic Resistance | Tough | 50 | 45 |

### `validate --slotting` findings

`CohEngine.Cli.exe validate <file>.mbd --mids-dir tests/fixtures/mids --slotting`, from the built tool in the job's scratch area:

- **Before (the build of 13 November):** 1 warning, 3 info.
  - P-POWER-001 warning: Mental Training opens at 22 but was picked at 47.
  - P-IO-003 info: no Preventive Medicine: Chance for +Absorb or Regenerative Tissue: +Regeneration.
  - P-SET-003 info, twice: Obliteration and Scirocco's Dervish in Frenzy while Avalanche is unused.
- **After (this build):** no warnings, 1 info.
  - P-SET-003 info: Obliteration fills Frenzy while Avalanche is not slotted anywhere. Measured: the Avalanche set the check means is the one in Arm Lash. Superior Avalanche's accuracy pieces in Frenzy are refused as duplicates of Arm Lash's (`H-ENH-004`, unique piece) and as the same piece as the regular Avalanche version (`H-ENH-005`). Armageddon's pieces cost 2.25 points of smashing and lethal resistance against Obliteration's; see "Measured alternatives".
- `python -m coh_engine validate` on the delivered file: no violations, exit 0.

### Measured alternatives

| Change measured | Melee / Ranged / AoE defense | Smashing/Lethal, Psionic resistance | Lowest hit chance | Group chain (arithmetic) | Decision |
| --- | --- | --- | --- | --- | --- |
| Frenzy: Obliteration accuracy pieces, Kismet in Combat Jumping, one proc in Frenzy, Impervium in Tough (kept) | 48.48 / 52.75 / 50.35% | 68.51%, 22.41% | 98.7% (Frenzy) | 966.5 DPS, bar 55 s | kept |
| The same with two Frenzy procs and no Impervium | 48.48 / 52.75 / 50.35% | 68.51%, 16.41% | 98.7% | 1,001.3 DPS, bar 55 s | psionic resistance ranks above damage |
| Armageddon accuracy pieces in Frenzy, no Kismet, three Frenzy procs | 48.48 / 52.75 / 50.35% | 66.26%, 16.41% | 100.2% | 1,016.3 DPS, bar 52 s | 2.25 points of smashing and lethal resistance lower |
| Obliteration accuracy pieces, no Kismet, three Frenzy procs | 48.48 / 52.75 / 50.35% | 68.51%, 16.41% | **91.7%** (Frenzy) | 1,036.2 DPS, bar 55 s | fails the 95% hit chance |
| Weave reduced to two pieces instead of a Frenzy proc (measured with Armageddon accuracy pieces and no Kismet) | 47.86 / 52.13 / 49.74% | 66.26%, 16.41% | 100.2% | | defense ranks first |
| Aegis dropped instead of a Frenzy proc (same measurement copy as the row above) | 48.48 / 52.75 / 50.35% | 66.26%, 11.41% | 100.2% | | resistance ranks above damage |
| Serum on three Preventive Medicine pieces | | | | | Serum recharges in 131.4 s: no longer permanent, so it keeps four |

## What the totals count

- **Counted as always on:** every toggle and auto power on the fourth bar and above, Health, Stamina, Hasten and Serum. Serum's +637 hit points last 120 s and Serum recharges in 119.8 s.
- **Not counted:** Aim, Boxing, Barrier Core Epiphany and Assault Radial Embodiment (off in the file).
- **Damage procs are counted, weighted by their chance.** Mids' `DamagePerActivation` includes each damage proc multiplied by its chance; see "Do the procs earn the style its place?".
- **One proc is counted that should not be.** Mids counts Gaussian's Synchronized Fire-Control: Chance for Build Up in Tactical Training: Leadership as always on: +40% to-hit and +80% damage. The file's `BuffToHit` (74.9%) and `BuffDam` (123.5%) are therefore too high.
  A measurement copy without that one piece gives the standing values: **+34.9% to-hit** (Tactical Training: Leadership 15.75%, Tactics 13.19%, Kismet 6%) and **+43.5% damage**. Every damage, chain and hit-chance figure below comes from that measurement copy.
  Defense, resistance, hit points and recharge come from the build itself: the measurement copy loses the six-piece Gaussian's bonus (2.5% defense in every position), which the real build has.
- **The harness exit code.** The Mids dump harness exits E14 on every run here only because its built-in exemplar build is absent from a one-build folder (`E21: exemplar-variant source build not found`). The build's own dump is complete.

## Targets and achieved numbers

Targets, in the operator's priority order: every positional defense at or above 45% with margin; resistances as high as the build allows under the 85% cap; hit points, regeneration and survivability pieces; 95% hit chance; endurance; then damage and recharge.

**Resistance cap: 85%.** The Mids fixture `tests/fixtures/mids/archetypes.json` gives `"ResCap": 0.85` for `Class_Arachnos_Soldier` (and `"HPCap": 2409.518`). `strategy/breakpoints.json` lists 75% for this archetype; both data sources say 85%, and that file was not changed.

| Target | Goal | Achieved | Met |
| --- | --- | --- | --- |
| Melee defense | ≥ 45% | **48.48%** | yes, 3.48 points of margin |
| Ranged defense | ≥ 45% | **52.75%** | yes |
| AoE defense | ≥ 45% | **50.35%** | yes |
| Typed defense | report | Smashing/Lethal 43.17%, Fire/Cold 51.60%, Energy/Negative 44.10%, Toxic/Psionic 37.85% | noted |
| Smashing/Lethal resistance | as high as the build allows (cap 85%) | 68.51% | noted |
| Fire/Cold, Energy/Negative resistance | as high as the build allows | 68.96%, 50.21% | noted |
| Toxic / Psionic resistance | as high as the build allows | 51.71% / 22.41% | noted (see Trade-offs) |
| Hit points | report | 1,952.9 (81% of the 2,409.5 cap), Serum included | noted |
| Regeneration | report | Mids `HPRegen` 1.51 (+151%) | noted |
| Survivability pieces | all five Health uniques | Numina's Convalesence, Miracle, Panacea, Preventive Medicine, Regenerative Tissue | yes |
| Hit chance vs +4 foes with +25% defense | ≥ 95% on every attack | lowest is Frenzy at 98.7% before the cap; every attack at 95% | yes |
| Endurance, toggles only | positive | recovery 3.03 end/s against toggles 1.44 end/s: +1.59 end/s | yes |
| Endurance, chains | report the bar (worst case, chain fired without a pause) | single target 101 s, group 55 s (arithmetic) | noted |
| Global recharge | report | +147.5% (Hasten's +70% included) | fact |
| Hasten | report | 131.0 s against its 120 s duration: about 92% uptime (arithmetic) | fact |
| Team auras | all kept | both Maneuvers, both Assaults, Tactical Training: Leadership, Tactics | yes |
| Mez protection | from the build | Fortification grants hold, stun, sleep and immobilize protection while on | yes |

Recovery per second is Mids' formula from `Statistics.cs`: (1 + `EndRec`) × 1.05 × 1.6667 × (1 + `EndMax` / 100) = 1.67 × 1.75 × 1.036 = 3.03 end/s. `EndRec` (0.67), `EndMax` (3.6) and `EndUse` (1.4394) are the cited totals; the rest is arithmetic on them. The chain planner reports the same 3.03 end/s on the measurement copy.

### Hit chance

The check is min(95%, accuracy multiplier × (75% + to-hit − 25%) × 0.65), with the standing to-hit of +34.9%. The 0.65 level factor for a +4 foe is approximate, as in the earlier builds of this series.
The accuracy multiplier is Mids' enhanced `Accuracy` divided by (0.75 + to-hit).

| Key | Attack | Accuracy multiplier | Hit chance before the cap | Recharge (s) | End cost |
| --- | --- | --- | --- | --- | --- |
| 1 | Slice | 2.205 | 121.8% | 1.73 | 2.64 |
| 2 | Longfang | 2.354 | 130.0% | 2.91 | 4.32 |
| 3 | Arm Lash | 1.953 | 107.8% | 3.69 | 5.97 |
| 4 | Frenzy | 1.787 | 98.7% | 6.96 | 11.15 |
| 5 | Frag Grenade | 2.205 | 121.8% | 4.65 | 7.70 |
| 6 | Suppression | 2.354 | 130.0% | 5.00 | 7.66 |
| ALT+1 | Venom Grenade | 1.986 | 109.7% | 7.11 | 6.56 |
| ALT+2 | Ion Core Final Judgement | 2.780 | 153.5% | 90.00 | 19.51 |

Every attack lands at the 95% cap. Kismet: Accuracy +6% is what keeps Frenzy there; without it Frenzy measured 91.7%.

## Recommended attack chains

**What the planner can and cannot do here.** `python -m coh_engine chain` reads Mids' per-activation damage, which already carries the damage procs at their PPM chance, so procs are handled.
It cannot be told which powers are on keys 1 to 6: on the full build it puts Venom Grenade (an ALT click) into both chains and Brawl into the single-target one, and it fires each attack once per cycle, so a chain that repeats Slice is outside its model.
So the six-key chains below are my arithmetic on the planner's own rules (arcanatime casts, an attack ready again cast plus recharge after it starts, area attacks hitting min(target cap, 10)), on Mids' per-activation damage, recharge and endurance from the measurement copy. The planner's own runs are given beside them as a cross-check.
Bar times are a full bar (103.6) divided by the net drain of the toggles plus the chain, with every power fired the instant it recharges: a worst case (see "Endurance against real encounter lengths"). The refill figures are recovery minus toggles over a 10 s and a 20 s pause. The scripts are `chains.py` and `summary_v3.py` in the scratch folder.

### Single hard target (arch-villain)

- **Order:** Arm Lash → Slice → Longfang → Slice, repeated.
- **Cycle:** 5.95 s, with 0.14 s waiting on Slice each cycle: effectively gapless at the build's recharge (arithmetic).
- **Damage:** **209.8 damage per second** from the six keys alone, before Aim and Venom Grenade (arithmetic).
- **Endurance:** 2.62 end/s for the chain; net −1.03 end/s with the toggles, so a full bar lasts **101 s**. A 10 s pause refills 15.9 endurance and a 20 s pause 31.8 (arithmetic).
- **Opportunity clicks:** ALT+1 Venom Grenade first (−20% resistance for 16 s; it recharges in 7.11 s, so re-press it when the debuff runs out), then CTRL+1 Aim before Arm Lash. Frenzy and Frag Grenade join the cycle whenever the arch-villain has adds.
- **Planner cross-check:** on the full build the planner's single-target pick is Arm Lash → Slice → Brawl → Venom Grenade → Frag Grenade → Longfang, cycle 9.37 s, **199.9 damage per second** with Venom's −20% and Aim counted, net −1.30 end/s, 79.5 s from a full bar. Brawl is not on a key, which is why the arithmetic above is the recommendation.

### Groups (a spawn of 10)

- **Order:** Frenzy → Arm Lash → Suppression → Frag Grenade, repeated. Every order of these four gives the same cycle.
- **Cycle:** 9.34 s, with 0.10 s of gap: effectively gapless (arithmetic).
- **Damage:** **966.5 damage per second** across the spawn from the six keys alone, before Aim and Venom Grenade (arithmetic).
- **Endurance:** 3.48 end/s for the chain; net −1.89 end/s with the toggles, so a full bar lasts **55 s**. A 10 s pause refills 15.9 endurance and a 20 s pause 31.8 (arithmetic).
- **Opportunity clicks:** ALT+2 Ion Core Final Judgement into the fresh spawn, ALT+1 Venom Grenade, CTRL+1 Aim, then jump in and start with Frenzy. Suppression's immobilize and Frag Grenade's knockdown keep the spawn inside Frenzy's 10 ft.
- **Planner cross-check (six keys only):** on the planner copy with Venom Grenade swapped out, the planner picks the same four attacks: Frenzy → Arm Lash → Suppression → Frag Grenade, cycle 9.59 s, gap 0.35 s, **902.7 damage per second** (961.3 with Aim on cooldown), net −1.84 end/s, 56.2 s from a full bar.
  That copy runs 10 points of recharge and 4% recovery short of the build, because the Ragnarok pieces leave with Venom Grenade, so its cycle is slower than the arithmetic's.
- **Planner with ALT+1 on cooldown:** Frenzy → Arm Lash → Venom Grenade → Suppression → Frag Grenade, cycle 11.09 s, **1,220.5 damage per second** with Venom's −20% and Aim counted, net −1.90 end/s, 54.6 s from a full bar.

The planner charges the casts of Aim and the other self-buff clicks against the chain's timeline and then multiplies by Aim's uptime; the arithmetic leaves both out. That is the difference between its chain-only figure and the arithmetic's.

## Endurance against real encounter lengths

The operator's own combat logs (2,972 non-farming engagements over 40 hours) give these encounter lengths:

| Measure | Median | 75th percentile | 90th percentile |
| --- | --- | --- | --- |
| A typical fight | 30 s | — | 108 s |
| A fight with a long-lived target | 103 s | 146 s | 210 s |
| The pause between fights | 31 s (at least 18 s three times in four) | — | — |

Against those, on the worst-case bar times: the group chain's **55 s** covers the median fight and runs out before the 90th-percentile fight ends. The single-target chain's **101 s** is about the median long-lived target. A median 31 s pause refills about 49 endurance and an 18 s pause about 29 (arithmetic: 1.59 end/s with the toggles on).

The same logs show a real player uses each power two to three times less often than the planner, which fires every power the instant it recharges. At half the planner's rate the group chain's net drain falls to about −0.15 end/s, and at a third it is positive: the bar would last most fights (estimate, not measured).

Caveats: the log figures come from the operator's other characters and from all content, not from this build. Hard-mode content could not be identified in the logs. The bar times assume the chain is fired without a pause, and they leave out the Performance Shifter proc, inspirations and team endurance buffs.

The 13 November version had stretched the group bar to 105 s by giving up three damage procs, Longfang's sixth piece and Fortification's fifth. Rule 11 forbids that trade, so it was reversed; the Luck of the Gambler Defense/Endurance pieces are the endurance cut this build keeps.

## Do the procs earn the style its place?

On base numbers the backpack attacks do not out-damage the ranged ones. As slotted, they do: Arm Lash at 421.3 per target is the highest per-activation figure in the build, ahead of Longfang at 333.8.

**How the engine handles procs.** The engine does not compute a proc chance itself: `src/coh_engine/assembly.py` (`_computed`) leaves any effect with a procs-per-minute value at the probability Mids stored.
Mids computes it in `Effect.cs` (`ActualProbability`):

```text
chance = PPM × (R + CastTimeReal) / (60 × (0.75 × AoEModifier + 0.25)), clamped to [0.015 × PPM + 0.05, 0.90]
R = BaseRecharge / (BaseRecharge / EnhancedRecharge − BuffHaste)       (recharge with slotted recharge only; global recharge does not lower it)
AoEModifier = 1 + 0.15 × Radius                                        (sphere)
            = 1 + 0.15 × Radius − 0.00036667 × Radius × (360 − Arc°)   (cone)
```

Applied to this build (arithmetic on the cited dump values; `BuffHaste` 1.475):

| Attack | Shape | Procs slotted | Recharge with slotted recharge only | AoEModifier | 3.5 PPM chance | 4.5 PPM chance (Armageddon) |
| --- | --- | --- | --- | --- | --- | --- |
| Arm Lash | cone, 10 ft, 30° | Armageddon, Obliteration, Scirocco's Dervish, Touch of Lady Grey | 8.12 s | 1.29 | 46.9% | 60.3% |
| Frenzy | sphere, 10 ft | Obliteration | 14.31 s | 2.50 | 45.4% | not slotted |

Checked against Mids: Arm Lash's per-activation damage carries 107.09 × 0.603 + 3 × 71.75 × 0.469 = 165.5 of proc damage, and Frenzy's 71.75 × 0.454 = 32.6. Both match Mids' `DamagePerActivation` exactly. Each proc rolls once per target hit.

The comparison is the build against a measurement copy with the same slots, the five procs swapped for damage pieces (Superior Avalanche and Armageddon in Arm Lash; Obliteration: Damage/Recharge in Frenzy):

| Measure | With procs (the build) | Without procs (measurement copy) |
| --- | --- | --- |
| Group chain, six keys (arithmetic) | **966.5 damage per second**, bar 55 s | 900.3 damage per second, bar 61 s |
| Single-target chain (arithmetic) | **209.8 damage per second**, bar 101 s | 189.9 damage per second, bar 146 s |
| Group chain with Venom on cooldown (planner) | 1,220.5, 54.6 s | 1,169.6, 55.1 s |
| Melee / Ranged / AoE defense | 48.48% / 52.75% / 50.35% | 53.48% / 52.75% / 50.35% |
| Fire/Cold resistance | 68.96% | 74.96% |

The five procs add 7% to the group chain and 10% to the single-target chain. They cost 5 points of melee defense and 6 of fire and cold resistance (the Superior Avalanche five-piece bonuses). Both versions clear every defense floor, so the build keeps the procs: damage is what this style has to earn.

## Against the ranged Crab

The ranged Crab of this series is `crab-ranged-4-star` in `characters/account1/laser-arachnos`. These figures are from its `.mbd` as it stood on 14 November 2026 at 22:22 (stamped 2026.5.1337, file hash beginning `0f11569`), dumped through the Mids dump harness on database 2026.5.1337 with the same Gaussian's proc removed; its defense and resistance are from its own `.mids-totals.json`.
Both builds are run through the planner under the same rules (every click attack eligible, Venom Grenade included): ranged stance for the ranged Crab, melee stance for this one.

| Measure | Ranged Crab | Melee Crab (this build) |
| --- | --- | --- |
| Single target (planner) | 171.7 damage per second, net −1.16 end/s, 90.6 s | **199.9**, net −1.30 end/s, 79.5 s |
| Spawn of 10 (planner) | 977.8 damage per second, net −1.06 end/s, 98.7 s | **1,220.5**, net −1.90 end/s, 54.6 s |
| Melee / Ranged / AoE defense (Mids totals) | 49.15% / 49.72% / 49.77% | 48.48% / 52.75% / 50.35% |
| Smashing/Lethal, Psionic resistance | 72.55%, 31.41% | 68.51%, 22.41% |
| Hasten | 130.1 s | 131.0 s |
| Resistance debuffs from its own powers | Venom Grenade −20% and Arctic Breath −15% | Venom Grenade −20% only |

**What the melee version gains:** 16% more single-target damage and 25% more area damage on the planner's figures, from the procs in Arm Lash and from Frenzy's ten-target point-blank area. Ranged defense is 3 points higher.

**What it loses:** it has to stand within 7 to 10 ft, where a 4-star spawn's melee hits land; Arm Lash reaches only 5 targets. It gives up Arctic Breath's −15% resistance for the team, 4 points of smashing and lethal resistance, 9 points of psionic resistance and 44 s of worst-case group bar.

## Team value

What the build gives the other seven players, as slotted (Mids values for the auras; the team receives each aura while in range):

| Source | Effect | Uptime |
| --- | --- | --- |
| Tactical Training: Maneuvers | +15.99% defense, every position and type | toggle, always on |
| Maneuvers (Leadership pool) | +5.05% defense, every position and type | toggle, always on |
| Tactical Training: Assault | +15% damage | toggle, always on |
| Assault (Leadership pool) | +15% damage | toggle, always on |
| Tactical Training: Leadership | +15.75% to-hit | toggle, always on |
| Tactics (Leadership pool) | +13.19% to-hit | toggle, always on |
| Venom Grenade (ALT+1) | −20% resistance to every damage type (−40% toxic), up to 10 foes, 16 s | recharges in 7.11 s: 100% if re-pressed when it runs out |
| Reactive Radial Flawless Interface | −2.5% resistance on hit for 8.3 s, from every attack | on every hit |
| Defense debuffs | −7.5% defense per hit: Slice (6 s), Longfang (5 s), Arm Lash, Frenzy, Suppression (10 s each) | the group chain lands Arm Lash, Frenzy and Suppression every 9.34 s, so a spawn carries up to −22.5% defense before its debuff resistance (arithmetic) |
| Control | Suppression: Magnitude 3 immobilize for 10 s on up to 10 foes; Frag Grenade: knockdown (Overwhelming Force converts its knockback) | every chain cycle |

Both Maneuvers and both Assaults are taken: +21.04% defense and +30% damage for the team, plus +28.94% to-hit from Tactical Training: Leadership and Tactics.

**What the build does not cover:** no heals for others (Serum heals only the Crab), no absorb for others, no resistance buffs, no regeneration debuff, and no aggro control: it has no taunt aura, so it does not hold a spawn for the team.

## Pick order

| Level | Pick | Opens at | Reason when later than it opens |
| --- | --- | --- | --- |
| 1 | Slice | 1 | |
| 1 | Crab Spider Armor Upgrade | 1 | |
| 2 | Longfang | 2 | |
| 4 | Combat Training: Defensive | 2 | Longfang took 2 |
| 6 | Combat Jumping | 4 | the travel power and a defense toggle; 4 went to Combat Training: Defensive |
| 8 | Maneuvers | 4 | the defense core comes first, in this order |
| 10 | Tactical Training: Maneuvers | 10 | |
| 12 | Arm Lash | 12 | |
| 14 | Aim | 6 | the exemplar rule asks for the build-up click by 15; the attacks and defense toggles came first |
| 16 | Tactical Training: Assault | 16 | |
| 18 | Frag Grenade | 18 | |
| 20 | Tactical Training: Leadership | 20 | |
| 22 | Mental Training | 22 | |
| 24 | Fortification | 24 | |
| 26 | Frenzy | 22 | Mental Training took 22 and Fortification (mez protection) 24 |
| 28 | Serum | 24 | after Fortification and Frenzy |
| 30 | Hasten | 4 | levels 4 to 28 hold the defense toggles, the attacks and the Soldier powers that open at 10 to 24 |
| 32 | Venom Grenade | 12 | an ALT debuff; the chain attacks and survival powers came first |
| 35 | Suppression | 8 | the sixth attack; the chains below 35 run on the other five |
| 38 | Boxing | 4 | prerequisite only, taken just before Tough |
| 41 | Tough | 14 | the Fighting pool is the level 50 survival layer; below 38 the armor, Fortification and the Maneuvers carry defense |
| 44 | Weave | 14 | as Tough |
| 47 | Assault | 4 | a team buff; the team already has Tactical Training: Assault from 16 |
| 49 | Tactics | 14 | a team buff; Tactical Training: Leadership covers to-hit from 20 |

## Powers by level, with slotting

Set pieces are level 50, attuned or superior. `(A)` is the free slot; numbers are the levels the added slots were placed at. "+5" marks an IO boosted with Enhancement Boosters.

- **1 Slice:** Superior Spider's Bite: Accuracy/Damage (A), Accuracy/Damage/RechargeTime (3), Damage/RechargeTime (5), Damage/Endurance/RechargeTime (23), Accuracy/Damage/Endurance/RechargeTime (39), RechargeTime/Global Toxic (46).
- **1 Crab Spider Armor Upgrade:** Steadfast Protection: Resistance/+Def 3% (A); Gladiator's Armor: TP Protection +3% Def (All) (3).
- **2 Longfang:** Superior Winter's Bite: Accuracy/Damage (A), Accuracy/Damage/Endurance (5), Damage/RechargeTime (17), Accuracy/Damage/Recharge (27), Damage/Endurance/Accuracy/RechargeTime (39), Recharge/Chance for -Speed & -Recharge (48).
- **4 Combat Training: Defensive:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A); Shield Wall: +Res (Teleportation), +5% Res (All) (9); Luck of the Gambler: Defense/Endurance (34).
- **6 Combat Jumping:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A); Kismet: Accuracy +6% (7); Luck of the Gambler: Defense/Endurance (7), Defense/Endurance/Recharge (43).
- **8 Maneuvers:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense/Endurance (13), Defense/Endurance/Recharge (34).
- **10 Tactical Training: Maneuvers:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense (11), Defense/Endurance (11), Defense/Endurance/Recharge (34), Defense/Recharge (45).
- **12 Arm Lash:** Superior Avalanche: Accuracy/Damage (A), Accuracy/Damage/Endurance/Recharge (15); Armageddon: Chance for Fire Damage (19); Obliteration: Chance for Smashing Damage (31); Scirocco's Dervish: Chance of Damage (Lethal) (39); Touch of Lady Grey: Chance for Negative Damage (48).
- **14 Aim:** Invention: Recharge Reduction +5 (A). Off in the totals.
- **16 Tactical Training: Assault:** Invention: Endurance Reduction +5 (A).
- **18 Frag Grenade:** Superior Dominion of Arachnos: Accuracy/Damage (A), Accuracy/Damage/Recharge (19), Damage/Recharge (21), Damage/Endurance/Recharge (31), Accuracy/Damage/Endurance/Recharge (40); Overwhelming Force: Damage/Chance for Knockdown/Knockback to Knockdown (48).
- **20 Tactical Training: Leadership:** Gaussian's Synchronized Fire-Control: To Hit Buff (A), To Hit Buff/Recharge (21), To Hit Buff/Recharge/Endurance (23), Recharge/Endurance (33), To Hit Buff/Endurance (40), Chance for Build Up (50).
- **22 Mental Training:** Invention: Run Speed +5 (A). Taken for its +20% recharge and slow resistance.
- **24 Fortification:** Gladiator's Armor: Resistance (A), Resistance/Rech/End (25), End/Resist (25), Recharge/Resist (37), Recharge/Endurance (46).
- **26 Frenzy:** Obliteration: Accuracy/Damage/Recharge (A), Accuracy/Damage/Endurance/Recharge (27), Chance for Smashing Damage (37).
- **28 Serum:** Preventive Medicine: Heal (A), Heal/Endurance (29), Endurance/RechargeTime (29), Heal/RechargeTime (43).
- **30 Hasten:** Invention: Recharge Reduction +5 (A), (31).
- **32 Venom Grenade:** Ragnarok: Damage/Recharge/Accuracy (A), Recharge/Accuracy (33), Damage/Recharge (33), Damage/Endurance (37), Damage (46).
- **35 Suppression:** Superior Frozen Blast: Accuracy/Damage (A), Accuracy/Damage/Endurance (36), Accuracy/Damage/Recharge (36), Damage/Endurance/Accuracy/RechargeTime (36), Damage/Endurance (40), Recharge/Chance for Immobilize (50).
- **38 Boxing:** empty (A). Prerequisite for Tough and Weave, never pressed; off in the totals.
- **41 Tough:** Unbreakable Guard: Resistance (A), Resistance/Endurance (42), RechargeTime/Resistance (42), +Max HP (42); Aegis: Psionic/Status Resistance (43); Impervium Armor: Psionic Resistance (50).
- **44 Weave:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense/Endurance (45), Defense/Endurance/Recharge (45).
- **47 Assault:** Invention: Endurance Reduction +5 (A).
- **49 Tactics:** Invention: To Hit Buff +5 (A).
- **Health:** Numina's Convalesence: +Regeneration/+Recovery (A); Preventive Medicine: Chance for +Absorb (9); Panacea: +Hit Points/Endurance (13); Regenerative Tissue: +Regeneration (15); Miracle: +Recovery (17).
- **Stamina:** Performance Shifter: Chance for +End (A). The proc is a lone piece by design: Stamina's added slots would hold only Endurance Modification, which ranks below every use the 67 slots have here.

Slots: 67 added, 24 picks, 4 pools. `python -m coh_engine validate` reports no violations.
Exemplar 10: Slice and Longfang are the two attacks, Combat Jumping (level 6) is the travel power and a defense toggle, Tactical Training: Maneuvers holds two added slots by level 11, Combat Jumping two by 7, Slice two by level 5, and Aim, the build-up click, is picked at 14.

**Lone set pieces.** No Luck of the Gambler piece is alone. The single pieces of a set are uniques or procs, which work alone: Steadfast Protection, Gladiator's Armor, Shield Wall, Kismet, the five Health uniques, Performance Shifter's proc, Aegis, Impervium Armor, Overwhelming Force's knockdown piece, and the damage procs in Arm Lash.

### Verbatim MidsReborn export (power lines)

The Mids export does not print the +5 boosts; they are in the `.mbd` as `RelativeLevel: PlusFive`.

```text
Level 1:	Slice				SprSpdBit-Acc/Dmg(A), SprSpdBit-Acc/Dmg/Rchg(3), SprSpdBit-Dmg/Rchg(5), SprSpdBit-Dmg/EndRdx/Rchg(23), SprSpdBit-Acc/Dmg/EndRdx/Rchg(39), SprSpdBit-Rchg/Global Toxic(46)
Level 1:	Crab Spider Armor Upgrade		StdPrt-ResDam/Def+(A), GldArm-3defTpProc(3)
Level 2:	Longfang			SprWntBit-Acc/Dmg(A), SprWntBit-Acc/Dmg/EndRdx(5), SprWntBit-Dmg/Rchg(17), SprWntBit-Acc/Dmg/Rchg(27), SprWntBit-Dmg/EndRdx/Acc/Rchg(39), SprWntBit-Rchg/SlowProc(48)
Level 4:	Combat Training: Defensive		LucoftheG-Def/Rchg+(A), ShlWal-ResDam/Re TP(9), LucoftheG-Def/EndRdx(34)
Level 6:	Combat Jumping			LucoftheG-Def/Rchg+(A), Ksm-ToHit+(7), LucoftheG-Def/EndRdx(7), LucoftheG-Def/EndRdx/Rchg(43)
Level 8:	Maneuvers			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(13), LucoftheG-Def/EndRdx/Rchg(34)
Level 10:	Tactical Training: Maneuvers		LucoftheG-Def/Rchg+(A), LucoftheG-Def(11), LucoftheG-Def/EndRdx(11), LucoftheG-Def/EndRdx/Rchg(34), LucoftheG-Def/Rchg(45)
Level 12:	Arm Lash			SprAvl-Acc/Dmg(A), SprAvl-Acc/Dmg/EndRdx/Rchg(15), Arm-Dam%(19), Obl-%Dam(31), ScrDrv-Dam%(39), TchofLadG-%Dam(48)
Level 14:	Aim				RechRdx-I(A)
Level 16:	Tactical Training: Assault		EndRdx-I(A)
Level 18:	Frag Grenade			SprDmnofA-Acc/Dmg(A), SprDmnofA-Acc/Dmg/Rchg(19), SprDmnofA-Dmg/Rchg(21), SprDmnofA-Dmg/EndRdx/Rchg(31), SprDmnofA-Acc/Dmg/EndRdx/Rchg(40), OvrFrc-Dam/KB(48)
Level 20:	Tactical Training: Leadership		GssSynFr--ToHit(A), GssSynFr--ToHit/Rchg(21), GssSynFr--ToHit/Rchg/EndRdx(23), GssSynFr--Rchg/EndRdx(33), GssSynFr--ToHit/EndRdx(40), GssSynFr--Build%(50)
Level 22:	Mental Training			Run-I(A)
Level 24:	Fortification			GldArm-ResDam(A), GldArm-Res/Rech/End(25), GldArm-End/Res(25), GldArm-RechRes(37), GldArm-RechEnd(46)
Level 26:	Frenzy				Obl-Acc/Dmg/Rchg(A), Obl-Acc/Dmg/EndRdx/Rchg(27), Obl-%Dam(37)
Level 28:	Serum				Prv-Heal(A), Prv-Heal/EndRdx(29), Prv-EndRdx/Rchg(29), Prv-Heal/Rchg(43)
Level 30:	Hasten				RechRdx-I(A), RechRdx-I(31)
Level 32:	Venom Grenade			Rgn-Acc/Dmg/Rchg(A), Rgn-Acc/Rchg(33), Rgn-Dmg/Rchg(33), Rgn-Dmg/EndRdx(37), Rgn-Dmg(46)
Level 35:	Suppression			SprFrzBls-Acc/Dmg(A), SprFrzBls-Acc/Dmg/EndRdx(36), SprFrzBls-Acc/Dmg/Rchg(36), SprFrzBls-Dmg/EndRdx/Acc/Rchg(36), SprFrzBls-Dmg/EndRdx(40), SprFrzBls-Rchg/ImmobProc(50)
Level 38:	Boxing				Empty(A)
Level 41:	Tough				UnbGrd-ResDam(A), UnbGrd-ResDam/EndRdx(42), UnbGrd-Rchg/ResDam(42), UnbGrd-Max HP%(42), Ags-Psi/Status(43), ImpArm-ResPsi(50)
Level 44:	Weave				LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(45), LucoftheG-Def/EndRdx/Rchg(45)
Level 47:	Assault				EndRdx-I(A)
Level 49:	Tactics				ToHit-I(A)
Level 1:	Health				NmnCnv-Regen/Rcvry+(A), Prv-Absorb%(9), Pnc-Heal/+End(13), RgnTss-Regen+(15), Mrc-Rcvry+(17)
Level 1:	Stamina				PrfShf-End%(A)
```

## Incarnates (all tier 4; Lore left empty, as 4-star bans pets)

- **Alpha: Cardiac Core Paragon.** Measured against Agility Core and Musculature Core on the same slotting (defense, resistance and Hasten from each build's totals; chains are arithmetic on each build's measurement copy, six keys only, worst-case bar from full with the toggles on):

  | Alpha | Melee / Ranged / AoE defense | Smashing/Lethal resistance | Hasten | Single-target chain | Group chain |
  | --- | --- | --- | --- | --- | --- |
  | Cardiac Core (taken) | 48.48% / 52.75% / 50.35% | 68.51% | 131.0 s | 209.8 DPS, bar 101 s | 966.5 DPS, bar 55 s |
  | Agility Core | 52.01% / 57.78% / 53.88% | 63.39% | 122.6 s | 208.8 DPS, bar 47 s | 955.3 DPS, bar 31 s |
  | Musculature Core | 48.48% / 52.75% / 50.35% | 63.39% | 131.0 s | 230.8 DPS, bar 44 s | 1,071.2 DPS, bar 30 s |

  Cardiac gives 5 more points of smashing and lethal resistance than either and nearly doubles both bars. Agility's 3.5 to 5 points of defense do not pay for halving the bars, and Musculature's 11% more damage costs the same.
- **Judgement: Ion Core Final Judgement** (ALT+2): 660.8 per activation from the measurement copy; a chaining area attack that also reaches foes outside melee.
- **Interface: Reactive Radial Flawless Interface.** The VEAT handbook names Reactive and Degenerative Radial as the two to take. Reactive's −resistance stacks with Venom Grenade.
- **Destiny: Barrier Core Epiphany** (CTRL+4). Off in the totals.
  An experienced hard-mode player reports this practice: Barrier is the best all-round Destiny for hard mode. On an eight-player team six take Barrier and two float (Clarion, Rebirth or Ageless); with six Barriers the rotation is two at once every 30 seconds.
  One or two stars up to level 54 can manage with about four Barrier holders; three and four stars at level 55 bring large increases in enemy accuracy, damage, hit points, regeneration and defense. This build takes Barrier to be one of the six.
- **Hybrid: Assault Radial Embodiment** (CTRL+3). The petless choice in the VEAT handbook. It is a toggle on for part of each cycle, so it is off in the totals.

## Pets: left out

Summon Spiderlings and Call Reinforcements are not taken, and Lore pets are banned in 4-star play. The petless guide reports that the pets "get thrashed at +4".

## Trade-offs

- **Frenzy carries one proc, not four.** The slots went to survivability under the operator's priority order: Preventive Medicine and Regenerative Tissue in Health, Impervium Armor in Tough (+6% psionic resistance), and the Luck of the Gambler pieces in Combat Jumping and Combat Training: Defensive. The cost is 3.5% of the group chain against a two-proc Frenzy with Impervium out (966.5 against 1,001.3, the second row of "Measured alternatives").
- **The procs cost defense.** See "Do the procs earn the style its place?": the five procs add 7% to the group chain against 5 points of melee defense and 6 of fire and cold resistance.
- **The worst-case group bar is 55 s.** Endurance is fifth in the priority order, and rule 11 forbids buying it with set bonuses, resistance or procs. The planner's bar fires every power on cooldown; the operator's logs suggest a real bar two to three times longer.
- **Hasten recharges in 131.0 s against 120 s**, about 92% uptime (arithmetic). Permanent Hasten is not a target.
- **Psionic resistance 22.41%.** Aegis and Impervium Armor in Tough lift it from 11.41%. Frag Grenade's sixth piece is Overwhelming Force (knockback to knockdown) instead of Superior Dominion of Arachnos' sixth, so the six-piece toxic and psionic resistance bonus (6%) is not taken: a melee Crab whose Frag Grenade scatters the spawn out of Frenzy's 10 ft loses more than the 6% is worth. Barrier covers psionic damage in hard mode.
- **Melee defense 48.48%, 3.48 points over the floor.** Every −defense hit takes melee under the soft cap first, and this build stands in melee. Defense-debuff resistance is not available in the six Soldier sets.
- **Boxing is a dead pick.** It is the Fighting pool prerequisite. Tough and Weave add smashing and lethal resistance, 7.2% defense in every position (Weave, enhanced), a fifth Luck of the Gambler recharge piece and Tough's three resistance uniques.
- **Omega Maneuver not taken.** A measurement copy with Omega Maneuver in place of Tactics gave 350.5 per target per activation (one Ragnarok piece) and a 90.9 s recharge on database 2026.1.1242: about 38.5 damage per second on a spawn of 10, or 4% of the group chain (arithmetic). It would cost Tactics (+13.19% to-hit for the team). The Omega Maneuver discussion thread also reports the delay makes it go off after the team has cleared the spawn.
- **No ancillary pool.** The six attacks are the Crab's own; an epic attack would be a seventh attack or an ALT click that costs a pick the team auras use.
- **No Combat Training: Offensive, Stealth or Wide Area Web Grenade.** Stealth was tried (database 2026.1.1242): Mids counts its out-of-combat +10% defense as always on, which would overstate defense by about 9 points in combat. Wide Area Web Grenade needs a pick.

## 4-star play

- Open from range with Longfang while the tank gathers the spawn, then ALT+2 Ion Core Final Judgement and ALT+1 Venom Grenade.
- CTRL+1 Aim, jump in, Frenzy → Arm Lash → Suppression → Frag Grenade. Suppression's immobilize holds the spawn inside Frenzy's 10 ft.
- On an arch-villain: Venom Grenade, Aim, then Arm Lash → Slice → Longfang → Slice. Re-press Venom Grenade every 16 s.
- Keep every toggle on: the team gets two Maneuvers, two Assaults, Tactical Training: Leadership and Tactics.
- CTRL+2 Serum before the pull: +637 HP for 120 s, recharging in 119.8 s.
- Watch the blue bar on long fights: 55 s of non-stop group chain from full at worst; a 30 s pause brings back about 48.
- Fortification is the mez protection. Do not let it drop under endurance drain.
- Stagger Barrier with the team's other Barrier holders.

## What I would verify

- **The Gaussian's proc in Mids' totals.** The standing to-hit (+34.9%) and damage (+43.5%) come from a measurement copy without that piece. Check the to-hit in game with the combat attributes window.
- **The 0.65 level factor** in the hit-chance check is approximate. Frenzy's margin is 3.7 points.
- **Proc chances in game.** The 46.9% and 45.4% chances follow Mids' formula; check with the combat log over a few hundred Arm Lash and Frenzy hits.
- **The Reactive Interface damage in Mids' per-activation figures.** Mids counts the interface's damage over time (66.9 per target) in every attack at full strength, while the dump stores a 75% chance for it. If the chance applies, every attack's figure is about 17 per target high, in this build and in the ranged one alike.
- **Team values of the auras.** The table uses the Mids self values; check that teammates in range receive the same defense, damage and to-hit.
- **The unique placement table** uses the exemplar rules' set minimum level − 3; Mids does not model it. Check the Health uniques at exemplar 20 in game.
- **Bar times in hard-mode fights.** The 55 s group bar is a worst case on a non-stop chain; check it against a combat log from a 4-star run.

## Guides used (paraphrased, short quotes only)

Under `homecoming-source-material/community/archetypes/`:

- `arachnos/the-spider-s-handbook-an-incomplete-guide-to-veats.md` — "The Spider's Handbook: An Incomplete Guide to VEATs." Arm Lash "procs well" and belongs in a Crab's chain at 50; Leadership double stacks; Reactive or Degenerative Radial Interface; Assault Radial Hybrid for a petless build.
- `arachnos/my-2-crab-builds-tank-and-proc-monster.md` — "My 2 crab builds : tank and proc Monster." Finds the Crab's damage low and raises it with damage procs in the backpack attacks.
- `arachnos/my-melee-crab.md` — "My \"melee\" Crab." A posted melee Crab with an Obliteration proc in Arm Lash and Superior Avalanche in Frenzy.
- `arachnos/my-crab-build-petless-but-well-rounded.md` — "My Crab build: petless, but well-rounded." Petless at +4 and perma-Serum.
- `arachnos/crab-discussion-omega-maneuver-worth-it.md` — "Crab discussion: Omega Maneuver worth it?" Omega Maneuver is too slow on a team.
- `tanker/inv-rad-hardmode-version-i-hope.md` — "Inv/Rad: Hardmode version (I hope?)." Hard-mode foes are +4 with about +25% defense, so accuracy matters.

## Files

Sibling files in this folder, all named "4-star" plus an ending:

- `.mbd`
- `.md`
- `.mids-totals.json`

No Sidekick file was made for this build.
