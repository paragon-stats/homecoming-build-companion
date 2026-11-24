# Fire Blast-Kinetics Corruptor - 4-Star

Level 50 Corruptor: Fire Blast / Kinetics, with the Leadership, Flight, Fighting and Speed pools and Soul Mastery.
It is a support blaster for Homecoming 4-star task forces. It stays alive at range first and adds damage and buffs second, the same order as the posted 4-star Fire/Kinetics build. It is not a proc build.
Loadable file: `fire-kin.mbd`. Every number below is from MidsReborn's own totals (`totals.json`, `TotalsCapped`) unless marked as my arithmetic.

## What the totals count

- **Counted as always on:** every toggle (Maneuvers, Hover, Tough, Weave, Dark Embrace, Tactics, Evasive Maneuvers), Health, Stamina and Hasten.
- **Not counted:** Barrier and Support Core Embodiment (both switched off in the file), Power Boost, Siphon Speed, Siphon Power, Fulcrum Shift, Increase Density, and the procs. Fly is off: Hover is the combat flight.
- Power Boost and Siphon Speed were measured in separate runs; their figures are given beside the targets.

## Targets and achieved numbers

| Target | Goal | Achieved (Mids) | Met |
| --- | --- | --- | --- |
| Ranged defense, Power Boost off | ≥ 45% (stretch 50%) | 50.32% | yes, stretch too |
| Ranged defense, Power Boost on | report | 67.43% for 15 s; Power Boost recharges in 37.3 s | noted |
| Energy / Negative defense | report | 43.45% | noted |
| Smashing / Lethal / Fire / Cold / Melee / AoE defense | report | 36.57% | noted |
| Smashing / Lethal resistance | ≥ 60% (stretch 75% cap) | 62.96% | yes (stretch no) |
| Negative resistance | ≥ 50% | 39.99% | **no** (see Trade-offs) |
| Energy resistance | as high as budget allows | 22.90% | noted |
| Fire / Cold resistance | report | 13.90% | noted |
| Hit points | against the 1,606.3 cap | 1,243.7 (77% of the cap) | noted |
| Global recharge | perma-Hasten | +146.3% (Hasten's +70% included): Hasten recharges in 124.9 s against its 120 s duration. With Siphon Speed counted, +166.3% and Hasten recharges in 118.3 s | yes, with Siphon Speed |
| Siphon Speed uptime | stated | Recharges in 20.0 s; its +20% recharge lasts 60 s, so one hit a minute keeps it at 100% | yes |
| Hit chance vs +4 foes with +25% defense | ≥ 95% for every power that rolls | 95% for every attack and every Kinetics power that rolls (see the table below) | yes |
| Endurance with toggles on | positive | 3.37 end/s recovery against 1.68 end/s toggles: +1.69 end/s | yes |
| Blazing Bolt fast-snipe to-hit | ≥ +22% | +22.13% (Tactics and Kismet) | yes |

Recovery per second is Mids' own formula from `Statistics.cs`: (1 + EndRec) × 1.6667 × (1 + EndMax / 100).
The single-target chain below costs about 3.55 end/s on top of the toggles, so the chain runs at about −1.9 end/s.
Transference covers it: it recharges in 8.6 s and Mids lists a 90.3-endurance return to the caster for each cast.

### Hit chance

The check is min(95%, accuracy multiplier × (75% + to-hit − 25%) × 0.65), with to-hit +22.13%.
The 0.65 level factor for +4 foes is approximate. The lowest multiplier that rolls is Blazing Bolt at 2.056, which needs 2.03.

| Power | Accuracy multiplier | Hit chance | Recharge (s) |
| --- | --- | --- | --- |
| Fire Blast | 2.172 | 95% | 1.10 |
| Blaze | 2.385 | 95% | 2.70 |
| Blazing Bolt | 2.056 | 95% | 3.73 |
| Fire Ball | 2.385 | 95% | 4.37 |
| Rain of Fire | 2.185 | 95% | 16.57 |
| Inferno | 3.041 | 95% | 39.86 |
| Transfusion | 2.715 | 95% | 2.45 |
| Transference | 2.205 | 95% | 8.64 |
| Siphon Power | 2.402 | 95% | 7.16 |
| Siphon Speed | 2.108 | 95% | 19.97 |
| Fulcrum Shift | 2.402 | 95% | 18.65 |

Speed Boost, Increase Density, Power Boost and Hasten do not roll to hit.

## Damage picture

- **Base numbers, without Fulcrum Shift** (Mids' per-activation damage, slotted, with the +11% damage from set bonuses; Scourge not modelled): Blazing Bolt 718, Blaze 451, Fire Blast 232, Fire Ball 212 per target, Rain of Fire 326 per target over its duration, Inferno 701 per target.
- **Single-target chain:** Blazing Bolt → Blaze → Fire Blast → Fire Blast, repeated. Each attack is ready when its turn comes, so the cycle has no gaps.
  It takes 5.68 s using arcana-rounded cast times (my arithmetic). That is about 241 damage per second at base numbers.
- **AoE opener:** Fulcrum Shift on the spawn the tank holds, then Rain of Fire, then Fire Ball. Inferno goes in when the spawn is bunched and the alpha strike has landed on the tank.
- **The damage cap:** `archetypes.json` gives the Corruptor `DamageCap` 5, which is 500% of base damage (+400% in buffs).
  Mids models Fulcrum Shift as self damage buffs of +40% and +20% for 45 s per foe hit, plus −20% damage on each foe for 30 s.
  With Fulcrum Shift on a full spawn, the Corruptor sits at or near the cap. At the cap the chain does roughly 2.4 times the base figure. That multiple is my arithmetic: 500% against the 206% already slotted and buffed.
  The data does not say whether both buff entries apply for every foe, so the exact number of foes needed is not resolved here.

## Kinetics duties

- **Fulcrum Shift:** two Accuracy and one Recharge Reduction. It lands at 95% and recharges in 18.7 s, well inside its 45 s buff, so it is always up.
- **Siphon Power:** two Accuracy, 95% to hit. It is the single-target damage buff for a lone boss when Fulcrum Shift has nothing to feed on.
- **Transference:** Preemptive Optimization, four pieces (EndMod/Accuracy/Endurance, EndMod/Accuracy/Recharge, EndMod/Recharge, EndMod/Endurance/Recharge). That gives accuracy, endurance modification and recharge in one power.
- **Speed Boost:** Preemptive Optimization, six pieces, five of them carrying EndMod. Mids gives each ally +50% recharge and +114.9% recovery for 120 s. It recharges in 0.55 s, so the whole team can be covered between spawns.
- **Increase Density:** Steadfast Protection: Resistance/+Def 3% as a mule. For a tank it gives +21.2% Smashing and +21.2% Energy resistance, hold, stun and immobilize protection (magnitude 10.4), knockback protection (magnitude 9) and teleport protection for 60 s. It recharges in 1.1 s.
- **Transfusion:** Touch of the Nictus, six pieces, accuracy first (Accuracy/Healing/Absorb, then Accuracy/Endurance/Healing/Absorb). It hits at 95%, because the heal only lands when the attack does.
- **Siphon Speed:** Pacing of the Turtle, five pieces (two carry accuracy). It hits at 95% and stays up permanently in combat.
- **Repel: skipped.** It is a knockback toggle that scatters spawns the tank has gathered, and the pick buys no defense.
- **Inertial Reduction: skipped.** It is a travel and jump buff for the team. Fly and Hover cover positioning, and the pick went to the Flight pool, which carries two Luck of the Gambler recharge pieces and defense.

## Powers by level, with slotting

Set pieces are level 50 or attuned. `(A)` is the free slot; numbers are the levels the added slots were placed at.

- **1 Fire Blast:** Apocalypse: Damage/Recharge/Accuracy (A), Recharge/Accuracy (3), Damage/Recharge (3), Damage/Endurance (36), Damage (45).
- **1 Transfusion:** Touch of the Nictus: Accuracy/Healing/Absorb (A), Accuracy/Endurance/Healing/Absorb (7), Accuracy/Endurance/Recharge (15), Healing/Absorb/Recharge (31), Healing/Absorb (39), Chance for Negative Energy Damage (48).
- **2 Fire Ball:** Superior Scourging Blast: Accuracy/Damage (A), Accuracy/Damage/RechargeTime (7), Accuracy/Damage/Endurance/RechargeTime (23), Damage/RechargeTime (36), Damage/Endurance/RechargeTime (45).
- **4 Maneuvers:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense (5), Defense/Endurance (5), Defense/Endurance/Recharge (37), Defense/Recharge (46).
- **6 Rain of Fire:** Bombardment: Accuracy/Recharge/Endurance (A), Accuracy/Damage/Recharge (9), Accuracy/Damage/Recharge/Endurance (15), Damage/Recharge (31), Damage (40), Chance for Fire Damage (48).
- **8 Siphon Power:** Invention: Accuracy (A), (9).
- **10 Siphon Speed:** Pacing of the Turtle: Accuracy/Slow (A), Accuracy/Endurance (11), Endurance/Recharge/Slow (25), Range/Slow (37), Damage/Slow (46).
- **12 Hover:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A); Kismet: Accuracy +6% (13).
- **14 Boxing:** Invention: Accuracy (A). It is the pool prerequisite for Tough and Weave.
- **16 Tough:** Gladiator's Armor: Resistance (A), Resistance/Recharge/Endurance (17), End/Resist (17), Recharge/Resist (33), Recharge/Endurance (40), TP Protection +3% Def (All) (48).
- **18 Blaze:** Superior Malice of the Corruptor: Accuracy/Damage (A), Accuracy/Damage/Recharge (19), Accuracy/Damage/Endurance/Recharge (19), Damage/Recharge (34), Damage/Endurance/Recharge (40), Recharge/Chance for Negative Energy Damage (50).
- **20 Speed Boost:** Preemptive Optimization: EndMod/Recharge (A), EndMod/Endurance/Recharge (21), EndMod/Endurance (21), EndMod/Accuracy/Endurance (34), EndMod/Accuracy/Recharge (43), Accuracy/Recharge (50).
- **22 Weave:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A); Shield Wall: +Res (Teleportation), +5% Res (All) (23); Reactive Defenses: Scaling Resist Damage (37).
- **24 Hasten:** Invention: Recharge Reduction (A), (25).
- **26 Blazing Bolt:** Sting of the Manticore: Accuracy/Damage (A), Accuracy/Interrupt/Range (27), Damage/Endurance (27), Damage/Endurance/Recharge (34), Damage/Interrupt/Recharge (43), Chance of Damage (Toxic) (50).
- **28 Transference:** Preemptive Optimization: EndMod/Accuracy/Endurance (A), EndMod/Accuracy/Recharge (29), EndMod/Recharge (29), EndMod/Endurance/Recharge (43).
- **30 Fulcrum Shift:** Invention: Accuracy (A), Accuracy (31), Recharge Reduction (39).
- **32 Inferno:** Armageddon: Damage/Recharge/Accuracy (A), Recharge/Accuracy (33), Damage/Recharge (33), Damage/Endurance (45).
- **35 Dark Embrace:** Unbreakable Guard: Resistance (A), Resistance/Endurance (36), Resistance/Endurance/RechargeTime (39).
- **38 Power Boost:** Invention: Recharge Reduction (A).
- **41 Tactics:** Gaussian's Synchronized Fire-Control: To Hit Buff (A), To Hit Buff/Recharge (42), To Hit Buff/Recharge/Endurance (42), To Hit Buff/Endurance (42); Invention: To Hit Buff (46).
- **44 Increase Density:** Steadfast Protection: Resistance/+Def 3% (A).
- **47 Fly:** Blessing of the Zephyr: Knockback Reduction (4 points) (A).
- **49 Evasive Maneuvers:** Luck of the Gambler: Defense/Increased Global Recharge Speed (A).
- **Health:** Numina's Convalescence: +Regeneration/+Recovery (A); Miracle: +Recovery (11).
- **Stamina:** Performance Shifter: Chance for +End (A), EndMod (13).

