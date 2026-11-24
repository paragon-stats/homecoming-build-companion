# Shield Defense-Fiery Melee Tanker - 4-Star

Level 50 Tanker, Shield Defense / Fiery Melee, pools Speed, Fighting and Leaping, ancillary Energy Mastery. This is a damage and clear-speed tank: it tanks 4-star task forces first and then puts as much as it can into AoE and sustained single-target damage.

Every number below comes from MidsReborn's own totals (`totals.json`, `TotalsCapped`) for the `.mbd` in this folder unless it is marked as hand-computed.

## Why this pairing

- **Shield Defense is a top Tanker armor in two captured rankings.** It is first in one ranking and third in another: "Infinitum Build Collection" (`community/archetypes/tanker/infinitum-build-collection.md`) and "Azrael Tank Ratings." (`community/archetypes/tanker/azrael-tank-ratings.md`).
- **It resists defense debuffs.** Three powers carry defense-debuff resistance. The guides name defense debuffs as what kills tanks at 4 stars.
- **A Shield tank has been posted for 4-star play.** One captured Shield tank build tanks the 4-star Imperious task force: "Build Guide: Shield Defense/Broadsword able to handle any content" (`community/archetypes/tanker/build-guide-shield-defense-broadsword-able-to-handle-any-con.md`).
- **It adds damage.** Against All Odds is a damage buff, and Shield Charge is a long-range AoE that teleports to its target. "Proc Monsters - Tanker Edition" (`community/archetypes/tanker/proc-monsters-tanker-edition.md`) calls out the damage boost and the AoE.
- **Fiery Melee is the top Tanker secondary for clear speed.** It also has the best pylon/clear average in the 2023 min-max melee comparison. Tanker figures: Fiery Melee pylon 2:08, mission clear 4:25, average 3:17; Battle Axe is the runner-up at 3:26. Source: "Minmax Melee Archetype Comparison & Tier List" (`community/mechanics/minmax-melee-archetype-comparison-tier-list.md`). The thread's tables are images, so these numbers were read from the live thread.

### Shield Defense names in this Mids database

Mids' internal power names and its display names differ here. This document uses the display names that the forum export shows:

| Display name (used here) | Internal name in `powers.json` | What it does in this database |
| --- | --- | --- |
| Battle Agility | `Shield_Defense.Deflection` | Toggle: Ranged and AoE defense, plus defense-debuff resistance (enhanceable) |
| Deflection | `Shield_Defense.Active_Defense` | Toggle: Melee and Psionic defense, plus Smashing/Lethal resistance |
| Active Defense | `Shield_Defense.Battle_Agility` | Click, 120 s, stacks: mez protection, plus defense-debuff, to-hit-debuff and slow resistance |

## Assumptions behind the totals

- **Solo.** Phalanx Fighting is counted with zero allies (its 5% base only). Against All Odds is counted at one foe, the minimum Mids applies.
- **Counted:** every toggle and auto power, the Hybrid toggle and the Alpha. Active Defense is counted as one stack.
- **Not counted:** Build Up, Hasten, One with the Shield and the Destiny. Barrier is a click whose opening value is not a standing buff. The writer marks every incarnate `StatInclude`, so `write.py` clears it on the Destiny entry; that is a deviation, listed in Trade-offs.
- **Hit chance is hand-computed** (approximate; verify in game). Hit chance = min(95%, Accuracy × min(95%, 75% + ToHit − 25%) × 0.65). The 0.65 is the purple-patch to-hit factor for a +4 foe, which counts as +3 after the Alpha level shift. The 25% is the +25% foe defense.

## Targets and achieved numbers

