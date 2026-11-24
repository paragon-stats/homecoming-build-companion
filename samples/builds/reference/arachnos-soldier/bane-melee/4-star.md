# Bane Spider Soldier-Bane Spider Training Arachnos Soldier - Melee - 4-Star

**This is the level 50 layout. It cannot be followed before level 24.** An Arachnos Soldier picks only from the base Arachnos Soldier and Training and Gadgets sets until the forced respec at 24; the Bane Spider powers listed here at levels 1, 6, 12 and 18 are placed there by that respec. This document is not a leveling plan.

Level 50 Arachnos Soldier, Bane Spider branch: Bane Spider Soldier / Bane Spider Training, with the Leaping, Speed, Leadership and Fighting pools and no epic pool. It is a **melee Bane**: the mace attacks (Bash, Pulverize, Shatter, Crowd Control) plus the Wolf Spider's Bayonet on the main row, with Placate on key 2 to set up a critical Shatter on key 3, and Venom Grenade and Surveillance as opportunity clicks. Its team job is the double resistance debuff on a hard target (Surveillance and Venom Grenade, −40% together) and the full Leadership stack; its personal job is single-target damage in melee.
Loadable file: `4-star.mbd`. Every number is from MidsReborn's totals (`totals.json`, `TotalsCapped`, copied here as `.mids-totals.json`) or from the repository's chain planner (`coh_engine chain`) unless it is marked as arithmetic.

**Database stamp.** The `.mbd` is stamped with Mids database version **2026.5.1337**, the operator's installed Homecoming database. Every total here comes from the Mids dump harness run on a copy of that database.

This version is reworked to the operator's slotting rules (see "Slotting rules applied"). The previous version is called "before" where the two are compared; its figures were re-measured on the same 2026.5.1337 database.

## Key map