Slots: 67 added, 24 picks, 4 pools. The validator reports no violations.
Exemplar 10: Fire Blast and Maneuvers hold two added slots by level 5. Fire Ball and Rain of Fire are the AoE attacks, and Hover is the travel power at 12.

### Verbatim MidsReborn export (power lines)

```text
Level 1:	Fire Blast			Apc-Acc/Dmg/Rchg(A), Apc-Acc/Rchg(3), Apc-Dmg/Rchg(3), Apc-Dmg/EndRdx(36), Apc-Dmg(45)
Level 1:	Transfusion			TchoftheN-Acc/Heal(A), TchoftheN-Acc/EndRdx/Heal/HP/Regen(7), TchoftheN-Acc/EndRdx/Rchg(15), TchoftheN-Heal/HP/Regen/Rchg(31), TchoftheN-Heal(39), TchoftheN-%Dam(48)
Level 2:	Fire Ball			SprScrBls-Acc/Dmg(A), SprScrBls-Acc/Dmg/Rchg(7), SprScrBls-Acc/Dmg/EndRdx/Rchg(23), SprScrBls-Dmg/Rchg(36), SprScrBls-Dmg/EndRdx/Rchg(45)
Level 4:	Maneuvers			LucoftheG-Def/Rchg+(A), LucoftheG-Def(5), LucoftheG-Def/EndRdx(5), LucoftheG-Def/EndRdx/Rchg(37), LucoftheG-Def/Rchg(46)
Level 6:	Rain of Fire			Bmbdmt-Acc/Rech/End(A), Bmbdmt-Acc/Dam/Rech(9), Bmbdmt-Acc/Dam/Rech/End(15), Bmbdmt-Dam/Rech(31), Bmbdmt-Dam(40), Bmbdmt-+FireDmg(48)
Level 8:	Siphon Power			Acc-I(A), Acc-I(9)
Level 10:	Siphon Speed			PcnoftheT-Acc/Slow(A), PcnoftheT-Acc/EndRdx(11), PcnoftheT-EndRdx/Rchg/Slow(25), PcnoftheT-Rng/Slow(37), PcnoftheT-Dmg/Slow(46)
Level 12:	Hover				LucoftheG-Def/Rchg+(A), Ksm-ToHit+(13)
Level 14:	Boxing				Acc-I(A)
Level 16:	Tough				GldArm-ResDam(A), GldArm-Res/Rech/End(17), GldArm-End/Res(17), GldArm-RechRes(33), GldArm-RechEnd(40), GldArm-3defTpProc(48)
Level 18:	Blaze				SprMlcoft-Acc/Dmg(A), SprMlcoft-Acc/Dmg/Rchg(19), SprMlcoft-Acc/Dmg/EndRdx/Rchg(19), SprMlcoft-Dmg/Rchg(34), SprMlcoft-Dmg/EndRdx/Rchg(40), SprMlcoft-Rchg/Dmg%(50)
Level 20:	Speed Boost			PreOptmz-EndMod/Rech(A), PreOptmz-EndMod/End/Rech(21), PreOptmz-EndMod/End(21), PreOptmz-EndMod/Acc/End(34), PreOptmz-EndMod/Acc/Rech(43), PreOptmz-Acc/Rech(50)
Level 22:	Weave				LucoftheG-Def/Rchg+(A), ShlWal-ResDam/Re TP(23), Rct-ResDam%(37)
Level 24:	Hasten				RechRdx-I(A), RechRdx-I(25)
Level 26:	Blazing Bolt			StnoftheM-Acc/Dmg(A), StnoftheM-Acc/ActRdx/Rng(27), StnoftheM-Dmg/EndRdx(27), StnoftheM-Dmg/EndRdx/Rchg(34), StnoftheM-Dmg/ActRdx/Rchg(43), StnoftheM-Dam%(50)
Level 28:	Transference			PreOptmz-EndMod/Acc/End(A), PreOptmz-EndMod/Acc/Rech(29), PreOptmz-EndMod/Rech(29), PreOptmz-EndMod/End/Rech(43)
Level 30:	Fulcrum Shift			Acc-I(A), Acc-I(31), RechRdx-I(39)
Level 32:	Inferno				Arm-Acc/Dmg/Rchg(A), Arm-Acc/Rchg(33), Arm-Dmg/Rchg(33), Arm-Dmg/EndRdx(45)
Level 35:	Dark Embrace			UnbGrd-ResDam(A), UnbGrd-ResDam/EndRdx(36), UnbGrd-ResDam/EndRdx/Rchg(39)
Level 38:	Power Boost			RechRdx-I(A)
Level 41:	Tactics				GssSynFr--ToHit(A), GssSynFr--ToHit/Rchg(42), GssSynFr--ToHit/Rchg/EndRdx(42), GssSynFr--ToHit/EndRdx(42), ToHit-I(46)
Level 44:	Increase Density		StdPrt-ResDam/Def+(A)
Level 47:	Fly				BlsoftheZ-ResKB(A)
Level 49:	Evasive Maneuvers		LucoftheG-Def/Rchg+(A)
Level 1:	Health				NmnCnv-Regen/Rcvry+(A), Mrc-Rcvry+(11)
Level 1:	Stamina				PrfShf-End%(A), PrfShf-EndMod(13)
```