| Target | Goal | Achieved (Mids) | Met |
| --- | --- | --- | --- |
| Melee defense | ≥ 50% | 61.12% | yes |
| Ranged defense | ≥ 50% | 52.03% | yes |
| AoE defense | ≥ 50% | 50.78% | yes, thin margin |
| Defense-debuff resistance | state it, keep every source | 66.59% with one Active Defense stack; 88.2% while two stacks overlap, about 47% of the time (hand-added) | yes |
| Smashing/Lethal resistance | ≥ 70% (stretch 80%) | 73.60% | yes; stretch not met |
| Energy/Negative resistance | ≥ 45% | 46.43% | yes |
| Fire/Cold resistance | ≥ 45% | 57.68% | yes |
| Toxic / Psionic resistance | state honestly | 56.93% / 31.15% | — |
| Hit points | as high as the defense targets allow | 2,447.9 (Tanker cap 3,534) | below cap |
| Global recharge | state it; Hasten if slots allow | +62.5% without Hasten. Hasten is taken with one Recharge IO: 219.6 s recharge, about 55% uptime | Hasten taken |
| Global accuracy / ToHit | carry both | +33% accuracy; +31% ToHit (Focused Accuracy +25%, Kismet +6%) | yes |
| Hit chance vs +4, +25% defense | ≥ 95% on every attack | 95% on every attack (Shield Charge, the lowest, computes to 96.4% before the cap). Taunt 89% | yes; Taunt is not an attack |
| Endurance, toggles only | positive | Recovery +91.44% (≈ 3.31 end/s hand-computed from 103.6 max endurance); toggles 1.012 end/s; net **+2.29 end/s** | yes |
| Endurance, attack chain running | positive | Chain 2.43 end/s (hand-computed); net **−0.135 end/s** without the Destiny | see note |
| Single-target chain | gapless | Greater Fire Sword → Fire Sword → Scorch → Incinerate, 6.20 s cycle. Greater Fire Sword recharges in 4.57 s against a 4.62 s window | yes |

**Endurance note.** Solo, with the chain running non-stop, the deficit is 0.135 end/s, so the bar lasts about 12.8 minutes. A blue inspiration, or any teammate's recovery or endurance buff, covers it. Solo, one blue inspiration every 12 minutes is enough. Mids does not count the Performance Shifter and Panacea endurance procs.

**Active Defense uptime.** Two Recharge IOs and the global recharge bring Active Defense to an 81.4 s recharge against its 120 s duration. One stack is up 100% of the time, and two stacks overlap for 38.6 s of every 81.4 s (about 47%). Every defense-debuff resistance source is kept:

- Battle Agility: 27.66%, enhanced by its Shield Wall pieces.
- Grant Cover: 17.3%. Grant Cover is taken for its own defense-debuff and recharge-debuff resistance, because its defense goes only to allies.
- Active Defense: 21.62% per stack.

**Damage per hit.** These are Mids `DamagePerActivation` values, without procs:

- Greater Fire Sword 515.5
- Incinerate 469.5
- Fire Sword 288.2
- Scorch 207.7
- Shield Charge 651.2 (44.5 s recharge)
- Fire Sword Circle 365.1 (7.9 s)
- Combustion 337.7 (6.4 s)

The single-target chain averages about 239 damage per second before procs (hand-computed).

## Power order and slotting

Slot levels are the ones the writer placed, read back from the forum export. Full set names; "(A)" is the free slot.