| Row | Key | Power | What it is for |
| --- | --- | --- | --- |
| Main | 1 | Bash | Filler that starts each cycle; 10% chance to stun |
| Main | 2 | Placate | Setup: hides you so the next attack is critical, and drops up to 5 foes' attention on you; recharges in 11.18 s |
| Main | 3 | Shatter | The pair hit: gains most from the critical (+306.7 against Pulverize's +220.6); recharges in 3.33 s |
| Main | 4 | Pulverize | Backup pair hit when Shatter is still recharging; 20% chance to stun; recharges in 2.41 s |
| Main | 5 | Bayonet | Third single-target hit (Wolf Spider attack, kept after the branch) |
| Main | 6 | Crowd Control | Melee cone, up to 10 foes, knockdown; the pair hit on groups (2 → 6) |
| Opportunity | ALT+1 | Surveillance | −20% resistance and −15% defense on one foe for 20 s; recharges in 14.38 s |
| Opportunity | ALT+2 | Venom Grenade | −20% resistance on up to 10 foes for 16 s, plus toxic damage; recharges in 6.86 s |
| Opportunity | ALT+3 | Ion Core Final Judgement | Nuke on a fresh spawn |
| Opportunity | ALT+4 to ALT+6 | (empty) | Free |
| Self-buffs | CTRL | Build Up | +80% damage, +20% to-hit for 10 s; recharges in 25.29 s |
| Self-buffs | CTRL | Assault Radial Embodiment | Hybrid toggle, on for part of each cycle |
| Self-buffs | CTRL | Barrier Core Epiphany | Destiny, on the team's Barrier rotation |
| Auto-fire | 0 | Hasten | +70% recharge; recharges in 126.44 s against its 120 s duration |
| Toggle bar (no keys) | — | Tactical Training: Maneuvers, Assault and Leadership; Maneuvers, Assault and Tactics (Leadership pool); Tough; Weave; Cloaking Device; Combat Jumping (also the travel power) | Run all the time |

**Not on a key.** Boxing: prerequisite, not on a key (it unlocks Tough and Weave). Auto powers: Bane Spider Armor Upgrade, Combat Training: Defensive, Mental Training.

**The setup-and-hit pair.** Placate sits on key 2 and the hit it sets up on key 3, so the pair is a fast 2-3 when both are up; key 4 is the backup pair hit.

- **Key 3 is Shatter.** A critical repeats the base hit, so the bigger base hit gains more. The critical adds 306.7 on Shatter, 220.6 on Pulverize, 193.7 on Bayonet and 134.8 on Bash (Mids enhanced base hits, measurement copy).
- **Do the recharges match? Yes.** Placate recharges in 11.18 s, Shatter in 3.33 s and Pulverize in 2.41 s. The single-target cycle is 14.31 s with one Shatter, so Shatter has long finished recharging whenever Placate is up. Placate's own 1.06 s animation also covers part of Shatter's recharge.
- **When key 4 is used instead.** Only when Shatter was fired less than about 2.3 s (3.33 − 1.06) before Placate comes up. That happens after an extra Shatter outside the chain, or after a Cloaking Device opener (below). Pulverize is always ready then. Pairing Placate with Pulverize every cycle would give 147.6 damage per second instead of 153.6 (arithmetic).

**Why Venom Grenade is on ALT.** With Placate on the main row, one of six attacks had to leave it. Venom Grenade does the least damage per activation (199.3) but carries the team's area resistance debuff. Its 16 s debuff outlasts its 6.86 s recharge, so it works as an opportunity click pressed once per cycle. Surveillance stays on ALT: one target, a 20 s debuff, pressed whenever it is up.

Primary plus ALT rows hold 9 click powers (limit 12): five attacks and Placate on the main row, plus Venom Grenade, Surveillance and the Judgement on ALT.

## What the totals count

- **Counted as always on:** every toggle and auto power on the toggle bar, Health, Stamina and Hasten.
- **Not counted:** Build Up, Placate, Barrier Core Epiphany and Assault Radial Embodiment (all set to off in the file), and the damage procs.
- **Cloaking Device is counted in full and should not be, in combat.** Mids gives it 9.89% defense (slotted) to melee, ranged, AoE and every type except toxic and psionic. The canonical power (`data/canonical/powers/training_gadgets/bane_spider_training/hide.json`) grants that defense as two equal effects. One of them is suppressed for 10 s after the wearer attacks (`suppress_events` `AttackedOther`, `post_event_duration` 10), so an attacker keeps half: 4.945 points. Every "in combat" defense figure below is the Mids total minus 4.945 (arithmetic).
- **One proc is counted that should not be.** Mids counts the Gaussian's Synchronized Fire-Control: Chance for Build Up proc in Tactical Training: Leadership as always on (+40% to-hit, +80% damage). To-hit, damage and the chains come from a measurement copy without that one piece: standing to-hit **+34.94%** (Tactical Training: Leadership 15.75%, Tactics 13.19%, Kismet 6%) and damage **+40.5%**. Defense, resistance, hit points, recharge and endurance are from the real build.
- **Mids counts the hidden critical hit as always on.** See "Critical hits": every mace attack's `DamagePerActivation` includes the critical repeat, and the chain planner reads that figure.
- **+5 boosts.** The 11 plain IOs (two Recharge in Hasten, two in Build Up, Accuracy and Recharge in Surveillance, Accuracy in Boxing, Endurance Reduction in both Assaults, Run Speed in Mental Training, To Hit Buff in Tactics) carry `RelativeLevel` `PlusFive` in the `.mbd`. The harness dump (`slots.json`) reads all 11 as `PlusFive`.
- **The harness exit.** The Mids dump harness ends with E14 only because its built-in exemplar build is not in a one-build folder; the build itself loads and dumps.

## Critical hits: how the rule works and how often it lands

From canonical data (`data/canonical/powers/arachnos_soldiers/bane_spider_soldier/*.json` and `arachnos_soldier/bayonet.json`):

- **What a critical is.** Bash, Pulverize, Shatter and Bayonet carry a second copy of their base hit, gated on `source>kMeter > 0` with chance 1.0. A critical doubles the direct hit, not the toxic damage over time. Crowd Control's copy has chance 0.5 per target. The repeat has the same scale as the base hit (Shatter 2.28, Pulverize 1.64, Bayonet 1.44, Bash 1.0, Crowd Control 1.61).
- **What sets the Meter.** Only two Soldier powers: Placate (Meter for 10 s, plus a hidden 5 s copy) and Cloaking Device (Meter while toggled, refreshed every 0.5 s). The other `kMeter` users in the data are other archetypes' inherents.
- **Does attacking clear it? Yes.**
  - Placate's Meter carries `cancel_events` `AttackedOther`, `Healed` and `Moved`, so the first attack after Placate consumes it.
  - Cloaking Device's Meter carries `suppress_events` `AttackedOther`, `Healed` and `Moved` with a 10 s `post_event_duration`, so after any attack it is off for 10 s.
  - The Stalker's Hide (`stalker_defense/ninjitsu/hide.json`) uses the same structure with 8 s.
- **What that means for a chain.** Placate gives **one critical per Placate**. Cloaking Device gives one critical on the opening hit, and only after 10 s without attacking, being healed or being moved. In a running fight every attack comes within 10 s of the last, so Cloaking Device never contributes after the opener. Placate recharges in **11.18 s** here, so the sustained rate is one critical per single-target cycle: **1 of 6 attacks**, one every 14.31 s.
- **Mids disagrees with canonical on the size.** Mids' database stores the repeat as two thirds of the base hit (Shatter 1.52 against 2.28), and Mids counts it on every activation. The arithmetic uses the canonical size and the Placate rate.

| Attack | Mids damage per activation (always critical, Mids size) | Without a critical (Mids figure minus its critical part) | Canonical critical bonus |
| --- | --- | --- | --- |
| Shatter | 645.3 | 440.9 | +306.7 |
| Pulverize | 501.8 | 354.7 | +220.6 |
| Bayonet | 486.6 | 357.5 | +193.7 |
| Bash | 359.6 | 269.7 | +134.8 |
| Crowd Control (per target) | 481.7 | 409.4 | +108.5 (50% of 217.0) |
| Venom Grenade (per target) | 199.3 | 199.3 | none |

Figures are per target, from the measurement copy (standing +40.5% damage). The critical bonus is the Mids enhanced value of the attack's base hit, which is what a canonical repeat adds (arithmetic).

## Targets and achieved numbers

Targets were set before slotting, in the operator's priority order: positional defense at or above 45% with margin; resistances as high as the build allows; hit points, regeneration and survivability pieces; 95% hit chance; endurance; then damage and recharge. Permanent Hasten is not a target.

**Resistance cap: 85%.** Mids fixture `tests/fixtures/mids/archetypes.json`, entry `"ClassName": "Class_Arachnos_Soldier"`, field `"ResCap": 0.85` (`"HPCap": 2409.518`). Canonical `data/canonical/archetypes/arachnos_soldier.json`, `attrib_resistance_max`: 0.85. `strategy/breakpoints.json` lists 75 for `Arachnos_Soldier`; both data sources say 85%, and that file was not changed.

| Target | Goal | Before (in combat) | Now: Mids total | Now: in combat (arithmetic) | Met |
| --- | --- | --- | --- | --- | --- |
| Melee defense | ≥ 45%, margin (no defense-debuff resistance) | 55.54% | 61.50% | **56.56%** | yes, 11.6 points of margin |
| Ranged defense | ≥ 45% | 52.36% | 62.27% | **57.33%** | yes |
| AoE defense | ≥ 45% | 47.73% | 53.37% | **48.43%** | yes, 3.4 points of margin |
| Typed defense | report | — | S/L 58.69%, F/C 54.62%, E/N 49.62%, Tox/Psi 38.48% | S/L 53.75%, F/C 49.68%, E/N 44.68%, Tox/Psi 38.48% | noted |
| Smashing/Lethal resistance | as high as the build allows (cap 85%) | 51.26% | 49.01% | same | noted (−2.25: Mocking Beratement's 3-piece bonus is gone) |
| Fire/Cold resistance | as above | 44.98% | 44.98% | same | noted |
| Energy/Negative resistance | as above | 20.23% | **24.73%** | same | improved, still the weak point |
| Toxic resistance | as above | 45.73% | 47.98% | same | improved |
| Psionic resistance | as above | 45.73% | 52.98% | same | improved |
| Hit points | report | 1,522.1 | **1,538.2** (64% of the 2,409.5 cap) | same | improved |
| Regeneration | report | 14.16 HP/s | **19.30 HP/s** (planner) | same | improved |
| Global recharge | fact, not a target | +183.75% | +160.0% (Hasten's +70% included) | same | — |
| Hasten | fact, not a target | 117.55 s (permanent) | 126.44 s against its 120 s duration: down 6.44 s a cycle, 94.9% uptime (arithmetic) | same | — |
| Hit chance vs +4 foes, +25% defense | 95% on every attack | 95% | 95% on all six attacks, Placate and Surveillance | same | yes |
| Endurance, toggles only | positive | +2.02 end/s | `EndRec` 0.8573, `EndUse` 1.4497 end/s; recovery 3.37 end/s; net +1.92 end/s | same | yes |
| Endurance, chains | sustainable, or time from a full bar | −0.47 / −0.75 end/s | single target −0.56 end/s, 183 s; groups −0.91 end/s, 114 s (arithmetic) | same | no; shortfall stated |
| Mez protection | from the build | armour | Bane Spider Armor Upgrade: protection against hold, stun, sleep, immobilize, confuse and fear (canonical, magnitude 4) | same | yes |

Recovery per second is Mids' formula from `Statistics.cs`: (1 + `EndRec`) × 1.05 × 1.6667 × (1 + `EndMax` / 100) = 1.8573 × 1.75 × 1.036 = 3.37 end/s. The planner reports the same 3.37. The planner fires every power the moment it recharges, so its bar times are worst cases; a player presses most powers less often.

### Hit chance

min(95%, accuracy multiplier × (75% + 34.94% − 25%) × 0.65). The 0.65 level factor for a +4 foe is approximate. The accuracy multiplier is Mids' enhanced `Accuracy` divided by (0.75 + its to-hit). The 95% line is a multiplier of 1.72.

| Key | Power | Accuracy multiplier | Hit chance | Recharge (s) | End cost |
| --- | --- | --- | --- | --- | --- |
| 1 | Bash | 2.401 | 95% | 1.14 | 2.82 |
| 2 | Placate | 1.955 | 95% | 11.18 | 1.72 |
| 3 | Shatter | 2.631 | 95% | 3.33 | 5.90 |
| 4 | Pulverize | 2.787 | 95% | 2.41 | 3.71 |
| 5 | Bayonet | 2.505 | 95% | 2.22 | 4.24 |
| 6 | Crowd Control | 2.401 | 95% | 4.57 | 8.24 |
| ALT+1 | Surveillance | 2.220 | 95% | 14.38 | 7.05 |
| ALT+2 | Venom Grenade | 2.286 | 95% | 6.86 | 6.43 |

## Recommended attack chains

Two sources, kept apart:

- **Planner** (`<python> -m coh_engine chain <dump> --stance melee --target single|spawn:10`, run on the measurement copy without the Gaussian's proc, dumped from the 2026.5.1337 database). It uses Mids' damage per activation, so **every mace hit is counted as a critical** at Mids' size. It reserves cast time for Build Up and Surveillance on cooldown, counts Build Up at 38% uptime (×1.128) and the stacked −40% resistance against a foe with 0% resistance (×1.400). It does not model Placate, a critical-hit rule, or arch-villain resistance.
- **Arithmetic** (`chain_arith.py` in the scratch folder): the same attacks with the critical removed, and Placate's canonical critical counted only on the hit Placate precedes. Cast times are arcanatimes. A power repeats only when its recharge plus its cast fits the cycle. Endurance uses the planner's recovery (3.37 end/s) and toggle drain (1.45 end/s).

**Single hard target (arch-villain):**

- Repeating sequence, once per Placate recharge: **1 Bash → 2 Placate → 3 Shatter (critical) → 4 Pulverize → 5 Bayonet → 1 Bash → ALT+2 Venom Grenade**. Press ALT+1 Surveillance whenever it is up (about 7 cycles in 8). Press CTRL Build Up every other cycle, just before Placate.
- Cycle 14.31 s on average against Placate's 12.24 s (recharge plus cast), so Placate is up at every key 2. Every attack is recharged when its turn comes. Surveillance (14.38 s plus its 1.72 s cast) holds its 20 s debuff without a gap.
- **Without critical hits** (the same attacks with no Placate): **142.7 damage per second** raw (arithmetic).
- **With the Placate critical on Shatter**, one critical per cycle (1 of 6 attacks): **153.6 damage per second** raw (arithmetic), of which 21.4 is the critical.
- With Build Up at about 35% uptime (×1.12) and the −40% resistance on a 0%-resistance target (×1.4), the estimate is about **224** without the critical and **240** with it (arithmetic). An arch-villain's own resistance and 4-star debuff resistance lower the ×1.4.
- Planner (always-critical at Mids' size): Shatter → Pulverize → Bayonet → Bash → Venom Grenade, cycle 9.50 s, gap 0. It gives 194.7 raw and 307.3 with Build Up and both debuffs. Treat it as an upper bound that the game does not reach.
- Endurance: 2.48 end/s for the chain with its clicks; net −0.56 end/s; a full bar (103.6) lasts about 183 s (arithmetic). Planner: net −0.70 end/s, 147.8 s.
- Opening on an arch-villain: ALT+1 Surveillance, ALT+2 Venom Grenade, CTRL Build Up, 2 Placate, 3 Shatter. If you arrive from Cloaking Device after 10 s idle, the first attack is already critical. In that case open with 3 Shatter, then pair the next Placate with 4 Pulverize, because Shatter is still recharging.

**Groups (`spawn:10`):**

- Repeating sequence: **6 Crowd Control → 3 Shatter → 4 Pulverize → ALT+2 Venom Grenade**. On every other cycle, put **2 Placate** in front of 6 Crowd Control. Press CTRL Build Up every third cycle. Use 1 Bash and 5 Bayonet as filler when the spawn is down to one or two foes.
- Cycle 9.28 s on average; gapless. Placate's turn comes every 18.6 s against its 12.24 s, and Crowd Control (4.57 s) is always ready after it.
- **Without critical hits** (no Placate): **786.1 damage per second** raw on ten targets (arithmetic).
- **With Placate → Crowd Control every other cycle:** **799.8 damage per second** raw (arithmetic), +1.7%. Crowd Control's repeat adds 108.5 per target on average, which beats Shatter's 306.7 from three targets up; pairing Placate with Shatter gives 757.9.
- **Placate every cycle loses damage in groups.** Its 11.18 s recharge is longer than the four-attack cycle, so a 1 Bash has to be added to wait for it. That gives 722.9 damage per second.
- Planner (always-critical): Crowd Control → Shatter → Pulverize → Venom Grenade, cycle 8.71 s, gap 0.39 s. It gives 771.2 raw and 1,217.5 with Build Up and debuffs.
- Endurance: 2.83 end/s; net −0.91 end/s; a full bar lasts about 114 s (arithmetic). Planner: net −1.00 end/s, 103.3 s.
- Openers: ALT+3 Ion Core Final Judgement on a fresh spawn, ALT+2 Venom Grenade, then 2 Placate and 6 Crowd Control.

**Build Up earns its pick.** The planner credits it ×1.128 to every layer at its 25.29 s recharge, which is more than the Placate critical adds. It costs one pick and one added slot, and it sits on the CTRL row, which does not count toward the 12 keys. The VEAT handbook calls Build Up "kind of a non-starter" for tight Bane builds; the planner's figure says otherwise here.

## Survival in melee range of a 4-star arch-villain

What the build reaches by itself:

- **Defense in combat:** melee 56.56%, ranged 57.33%, AoE 48.43% (above). Melee has the margin because that is where the arch-villain hits.
- **Resistance:** Smashing/Lethal 49.01%, Fire/Cold 44.98%, Toxic 47.98%, Psionic 52.98%, Energy/Negative 24.73%. Energy and negative rose 4.5 points from Shield Wall's four-piece bonus, which the slots freed from Hasten, Placate and Tactics paid for. Psionic rose 7.25 points from Aegis: Psionic/Status Resistance in Tough and Perfect Zinger in Placate.
- **Hit points 1,538.2, regeneration 19.30 HP/s,** plus Preventive Medicine's absorb proc in Health, which Mids does not count. There is no self-heal.
- **Mez protection** from the armour. **No defense-debuff resistance** exists in the six Soldier sets: every −defense the arch-villain lands comes straight off the numbers above.
- **Placate** drops up to 5 foes' attention on you while it lasts. The canonical help text warns that attacking a placated foe lets it attack you back.

What it relies on the team for:

- **A tank holding the arch-villain's aggro.** With 24.7% energy/negative resistance and 1,538 HP, a few unavoided energy or negative hits from a +4 arch-villain take most of the bar.
- **Barrier on the team's rotation** (this build carries one) for burst windows and defense-debuff cascades.
- **Heals or absorb and defense-debuff resistance** from a support player, or purple inspirations.
- **Recovery help** (Speed Boost, Ageless) for long fights, since both chains drain endurance.

## Team value

What the build gives the other seven players, as slotted. The aura values are Mids' figures for the slotted toggles; teammates in range get the same buff.

| Source | Effect | Uptime |
| --- | --- | --- |
| Tactical Training: Maneuvers | +15.99% defense, all positions and types | toggle, always |
| Maneuvers (Leadership pool) | +5.32% defense (three Luck of the Gambler pieces) | toggle, always |
| Tactical Training: Assault | +15% damage | toggle, always |
| Assault (Leadership pool) | +15% damage | toggle, always |
| Tactical Training: Leadership | +15.75% to-hit, +perception | toggle, always |
| Tactics (Leadership pool) | +13.19% to-hit, +perception (one To Hit Buff IO at +5) | toggle, always |
| Venom Grenade (ALT+2) | −20% resistance to every damage type on up to 10 foes, 16 s (Mids lists toxic at −40%) | recharges in 6.86 s; pressed every cycle: 100% on the held target |
| Surveillance (ALT+1) | −20% resistance and −15% defense on one foe, 20 s | recharges in 14.38 s: 100% on the arch-villain |
| Both together | −40% resistance on the arch-villain before its own resistance | 100% |
| Reactive Radial Flawless Interface | −resistance and fire damage over time | proc; not modelled |
| Mace attacks | Pulverize 20% and Bash 10% chance to stun; Shatter and Crowd Control knock down (magnitude 0.67) | per hit |
| Placate | foe placate on up to 5 targets | 11.18 s recharge |

Both Maneuvers and both Assaults are taken: together +21.31% defense and +30% damage for the team. Tactics gives 2.56 points less to-hit than before (13.19% against 15.75%); its four Gaussian's slots went to Combat Training: Defensive's resistance and hit points (see rule 10). Mids counts each Soldier aura and the matching pool power as separate buffs; whether the game stacks them is not in the data.

**What the build does not cover:** heals, absorb for others, resistance buffs for others, regeneration debuffs, defense-debuff resistance, and holding aggro (Placate sheds it). Its only defense debuff is Surveillance's −15% on one target; the mace attacks carry none.

## Slotting rules applied

One line per rule of the operator's review, with what changed.

1. **Sets outrank diversification.** All six attacks keep five or six pieces of a Very Rare, Winter or archetype set: Hecatomb, Superior Blistering Cold, Ragnarok, Superior Spider's Bite, Armageddon and Superior Dominion of Arachnos. No set was broken up; the freed slots came from plain IOs and common sets.
2. **No over-slotting with plain IOs.** Hasten's third Recharge IO is removed (it was flagged P-ED-002, adding 15.76% over two boosted IOs). Stamina holds one Endurance Modification IO and one set piece, below the threshold.
3. **Hasten: two Recharge IOs at +5.** Done: two Invention: Recharge Reduction at +5.
4. **Permanent Hasten is not a target.** It is removed as a target everywhere, and the choices made for it are reversed. Mocking Beratement's six pieces in Placate (taken for 7.5% recharge) became two pieces. Reactive Defenses' sixth piece (8.75% recharge) is gone. The fifth Luck of the Gambler global moved out of Combat Training: Defensive. Recharge fell from +183.75% to +160%, and Hasten from 117.55 s to 126.44 s, 94.9% uptime.
5. **Boost single-aspect IOs to +5.** All 11 plain IOs carry `PlusFive`, set by a post-processing step in `bane_spec_v3.py`; the harness reads all 11 (see "What the totals count").
6. **Luck of the Gambler: no lone pieces.** Combat Jumping (2 pieces), pool Maneuvers (3) and Cloaking Device (2) now hold sets. Combat Training: Defensive no longer holds a lone piece. Tactical Training: Maneuvers keeps 5. The two-piece regeneration bonus is held five times (four Luck of the Gambler sets and Shield Wall), at the rule-of-five limit. The Defense/Endurance pieces cut each toggle's cost.
7. **Uniques where the slot's level makes them count.** Both +3% defense globals stay in Bane Spider Armor Upgrade at slots 1 and 7. Unbreakable Guard: +Max HP moved to Tough's free slot, and Aegis: Psionic/Status Resistance was added in Tough's first added slot. Reactive Defenses: Scaling Resist Damage holds Weave's free slot. See the table below.
8. **Survivability pieces in Health.** Health now holds Numina's Convalesence: +Regeneration/+Recovery, Miracle: +Recovery, Panacea: +Hit Points/Endurance, Preventive Medicine: Chance for +Absorb and Regenerative Tissue: +Regeneration. They were paid for from plain IOs and Mocking Beratement's last pieces.
9. **Quality-of-life powers at the level they open.** Mental Training moved from 49 to 22, the level it opens. The rest of the pick order and the reason for each late pick are in "Pick order" below.
10. **Rarer sets measured.** Two comparisons were measured on the totals:
    - **Placate:** two Perfect Zinger pieces against two Mocking Beratement pieces (both Threat sets; there is no purple, Winter or archetype Threat set). Perfect Zinger gives +2.25% toxic and psionic resistance and a 11.18 s Placate recharge. Mocking Beratement gives +1.8% maximum endurance and 10.64 s. Perfect Zinger is kept, since resistance outranks endurance.
    - **Four slots:** four Gaussian's pieces in Tactics against four Shield Wall pieces in Combat Training: Defensive. Shield Wall gives +4.5% energy/negative resistance, +3.9% ranged defense, +4 HP and +0.10 on Mids' `HPRegen` (1.51 against 1.41). Gaussian's gives +2.56% team to-hit, +2.5% recovery and lower toggle cost. Shield Wall is kept by the priority order.
    - Kismet, Steadfast Protection, Miracle and Regenerative Tissue are single unique pieces held for their global; no rarer set carries the same global.
    - The tool's P-SET-003 reports no lower-tier set while a rarer one is unused.
11. **Endurance.** No set bonus, resistance or proc was traded for endurance. Toggles net +1.92 end/s against +2.02 before.

### Unique placement

The lowest exemplar level is the highest of three: the pick level minus 5, the slot level minus 5, and the set's minimum level minus 3 (the exemplar −3 rule in `docs/style-guides/build-rules/exemplar-10.md`). It is arithmetic on the slot levels in the `.mbd`.

| Unique | Power | Slot level | Lowest exemplar level at which it works |
| --- | --- | --- | --- |
| Steadfast Protection: Resistance/+Defense 3% | Bane Spider Armor Upgrade | 1 (A) | 6 |
| Gladiator's Armor: TP Protection +3% Defense (All) | Bane Spider Armor Upgrade | 7 | 6 |
| Shield Wall: +Resist (Teleportation), +5% Resist (All) | Combat Training: Defensive | 2 (A) | 6 |
| Kismet: Accuracy +6% | Combat Jumping | 5 | 6 |
| Luck of the Gambler: Defense/Increased Global Recharge Speed | Combat Jumping | 4 (A) | 21 |
| Luck of the Gambler: Defense/Increased Global Recharge Speed | Tactical Training: Maneuvers | 10 (A) | 21 |
| Luck of the Gambler: Defense/Increased Global Recharge Speed | Maneuvers | 41 (A) | 36 |
| Luck of the Gambler: Defense/Increased Global Recharge Speed | Cloaking Device | 44 (A) | 39 |
| Unbreakable Guard: +Max HP | Tough | 32 (A) | 27 |
| Aegis: Psionic/Status Resistance | Tough | 33 | 28 |
| Reactive Defenses: Scaling Resist Damage | Weave | 35 (A) | 30 |
| Numina's Convalesence: +Regeneration/+Recovery | Health | 1 (A) | 26 |
| Miracle: +Recovery | Health | 15 | 16 |
| Panacea: +Hit Points/Endurance | Health | 23 | 18 |
| Preventive Medicine: Chance for +Absorb | Health | 34 | 29 |
| Regenerative Tissue: +Regeneration | Health | 46 | 41 |
| Performance Shifter: Chance for +End | Stamina | 1 (A) | 17 |
| Gaussian's Synchronized Fire-Control: Chance for Build Up | Tactical Training: Leadership | 48 | 43 |

Health's added slots were not pinned. The writer placed them after the exemplar-10 essentials, so Preventive Medicine and Regenerative Tissue work only from exemplar 29 and 41. The +3% defense globals, Shield Wall and Kismet all work from exemplar 6.

### `validate --slotting` findings

`CohEngine.Cli.exe validate <build>.mbd --mids-dir tests/fixtures/mids --slotting`:

- **Before (the delivered build): 6 warnings, 1 info.**
  - P-ED-002: Hasten's third Recharge IO.
  - P-SET-002 four times: lone Luck of the Gambler in Combat Training: Defensive, Combat Jumping, Maneuvers and Cloaking Device.
  - P-POWER-001: Mental Training picked at 49 against an opening at 22.
  - Info P-IO-003: no Preventive Medicine: Chance for +Absorb or Regenerative Tissue: +Regeneration.
- **After: no violations** (no warnings, no info). `python -m coh_engine validate` also exits 0 with "no violations".

### Pick order

Picks follow the level each power opens, except where noted:

- Mental Training at 22, its opening level.
- Placate at 24 (opens 22), Surveillance at 26 (opens 24) and Crowd Control at 28 (opens 26): each moved one pick later so Mental Training could take 22.
- Venom Grenade at 14 (opens 12): Pulverize, the second exemplar-10 attack, takes 12.
- Bayonet at 38 (opens 12): picks 12 to 28 go to the chain's core and the three Tactical Training toggles, which open by then and carry the team value.
- Boxing at 30, Tough at 32 and Weave at 35: the Fighting pool, for resistance and defense before the duplicate Leadership toggles.
- Maneuvers at 41, Cloaking Device at 44, Assault at 47 and Tactics at 49 (open at 4 to 24): each duplicates a Tactical Training aura or adds a few points of defense that the build already meets without it.

## Powers by level, with slotting

Set pieces are level 50, attuned or superior; plain IOs are level 50 boosted to +5. `(A)` is the free slot; numbers are the levels the added slots were placed at.

- **1 Bash:** Hecatomb: Damage/Recharge/Accuracy (A), Recharge/Accuracy (3), Damage/Recharge (3), Damage/Endurance (25), Damage (37), Chance of Damage (Negative) (46).
- **1 Bane Spider Armor Upgrade:** Steadfast Protection: Resistance/+Defense 3% (A); Gladiator's Armor: TP Protection +3% Defense (All) (7).
- **2 Combat Training: Defensive:** Shield Wall: +Resist (Teleportation), +5% Resist (All) (A), Defense/Endurance (7), Defense/Endurance/Recharge (23), Defense (43).
- **4 Combat Jumping:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense/Endurance (5); Kismet: Accuracy +6% (5).
- **6 Build Up:** Invention: Recharge Reduction +5 (A), (9).
- **8 Hasten:** Invention: Recharge Reduction +5 (A), (9).
- **10 Tactical Training: Maneuvers:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense (11), Defense/Endurance (11), Defense/Endurance/Recharge (34), Defense/Recharge (45).
- **12 Pulverize:** Superior Blistering Cold: Accuracy/Damage (A), Accuracy/Damage/Endurance (13), Accuracy/Damage/Recharge (13), Damage/Endurance/Accuracy/RechargeTime (27), Damage/Endurance (37), Recharge/Chance for Hold (46).
- **14 Venom Grenade:** Ragnarok: Damage/Recharge/Accuracy (A), Recharge/Accuracy (15), Damage/Recharge (17), Damage/Endurance (31), Damage (40), Chance for Knockdown (48).
- **16 Tactical Training: Assault:** Invention: Endurance Reduction +5 (A).
- **18 Shatter:** Superior Spider's Bite: Accuracy/Damage (A), Accuracy/Damage/RechargeTime (19), Accuracy/Damage/Endurance/RechargeTime (19), Damage/RechargeTime (31), Damage/Endurance/RechargeTime (40), RechargeTime/Global Toxic (48).
- **20 Tactical Training: Leadership:** Gaussian's Synchronized Fire-Control: To Hit Buff (A), To Hit Buff/Recharge (21), To Hit Buff/Recharge/Endurance (21), Recharge/Endurance (31), To Hit Buff/Endurance (40), Chance for Build Up (48).
- **22 Mental Training:** Invention: Run Speed +5 (A). Taken for its +20% recharge and slow resistance.
- **24 Placate:** Perfect Zinger: Accuracy/Recharge (A), Threat/Placate/Recharge (25).
- **26 Surveillance:** Invention: Accuracy +5 (A); Invention: Recharge Reduction +5 (27).
- **28 Crowd Control:** Armageddon: Damage/Recharge/Accuracy (A), Recharge/Accuracy (29), Damage/Recharge (29), Damage/Endurance (33), Damage (42), Chance for Fire Damage (50).
- **30 Boxing:** Invention: Accuracy +5 (A). Prerequisite, not on a key.
- **32 Tough:** Unbreakable Guard: +Max HP (A); Aegis: Psionic/Status Resistance (33); Unbreakable Guard: Resistance (33), Resistance/Endurance (34), Resistance/Endurance/RechargeTime (43), RechargeTime/Resistance (50).
- **35 Weave:** Reactive Defenses: Scaling Resist Damage (A), Defense (36), Defense/Endurance (36), Defense/RechargeTime (36), Defense/Endurance/RechargeTime (45).
- **38 Bayonet:** Superior Dominion of Arachnos: Accuracy/Damage (A), Accuracy/Damage/Recharge (39), Accuracy/Damage/Endurance/Recharge (39), Damage/Recharge (39), Damage/Endurance/Recharge (43), Recharge/Chance for −Damage and Terrorize (50).
- **41 Maneuvers:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense/Endurance/Recharge (42), Defense (42).
- **44 Cloaking Device:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense/Endurance (45).
- **47 Assault:** Invention: Endurance Reduction +5 (A).
- **49 Tactics:** Invention: To Hit Buff +5 (A).
- **Health:** Numina's Convalesence: +Regeneration/+Recovery (A); Miracle: +Recovery (15); Panacea: +Hit Points/Endurance (23); Preventive Medicine: Chance for +Absorb (34); Regenerative Tissue: +Regeneration (46).
- **Stamina:** Performance Shifter: Chance for +End (A), EndMod (17), EndMod/Recharge (37).

Slots: 67 added, 24 picks, 4 pools.
Exemplar 10: Bash and Pulverize are the two attacks (Bash with two added slots at 3), Combat Jumping at 4 is the travel power, and Tactical Training: Maneuvers has two added slots by level 11.

**Lone set pieces.** No Luck of the Gambler piece stands alone. Single unique pieces held for their global are Steadfast Protection, Gladiator's Armor, Kismet, Aegis, Numina's Convalesence, Miracle, Panacea, Preventive Medicine, Regenerative Tissue and Performance Shifter's proc. Their partners would add little the uniques do not, so they are held as uniques rather than as sets.

### Verbatim MidsReborn export (power lines)

The export does not print the +5 boost on plain IOs; the `.mbd` carries it.

```text
Level 1:	Bash				Hct-Acc/Dmg/Rchg(A), Hct-Acc/Rchg(3), Hct-Dmg/Rchg(3), Hct-Dmg/EndRdx(25), Hct-Dmg(37), Hct-Dam%(46)
Level 1:	Bane Spider Armor Upgrade		StdPrt-ResDam/Def+(A), GldArm-3defTpProc(7)
Level 2:	Combat Training: Defensive		ShlWal-ResDam/Re TP(A), ShlWal-Def/EndRdx(7), ShlWal-Def/EndRdx/Rchg(23), ShlWal-Def(43)
Level 4:	Combat Jumping			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(5), Ksm-ToHit+(5)
Level 6:	Build Up			RechRdx-I(A), RechRdx-I(9)
Level 8:	Hasten				RechRdx-I(A), RechRdx-I(9)
Level 10:	Tactical Training: Maneuvers		LucoftheG-Def/Rchg+(A), LucoftheG-Def(11), LucoftheG-Def/EndRdx(11), LucoftheG-Def/EndRdx/Rchg(34), LucoftheG-Def/Rchg(45)
Level 12:	Pulverize			SprBlsCol-Acc/Dmg(A), SprBlsCol-Acc/Dmg/EndRdx(13), SprBlsCol-Acc/Dmg/Rchg(13), SprBlsCol-Dmg/EndRdx/Acc/Rchg(27), SprBlsCol-Dmg/EndRdx(37), SprBlsCol-Rchg/HoldProc(46)
Level 14:	Venom Grenade			Rgn-Acc/Dmg/Rchg(A), Rgn-Acc/Rchg(15), Rgn-Dmg/Rchg(17), Rgn-Dmg/EndRdx(31), Rgn-Dmg(40), Rgn-Knock%(48)
Level 16:	Tactical Training: Assault		EndRdx-I(A)
Level 18:	Shatter				SprSpdBit-Acc/Dmg(A), SprSpdBit-Acc/Dmg/Rchg(19), SprSpdBit-Acc/Dmg/EndRdx/Rchg(19), SprSpdBit-Dmg/Rchg(31), SprSpdBit-Dmg/EndRdx/Rchg(40), SprSpdBit-Rchg/Global Toxic(48)
Level 20:	Tactical Training: Leadership		GssSynFr--ToHit(A), GssSynFr--ToHit/Rchg(21), GssSynFr--ToHit/Rchg/EndRdx(21), GssSynFr--Rchg/EndRdx(31), GssSynFr--ToHit/EndRdx(40), GssSynFr--Build%(48)
Level 22:	Mental Training			Run-I(A)
Level 24:	Placate				PrfZng-Acc/Rchg(A), PrfZng-Taunt/Rchg(25)
Level 26:	Surveillance			Acc-I(A), RechRdx-I(27)
Level 28:	Crowd Control			Arm-Acc/Dmg/Rchg(A), Arm-Acc/Rchg(29), Arm-Dmg/Rchg(29), Arm-Dmg/EndRdx(33), Arm-Dmg(42), Arm-Dam%(50)
Level 30:	Boxing				Acc-I(A)
Level 32:	Tough				UnbGrd-Max HP%(A), Ags-Psi/Status(33), UnbGrd-ResDam(33), UnbGrd-ResDam/EndRdx(34), UnbGrd-ResDam/EndRdx/Rchg(43), UnbGrd-Rchg/ResDam(50)
Level 35:	Weave				Rct-ResDam%(A), Rct-Def(36), Rct-Def/EndRdx(36), Rct-Def/Rchg(36), Rct-Def/EndRdx/Rchg(45)
Level 38:	Bayonet				SprDmnofA-Acc/Dmg(A), SprDmnofA-Acc/Dmg/Rchg(39), SprDmnofA-Acc/Dmg/EndRdx/Rchg(39), SprDmnofA-Dmg/Rchg(39), SprDmnofA-Dmg/EndRdx/Rchg(43), SprDmnofA-Rchg/DmgFear%(50)
Level 41:	Maneuvers			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx/Rchg(42), LucoftheG-Def(42)
Level 44:	Cloaking Device			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(45)
Level 47:	Assault				EndRdx-I(A)
Level 49:	Tactics				ToHit-I(A)
Level 1:	Health				NmnCnv-Regen/Rcvry+(A), Mrc-Rcvry+(15), Pnc-Heal/+End(23), Prv-Absorb%(34), RgnTss-Regen+(46)
Level 1:	Stamina				PrfShf-End%(A), PrfShf-EndMod(17), PrfShf-EndMod/Rchg(37)
```

## Incarnates (all tier 4; Lore left empty, as 4-star bans pets)

- **Alpha: Cardiac Core Paragon.** See "Alpha choice".
- **Judgement: Ion Core Final Judgement.** On ALT+3.
- **Interface: Reactive Radial Flawless Interface.** The VEAT handbook names Reactive and Degenerative Radial; Reactive's −resistance stacks with Venom Grenade and Surveillance.
- **Destiny: Barrier Core Epiphany.** Off in the totals. By this project's hard-mode convention, six of eight players take Barrier and rotate two at once every 30 s; this build is one of the six.
- **Hybrid: Assault Radial Embodiment.** The petless choice in the VEAT handbook. It is a toggle on for part of each cycle, so it is off in the totals. The Mids fixture has no Melee Hybrid, so a survival Hybrid could not be measured.

## Alpha choice

Same picks and slotting; only the Alpha changed. Mids totals (defense before the Cloaking Device correction):

| Measure | Cardiac Core (taken) | Agility Core |
| --- | --- | --- |
| Melee / Ranged / AoE defense | 61.50% / 62.27% / 53.37% | 66.26% / 68.35% / 58.14% |
| Smashing/Lethal, Energy/Negative resistance | 49.01%, 24.73% | 45.36%, 23.23% |
| Hasten recharge (120 s duration) | 126.44 s | 118.56 s |
| `EndRec` / `EndUse` (toggles) | 0.8573 / 1.4497 end/s | 0.9663 / 1.9206 end/s |
| Net endurance, toggles only (arithmetic) | +1.92 end/s | +1.64 end/s |
| Shatter end cost | 5.90 | 7.60 |
| Single-target chain net (arithmetic) | −0.56 end/s | about −1.55 end/s (attack costs scale by 7.60 / 5.90) |

Cardiac is taken, as the operator prefers. Every positional target is met with it, and it keeps 3.7 points more smashing/lethal resistance. Agility's extra defense costs about 29% more endurance per attack, which nearly triples the single-target drain.

## Trade-offs

- **The chains drain endurance.** Toggles leave +1.92 end/s. The single-target chain nets −0.56 end/s (183 s from a full bar) and the group chain −0.91 end/s (114 s). Both are worst cases at every power on cooldown, and Mids does not count the Performance Shifter proc. In play: drop Venom Grenade from a cycle when the target already carries it, ask for Speed Boost or Ageless, carry blue inspirations.
- **Energy/Negative resistance 24.73%.** Shield Wall's four pieces raised it from 20.23%. The remaining energy/negative set bonuses sit in melee sets such as Touch of Death. Using them would mean breaking an attack's purple, Winter or archetype set, which rule 1 forbids.
- **AoE defense 48.43% in combat, 3.4 points over the floor.** The Gaussian's six-piece bonus (+2.5% in every position) sits in Tactical Training: Leadership. It cannot be repeated in Tactics because the Chance for Build Up piece is unique.
- **Hasten is up 94.9% of the time.** It is reported as a fact, by rule 4: the recharge given up for it bought Shield Wall, Aegis and Health's survivability pieces.
- **Placate in groups only every other cycle.** Its 11.18 s recharge no longer fits the 8.76 s group cycle, and waiting for it every cycle loses damage (722.9 against 799.8).
- **Tactics with one To Hit Buff IO.** The team gets 13.19% to-hit from it rather than 15.75%; the four slots bought +4.5% energy/negative resistance, +3.9% ranged defense and more hit points.
- **Bayonet over Frag Grenade for the sixth attack.** Bayonet adds 357.5 per activation to the single-target chain and carries the critical rule; Frag Grenade would add a second area attack for groups. The style's point is single-target damage, so Bayonet.
- **No epic pool.** The four VEAT epics offer ranged attacks and pets, nothing for melee survival.
- **Boxing** is a dead pick that unlocks Tough (+25.8% smashing/lethal resistance, slotted) and Weave (+7.9% defense, slotted).

## 4-star play

- Let the tank take the arch-villain first. Use Surveillance and Venom Grenade from range, then close in.
- Press Placate before Shatter every cycle, and do not reposition between them: moving cancels Placate's hidden status (`cancel_events` `Moved`).
- Build Up every other cycle on an arch-villain, just before Placate and Shatter.
- On groups, Placate before Crowd Control whenever it is up, about every other cycle.
- Stagger Barrier with the team's other Barrier holders. Use it when the arch-villain's energy or negative attacks land.

## What I would verify

- **The critical size.** Canonical data repeats the full base hit, while Mids' database stores two thirds and counts it as always on. Check one Placate → Shatter in the combat log against a plain Shatter.
- **That the attack consuming Placate still crits.** Bash's canonical help text says it does ("This power will deal critical damage if used after a successful Placate"). The data shows the Meter cancelled by `AttackedOther`, so the order of the two events is up to the game.
- **What `Moved` means.** Placate's Meter is cancelled, and Cloaking Device's suppressed, on `Moved`. The Stalker's Hide carries the same event, and Stalkers keep Hide while walking, so it probably means knockback or teleport, not walking. Not confirmed.
- **Placate against an arch-villain.** The self-hide comes with a successful Placate; arch-villain placate resistance is not modelled.
- **Aura stacking.** Mids stacks Tactical Training: Maneuvers with pool Maneuvers, and Assault with Assault.
- **The 0.65 level factor** in the hit-chance check is approximate.

## Guides used (paraphrased, short quotes only)

Under `homecoming-source-material/community/archetypes/arachnos/`:

- `the-spider-s-handbook-an-incomplete-guide-to-veats.md` ("The Spider's Handbook: An Incomplete Guide to VEATs"):
  - Placate enables critical hits on Pulverize, Shatter and Crowd Control.
  - A Bane is like a mace Stalker "with minimal DDR".
  - Build Up is called a non-starter for tight Bane builds.
  - Reactive or Degenerative Radial Interface; Assault Radial Hybrid for a petless build.
- `bane-spider-all-mace-all-day.md` ("Bane Spider - all mace, all day"): a mace Bane that takes Build Up, Placate, Cloaking Device and Surveillance, and slots Surveillance lightly to free slots for smashing/lethal resistance.
- `good-solo-and-team-friendly-pure-mace-bane.md` ("Good solo and team friendly pure mace Bane"): Cloaking Device as a Luck of the Gambler holder, and Surveillance on one accuracy slot.

## Files

Sibling files in this folder, all named "4-star" plus an ending:

- `.mbd`
- `.md`
- `.mids-totals.json`

No Sidekick file was made for this build.