## Incarnates (all tier 4; Lore left empty, as 4-star bans pets)

- **Alpha: Agility Core Paragon.** Measured against Resilient Core Paragon on this build. Agility gives 50.3% Ranged defense, 63.0% Smashing/Lethal resistance and 40.0% Negative resistance. Resilient gives 46.1%, 71.2% and 43.0%. Defense above the soft cap comes first in the 4-star guides, and Agility is what the posted 4-star build runs.
- **Judgement: Pyronic Core Final Judgement.** A ranged fire AoE on a 4-star-proven Fire/Kinetics build and its budget copy.
- **Interface: Reactive Radial Flawless Interface.** A −resistance and fire damage-over-time proc on every attack, as the posted 4-star build runs it.
- **Destiny: Barrier Core Epiphany.** It is the answer to a defense-debuff cascade and the Negative-resistance gap. It is off in the totals.
  An experienced hard-mode player reports this practice. Barrier is the best all-round Destiny for hard mode.
  On an eight-player team, six take Barrier and two float (Clarion, Rebirth or Ageless). With six Barriers the rotation is two at once every 30 seconds.
  Clarion is situational, for a team with little mez protection.
  One or two stars up to level 54 can manage with about four Barrier holders. Three and four stars at level 55 bring large increases in enemy accuracy, damage, hit points, regeneration and defense.
  This build takes Barrier Core to be one of the six.