| Level | Power | Slotting |
| --- | --- | --- |
| 1 | Battle Agility | Shield Wall: Defense/Recharge (A), Defense (3), Defense/Endurance (7), Defense/Endurance/Recharge (21), +Res (Teleportation), +5% Res (All) (36); Luck of the Gambler: Defense/Increased Global Recharge Speed (45) |
| 1 | Scorch | Superior Blistering Cold: Accuracy/Damage (A), Accuracy/Damage/Recharge (3), Damage/Endurance (7), Accuracy/Damage/Endurance (21), Damage/Endurance/Accuracy/RechargeTime (36), Recharge/Chance for Hold (45) |
| 2 | Deflection | Luck of the Gambler: Defense/Recharge (A), Defense (5), Defense/Endurance (9), Defense/Increased Global Recharge Speed (27); Steadfast Protection: Resistance/+Def 3% (36), Resistance/Endurance (46) |
| 4 | Fire Sword | Superior Gauntleted Fist: Accuracy/Damage (A), Accuracy/Damage/RechargeTime (5), Damage/RechargeTime (9), Damage/Endurance/RechargeTime (27), Accuracy/Damage/Endurance/RechargeTime (37), RechargeTime/+Absorb (46) |
| 6 | True Grit | Unbreakable Guard: Resistance (A), Resistance/Endurance (11), RechargeTime/Resistance (17), Resistance/Endurance/RechargeTime (34), +Max HP (45) |
| 8 | Against All Odds | Endurance Reduction IO (A) |
| 10 | Combustion | Superior Avalanche: Accuracy/Damage (A), Accuracy/Damage/Recharge (11), Damage/Endurance (15), Accuracy/Damage/Endurance (31), Accuracy/Damage/Endurance/Recharge (37), Recharge/Chance for Knockdown (46) |
| 12 | Super Speed | Winter's Gift: Slow Resistance (20%) (A) |
| 14 | Active Defense | Recharge Reduction IO (A), Recharge Reduction IO (15) |
| 16 | Taunt | Perfect Zinger: Accuracy/Recharge (A), Threat/Placate/Recharge (17), Threat/Placate/Recharge/Range (19), Chance for Psi Damage (43) |
| 18 | Grant Cover | Reactive Defenses: Scaling Resist Damage (A) |
| 20 | Build Up | Gaussian's Synchronized Fire-Control: Chance for Build Up (A) |
| 22 | Shield Charge | Obliteration: Accuracy/Damage/Recharge (A), Accuracy/Damage/Endurance/Recharge (23), Chance for Smashing Damage (23); Eradication: Chance for Energy Damage (33); Scirocco's Dervish: Chance of Damage (Lethal) (37); Fury of the Gladiator: Chance for Res Debuff (48) |
| 24 | Fire Sword Circle | Armageddon: Damage/Recharge/Accuracy (A), Recharge/Accuracy (25), Damage/Recharge (25), Damage (33), Damage/Endurance (40), Chance for Fire Damage (48) |
| 26 | One with the Shield | Resist Damage IO (A) |
| 28 | Incinerate | Mako's Bite: Accuracy/Damage (A), Accuracy/Damage/Endurance/Recharge (29), Damage/Endurance (29), Damage/Recharge (33), Accuracy/Endurance/Recharge (40), Chance of Damage (Lethal) (50) |
| 30 | Greater Fire Sword | Superior Might of the Tanker: Accuracy/Damage (A), Accuracy/Damage/Recharge (31), Damage/Recharge (31), Damage/Endurance/Recharge (34), Accuracy/Damage/Endurance/Recharge (40), Recharge/Chance for +Res (All) (50) |
| 32 | Phalanx Fighting | Luck of the Gambler: Defense/Increased Global Recharge Speed (A) |
| 35 | Boxing | Accuracy IO (A) |
| 38 | Tough | Gladiator's Armor: TP Protection +3% Def (All) (A), Resistance (39), Resistance/Rech/End (39), Recharge/Resist (39), End/Resist (42), Recharge/Endurance (50) |
| 41 | Weave | Luck of the Gambler: Defense/Increased Global Recharge Speed (A), Defense (42), Defense/Endurance (42), Defense/Recharge (43) |
| 44 | Focused Accuracy | Endurance Reduction IO (A) |
| 47 | Combat Jumping | Luck of the Gambler: Defense/Increased Global Recharge Speed (A); Kismet: Accuracy +6% (48) |
| 49 | Hasten | Recharge Reduction IO (A) |
| 1 | Health | Panacea: +Hit Points/Endurance (A); Numina's Convalescence: +Regeneration/+Recovery (13); Miracle: +Recovery (34) |
| 1 | Stamina | Performance Shifter: Chance for +End (A), EndMod (13), EndMod/Recharge (19), EndMod/Accuracy (43) |
| 1 | Sprint | Celerity: +Stealth (A) |

Budget: 24 picks, 67 of 67 added slots, no power over 6 slots, four pools (Speed, Fighting, Leaping, Energy Mastery). Every attack carries accuracy in slot (A) and in its first added slot.

Mids forum export, verbatim:

```text
Level 1:	Battle Agility			ShlWal-Def/Rchg(A), ShlWal-Def(3), ShlWal-Def/EndRdx(7), ShlWal-Def/EndRdx/Rchg(21), ShlWal-ResDam/Re TP(36), LucoftheG-Def/Rchg+(45)
Level 1:	Scorch				SprBlsCol-Acc/Dmg(A), SprBlsCol-Acc/Dmg/Rchg(3), SprBlsCol-Dmg/EndRdx(7), SprBlsCol-Acc/Dmg/EndRdx(21), SprBlsCol-Dmg/EndRdx/Acc/Rchg(36), SprBlsCol-Rchg/HoldProc(45)
Level 2:	Deflection			LucoftheG-Def/Rchg(A), LucoftheG-Def(5), LucoftheG-Def/EndRdx(9), LucoftheG-Def/Rchg+(27), StdPrt-ResDam/Def+(36), StdPrt-ResDam/EndRdx(46)
Level 4:	Fire Sword			SprGntFis-Acc/Dmg(A), SprGntFis-Acc/Dmg/Rchg(5), SprGntFis-Dmg/Rchg(9), SprGntFis-Dmg/EndRdx/Rchg(27), SprGntFis-Acc/Dmg/EndRdx/Rchg(37), SprGntFis-Rchg/+Absorb(46)
Level 6:	True Grit			UnbGrd-ResDam(A), UnbGrd-ResDam/EndRdx(11), UnbGrd-Rchg/ResDam(17), UnbGrd-ResDam/EndRdx/Rchg(34), UnbGrd-Max HP%(45)
Level 8:	Against All Odds		EndRdx-I(A)
Level 10:	Combustion			SprAvl-Acc/Dmg(A), SprAvl-Acc/Dmg/Rchg(11), SprAvl-Dmg/EndRdx(15), SprAvl-Acc/Dmg/EndRdx(31), SprAvl-Acc/Dmg/EndRdx/Rchg(37), SprAvl-Rchg/KDProc(46)
Level 12:	Super Speed			WntGif-ResSlow(A)
Level 14:	Active Defense			RechRdx-I(A), RechRdx-I(15)
Level 16:	Taunt				PrfZng-Acc/Rchg(A), PrfZng-Taunt/Rchg(17), PrfZng-Taunt/Rchg/Rng(19), PrfZng-Dam%(43)
Level 18:	Grant Cover			Rct-ResDam%(A)
Level 20:	Build Up			GssSynFr--Build%(A)
Level 22:	Shield Charge			Obl-Acc/Dmg/Rchg(A), Obl-Acc/Dmg/EndRdx/Rchg(23), Obl-%Dam(23), Erd-%Dam(33), ScrDrv-Dam%(37), FuroftheG-ResDeb%(48)
Level 24:	Fire Sword Circle		Arm-Acc/Dmg/Rchg(A), Arm-Acc/Rchg(25), Arm-Dmg/Rchg(25), Arm-Dmg(33), Arm-Dmg/EndRdx(40), Arm-Dam%(48)
Level 26:	One with the Shield		ResDam-I(A)
Level 28:	Incinerate			Mk'Bit-Acc/Dmg(A), Mk'Bit-Acc/Dmg/EndRdx/Rchg(29), Mk'Bit-Dmg/EndRdx(29), Mk'Bit-Dmg/Rchg(33), Mk'Bit-Acc/EndRdx/Rchg(40), Mk'Bit-Dam%(50)
Level 30:	Greater Fire Sword		SprMghoft-Acc/Dmg(A), SprMghoft-Acc/Dmg/Rchg(31), SprMghoft-Dmg/Rchg(31), SprMghoft-Dmg/EndRdx/Rchg(34), SprMghoft-Acc/Dmg/EndRdx/Rchg(40), SprMghoft-Rchg/Res%(50)
Level 32:	Phalanx Fighting		LucoftheG-Def/Rchg+(A)
Level 35:	Boxing				Acc-I(A)
Level 38:	Tough				GldArm-3defTpProc(A), GldArm-ResDam(39), GldArm-Res/Rech/End(39), GldArm-RechRes(39), GldArm-End/Res(42), GldArm-RechEnd(50)
Level 41:	Weave				LucoftheG-Def/Rchg+(A), LucoftheG-Def(42), LucoftheG-Def/EndRdx(42), LucoftheG-Def/Rchg(43)
Level 44:	Focused Accuracy		EndRdx-I(A)
Level 47:	Combat Jumping			LucoftheG-Def/Rchg+(A), Ksm-ToHit+(48)
Level 49:	Hasten				RechRdx-I(A)
Level 1:	Health				Pnc-Heal/+End(A), NmnCnv-Regen/Rcvry+(13), Mrc-Rcvry+(34)
Level 1:	Stamina				PrfShf-End%(A), PrfShf-EndMod(13), PrfShf-EndMod/Rchg(19), PrfShf-EndMod/Acc(43)
Level 50:	Cardiac Core Paragon
Level 50:	Pyronic Core Final Judgement
Level 50:	Reactive Radial Flawless Interface
Level 50:	Barrier Core Epiphany
Level 50:	Assault Radial Embodiment
```