- **Hybrid: Support Core Embodiment** (`Incarnate.Hybrid.Support_Genome_8`; the Mids database names it Support Core Embodiment). Under Fulcrum Shift the Corruptor already sits near its damage cap, so Assault's damage is mostly wasted. Support's team buffs are the second choice the posted build lists. It is off in the totals.

## Trade-offs

- **Negative resistance 40.0% against the 50% target.** Dark Embrace gives 17.7%. The rest comes from Shield Wall +5%, Reactive Defenses +3%, and the six-piece bonuses of Touch of the Nictus and Bombardment (+4.5% each).
  Pacing of the Turtle at five pieces adds +3.75% and Unbreakable Guard at three adds +2.25%. Each remaining option was measured or priced:
  - Resilient Core Alpha: +3.0 points (43.0%), costs 4.2 points of Ranged defense.
  - Shield Wall four-piece in Maneuvers in place of Luck of the Gambler: +4.5 points. It loses the Luck of the Gambler +9% accuracy, and Blazing Bolt drops under 95% to hit.
  - Closing the full 10 points needs both of those plus another +4.5% six-piece set. There is no attack or toggle left to hold one without giving up the ranged-defense or accuracy targets.
  Barrier, a teammate's Increase Density or other resistance buffs, and inspirations cover it in the Negative-heavy new ITF.