### Why the epic is Energy Mastery: Focused Accuracy

- **It is what holds every attack at 95% against +4 foes with +25% defense.** Focused Accuracy adds +25% ToHit.
- **Without it, ToHit falls to +6%.** Hit chance would drop to 67% on Shield Charge, 77% on Incinerate and 83% on Fire Sword.
- **It fits the budget.** It costs one pick and 0.205 end/s.
- **A damage epic would cost the defense targets.** Soul Mastery would need two picks (Gloom and Dark Obliteration), and the spare pick does not exist without dropping Weave or Combat Jumping, which hold the ranged and AoE defense targets.

## Incarnates (T4; Lore left empty)

- **Alpha: Cardiac Core Paragon.** Re-measured in Mids on the final slotting with Barrier, same picks and slots. Sustained is chain damage × solo chain uptime, where uptime = (recovery − toggles) ÷ chain cost; all are hand-computed from Mids values.

  | Alpha | Chain damage/s | Solo net with chain | Sustained solo | Targets |
  | --- | --- | --- | --- | --- |
  | Musculature Core | 261 | −1.14 end/s | ≈ 165 | fails: S/L 68.45%, E/N 44.28% |
  | Agility Core | 235 | −0.99 end/s | ≈ 160 | fails: S/L 68.45%, E/N 44.28% (Ranged 55.4%, AoE 54.15%) |
  | Cardiac Core | 239 | −0.135 end/s | ≈ 225 | all met |

  Cardiac is the only one that keeps every target met. It also has the best sustained damage solo. With a teammate's buff or an inspiration covering endurance, Musculature's 261 would win only if the 1.5 points of S/L and 0.7 points of E/N resistance were rebuilt elsewhere.

- **Judgement: Pyronic Core Final Judgement.** A fire AoE nuke, 678 damage per hit in Mids, every 90 s. It is the biggest single clear button the build has.
- **Interface: Reactive Radial Flawless Interface.** It adds a resistance debuff and fire damage over time to every attack. That helps the 16-target Tanker AoEs and the team more than Degenerative's maximum-hit-point debuff helps a single boss.
- **Destiny: Barrier Core Epiphany.** Barrier is the 4-star panic button the guides recommend. It is not counted in the totals: its opening values decay over 2 minutes, so `StatInclude` is off.
  An experienced hard-mode player reports this practice. Barrier is the best all-round Destiny for hard mode.
  On an eight-player team, six take Barrier and two float (Clarion, Rebirth or Ageless). With six Barriers the rotation is two at once every 30 seconds.
  Clarion is situational, for a team with little mez protection.
  One or two stars up to level 54 can manage with about four Barrier holders. Three and four stars at level 55 bring large increases in enemy accuracy, damage, hit points, regeneration and defense.
  This build takes Barrier Core to be one of the six.
  Solo, with the attack chain running non-stop, endurance nets about −0.135 per second, so the bar lasts about 13 minutes. A blue inspiration or any teammate's recovery or endurance buff covers it.
  Alternative: Ageless Core Epiphany, for runs where the team already has its six Barriers. It removes the endurance deficit. It gives up the tank's own Barrier.
- **Hybrid: Assault Radial Embodiment.** A free toggle that adds damage. Melee Core's scaling resistance and defense is not needed to meet the targets.

## Trade-offs

- **Cardiac over Musculature.** About 9% more damage per hit was given up. Musculature fails the S/L and E/N targets and runs the bar dry solo (−1.14 end/s with the chain running).
- **Barrier over Ageless.** The build gives up Ageless's endurance and +70% recharge burst for the team panic button. Solo, that leaves a 0.135 end/s deficit with the chain running, which a blue inspiration or a teammate's recovery or endurance buff covers.
- **Breath of Fire is not taken.** Its pick went to Focused Accuracy, because the to-hit target needs it. The cone would add a fourth AoE, at the cost of 95% hit chance on every attack.
- **Mako's Bite in Incinerate instead of Hecatomb.**
  - Hecatomb gives +15% accuracy, +10% recharge and +6% Fire/Cold resistance.
  - Mako's Bite gives +3.75% ranged defense and +3.75% Smashing/Lethal resistance.
  - Ranged defense is the scarce one: without Mako's Bite it falls to 48%.
- **The 67th slot went to a second Recharge IO in Active Defense.** One run each through the loop compared four places for it:
  - Active Defense: two-stack overlap 23% → 47%. Chosen.
  - Health, Miracle: Heal: +0.043 end/s.
  - Stamina, Performance Shifter: EndMod/Accuracy/Recharge: +0.025 end/s and +2.5% damage.
  - Hasten: uptime 55% → 66%.
  
  Neither endurance option makes the chain net-positive. The best one leaves it at −0.092 end/s. Doubling the time at 88% defense-debuff resistance buys the most against the 4-star failure mode.
- **Melee defense is about 11 points over target.** The excess comes with the attack sets (Superior Gauntleted Fist, Superior Avalanche, Superior Blistering Cold, Unbreakable Guard). It cannot be moved to ranged or AoE without losing those sets' resistance bonuses.
- **Hit points sit about 1,086 below the cap.** Raising them would mean trading the Luck of the Gambler or Superior set bonuses that hold the ranged and AoE defense targets.
- **The writer forces every incarnate `StatInclude`.** `write.py` sets it to false on the Destiny entry after writing, so Barrier is not totalled as a permanent buff.

## Playing it at 4 stars

1. **Open each pull with Shield Charge** into the spawn, then Fire Sword Circle and Combustion. Against All Odds and Taunt hold the aggro, and the 16-target caps tag the whole group.
2. **Re-click Active Defense on cooldown, about every 81 s.** Its 120 s duration means one stack is always up, and two stacks overlap almost half the time. That is the defense-debuff resistance the guides say a 4-star tank dies without.
3. **Run Focused Accuracy at all times.** Use Build Up before a boss or a debuffing group (Imperious task force Nictus, Circle of Thorns and Cimeroran mages).
4. **Single target:** Greater Fire Sword, Fire Sword, Scorch, Incinerate, repeated.
5. **Barrier is staggered with the team's other Barrier holders.** Fire it on the team's call or when defense debuffs land (the ambush at the top of the hill in the Imperious task force's first mission). Keep a blue inspiration for long fights.
6. **One with the Shield is the emergency click.** It gives +43.7% Smashing/Lethal and +21.8% other resistance, plus 375 hit points, for 120 s, on a 360 s recharge. Use it when typed damage spikes (Imperious task force negative energy, Psionic at 31%). Nothing in the set heals, so it and inspirations are the answer to a bad pull.

## What I would verify

- **The purple-patch to-hit factor of 0.65 for a +3 foe.** It drives every hit-chance figure here. It comes from training memory: approximate; verify in game.

## Guides used

- "Build Guide: Shield Defense/Broadsword able to handle any content" — `community/archetypes/tanker/build-guide-shield-defense-broadsword-able-to-handle-any-con.md`
- "Infinitum Build Collection" — `community/archetypes/tanker/infinitum-build-collection.md`
- "Azrael Tank Ratings." — `community/archetypes/tanker/azrael-tank-ratings.md`
- "Proc Monsters - Tanker Edition" — `community/archetypes/tanker/proc-monsters-tanker-edition.md`
- "Fire/MA: the Brunker, and compilation of Fire Armor builds." — `community/archetypes/tanker/fire-ma-the-brunker-and-compilation-of-fire-armor-builds.md` (defense-debuff cascade, Barrier as the hard-mode button, the 4-star notes)
- "Flavors of the tank" — `community/archetypes/tanker/flavors-of-the-tank.md`
- "Minmax Melee Archetype Comparison & Tier List" — `community/mechanics/minmax-melee-archetype-comparison-tier-list.md`
- "Brute & Tanker Melee Benchmarks (DPS, Clear Speed & Proc Comparisons)" — `community/archetypes/brute/brute-tanker-melee-benchmarks-dps-clear-speed-proc-compariso.md` (accuracy and to-hit parameters, Musculature and Ageless used in its benchmark builds, not adopted here)

All paths are under `C:\Users\petek\repos\homecoming-source-material\`.

## Files

Sibling files in this folder, all named "shield-fiery-4-star" plus an ending:

- `.mbd`
- `.md`
- `.mids-totals.json`