- **Three Superior Malice of the Corruptor sets would be better, but the game allows one.** Archetype-set pieces are unique per build. Blaze holds the one Superior Malice set and Fire Ball the one Superior Scourging Blast set.
  Fire Blast takes Apocalypse for its recharge bonus. Blazing Bolt takes Sting of the Manticore for Ranged defense and two accuracy pieces.
- **Smashing/Lethal 75% stretch not reached (63.0%).** Resilient Core reaches 71.2% at the cost above. Increase Density on self adds +21.2% Smashing (not Lethal) while it lasts.
- **Fulcrum Shift slotted for accuracy over recharge.** One Recharge Reduction is enough to keep its 45 s buff permanent. The second Accuracy keeps it at 95% to hit.
- **Fire Ball's sixth Superior Scourging Blast piece traded for an Invention: To Hit Buff in Tactics.** That lifts to-hit from +21.75% to +22.13%, over the +22% fast-snipe line for Blazing Bolt. It costs 5 points of AoE defense and 2.5 of Fire/Cold defense.
- **Fly and Evasive Maneuvers over Aim and Flares.** Evasive Maneuvers needs two Flight powers. The pair gives real travel, a fourth Luck of the Gambler recharge piece and more defense. Aim would add burst that Fulcrum Shift already caps, and the chain has no gap for Flares to fill.
- **Hit points at 77% of the cap.** Unbreakable Guard's +Max HP and more Superior Malice sets were the usual sources. They are blocked by the slot budget and the archetype-set uniqueness.

## 4-star play

- Stay at hover range behind the tank. Apply Increase Density to the tank before the pull, then Speed Boost on everyone.
- Opener: Fulcrum Shift on the held spawn, Rain of Fire, Fire Ball. Inferno goes in only once the alpha has landed on the tank and the spawn is bunched.
- Single target: Blazing Bolt, Blaze, Fire Blast, Fire Blast. Siphon Power on the boss when Fulcrum Shift has few bodies to feed on.
- Hit Siphon Speed at least once a minute: it is what makes Hasten permanent.
- Power Boost for the hard spawns: Ranged defense goes from 50% to 67% for 15 s and Transfusion heals harder. Hard-mode defense needs to sit well above 45%.
- Use Transference whenever the blue bar is under half. The chain runs about 1.9 end/s short without it.
- Negative energy is the hole (40% resistance). Stagger Barrier with the team's other Barrier holders for the Negative-heavy fights in the new ITF.

## What I would verify

- **Blazing Bolt's fast-snipe cast in game.** Mids shows a 1.67 s cast at both +21.75% and +22.13% to-hit. The build sits just over +22% to be safe.
- **How many foes Fulcrum Shift needs to cap damage.** Mids lists two self buff entries (+40% and +20%) per foe. Check the combat log for the real stack.
- **Transference's endurance return.** The 90.3 figure is Mids' entry for the caster. Check it in game.
- **The 0.65 level factor** in the hit-chance check is approximate.

## Guides used (paraphrased, no quotes)

Under `homecoming-source-material/community/archetypes/`:

- `corruptor/build-voltak-s-fire-kinetics-4-star-itf-and-aeon-proven-farm.md` — "BUILD - Fire/Kinetics - 4 Star ITF and AEON Proven, Farm Proven, even Solo's Rikti 54x8 NO INSPIRATIONS, NO DEATHS." Not a proc build, hard-mode defense well over 45, Power Boost on hard content, Soul Mastery, Hover and Fly, Agility, Pyronic and Reactive.
- `corruptor/villianously-cost-effective-fire-blast-kinetics-damage-force.md` — "Villianously Cost Effective - Fire Blast Kinetics (Damage & Force Multiplier)." A budget copy of that build: Preemptive Optimization in Speed Boost and Transference, Unbreakable Guard in Dark Embrace, Support Core Hybrid.
- `corruptor/new-version-of-darkir-s-kintank-blow-up-and-survive-hardcap.md` — "New Version of [author]'s Kintank, blow **** up and survive (hardcap+softcap s/l and ranged def, 200%+ rech)." Epic pool trade-offs.
- `corruptor/my-fire-kin-mace-45-s-l-def-50-s-l-res-and-over-1500-hp-with.md` — "My Fire/Kin/Mace 45% s/l def, 50% s/l res, and over 1500 hp without incarnate." Superior Malice in Blaze and Siphon Speed for recharge.
- `corruptor/the-boombox-fire-kin.md` — "The BoomBox - Fire/Kin." Use Speed Boost and Increase Density on the team; Fulcrum Shift with Inferno.
- `defender/kin-archer-usual-softcaps.md` — "Kin/Archer. Usual softcaps." Do not let Speed Boost drop; follow the tank; flying out of range is the real defense under focused fire.
- `defender/using-the-empathy-defender-in-itf-4-star-and-aeon-r-part-2-b.md` — "Using the Empathy Defender in ITF 4 Star and AEON R (Part 2)." Ranged soft cap on a support character; Power Boost doubles the buffs; Soul Mastery.
- `tanker/inv-rad-hardmode-version-i-hope.md` — "Inv/Rad: Hardmode version (I hope?)." Hard-mode foes are +4 with about +25% defense, so accuracy matters; psionic resistance is not needed there.

## Files

Sibling files in this folder, all named "fire-kinetics-4-star" plus an ending:

- `.mbd`
- `.skif`
- `.md`
- `.sidekick-link.txt`
- `.mids-totals.json`
- `.sidekick-totals.txt`

The Sidekick share link is in the file `fire-kinetics-4-star.sidekick-link.txt`.
