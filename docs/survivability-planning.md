# Survivability planning

How well a build survives comes from several layers that multiply, not from one stat ranked above the others.
This page explains those layers, gives a small set of planning figures for enemies, names the source behind each figure, and says what was simplified to get there.

The figures are a general guide for comparing builds. They are not a model of every damage type against every defense.
Nothing in the engine computes them yet: `validate`, `score` and the chain planner do not use anything on this page.

## Sources, in order of authority

Each figure on this page names its source. When two sources disagree, say so, and prefer the one higher in this table.

| Order | Source | What it settles | Where |
| --- | --- | --- | --- |
| 1 | Game data | Enemy class base chance to hit, accuracy and hit points; which sets ignore exemplaring; power effects | `data/canonical/` |
| 2 | Patch notes | Content modifiers, such as the Advanced Mode star levels | `data/patches/` |
| 3 | Homecoming wiki | Rules of play the data does not spell out, such as exemplaring | Links under [Wiki pages read](#wiki-pages-read) |
| 4 | Community guides | Formulas and long-standing mechanics write-ups | The private source-material collection |
| 5 | Combat logs | A check on the figures above, from real play | Local captures, not in the repository |

Combat logs do not set a figure by themselves. They test it.
When a log disagrees with a figure, find the cause before changing either; the two cases so far are under [How the figures were checked](#how-the-figures-were-checked).

## The mechanics

### The layers

```text
damage taken = incoming damage
  x share of time the enemy can act
  x chance to be hit
  x (1 - resistance)

what absorbs it = hit points + absorb, refilled by regeneration and heals
```

Because the layers multiply, the worth of one more point of a stat depends on where the build already stands:

- At 70% resistance, one more point cuts damage taken by about 3.3% (from 30% let through to 29%).
- At a 10% chance to be hit, one point less cuts it by 10%. At the 5% floor, more defense cuts nothing.

A fixed priority order, such as "positional defense first, then resistance", cannot express that. Compare builds by the product.

### Chance to be hit

The formula is from Arcanaville's "Guide to ToHit and Defense" (in the source-material collection, `community/pre-sunset/mechanics/guide-to-tohit-and-defense-v1-10beta.md`):

```text
chance to be hit = bounded( accuracy x bounded( base chance to hit + to-hit buffs - defense ) )
```

Both `bounded` steps hold the value between 5% and 95%. The game uses the better of the matching typed defense and positional defense for each attack.

The terms on the enemy's side:

| Term | Value | Source |
| --- | --- | --- |
| Base chance to hit, ordinary enemy classes | 50% | `data/canonical/archetypes/minion_grunt.json`, `lt_grunt.json`, `boss_grunt.json`, `boss_elite.json`, `boss_archvillain.json`: `to_hit` 0.5 |
| Base chance to hit, Praetorian and incarnate elite class | 63.75% | `data/canonical/archetypes/boss_praetorianelite.json`: `to_hit` 0.6375 |
| Base chance to hit, monster class | 65% | `data/canonical/archetypes/boss_monster.json`: `to_hit` 0.65 |
| Accuracy by rank | minion 1.0, lieutenant 1.15, boss 1.3, arch-villain and monster 1.5 | The guide above |
| Accuracy by level difference | +3 is 1.3, +4 is 1.4, +5 is 1.5 | The guide above |
| Accuracy of the attacking power | usually 1.0 to 1.2, up to 2.0 on some powers | The power's file under `data/canonical/powers/` |

Base chance to hit belongs to the enemy's class, not its rank. An elite boss of the Praetorian class starts at 63.75%, an ordinary elite boss at 50%.

### Advanced Mode star levels

From the patch record `data/patches/2022-08-20-beta-staging-patch-notes-for-august-20th-2022-issue-27-second-chances-page-4.json`.
A search of the later records in `data/patches/` found none that revises these values.

At four stars:

- The mission difficulty is locked at +4 and the team size at x8.
- All enemies get +30% chance to hit, +30% defense, +20% smashing and lethal resistance, +30% resistance to everything else, and +40% damage.
- Bosses and elite bosses have magnitude 10 knockdown protection.
- Arch-villains and giant monsters have the incarnate base chance to hit (63.75%) and a +1 level shift.

The same record gives the two- and three-star values. No combat log has confirmed any of them.

### Time with defense down

Defense is not on all the time. A death, a mez that drops toggles, or a stack of defense debuffs takes it away for a while, and enemies hit far more often in that window.
In logged play 8.5% of enemy rolls in an ordinary task force landed while defense was down, and over a third on a bad evening.
This is why mez and knockback protection count toward survivability even though they add nothing to the totals.

### Effects the Mids totals leave out

MidsReborn's totals, which the engine reproduces, do not include these. List them beside a build's totals; do not treat them as free to trade away.

- Absorb and heal procs, for example Preventive Medicine: Chance for +Absorb and Panacea: +Hit Points/Endurance.
- Endurance procs, for example Performance Shifter: Chance for +End.
- Scaling resistance, for example Reactive Defenses.
- Debuffs the build puts on enemies: resistance, defense, damage and recharge.
- Control as mitigation: knockdown, knockback, fear, holds and slows. Higher ranks resist these, and bosses deal a larger share of damage than their share of a spawn.
- Buffs the build gives teammates, such as the Leadership toggles.

Mids also counts a click power as always on. A self-buff with 92% uptime is counted at 100%.

### Exemplaring

Exemplaring changes which powers, slots and set bonuses a build keeps, and so its defense.
The rules are in [the exemplar-10 rule](style-guides/build-rules/exemplar-10.md#mechanics-summary). Two points matter for planning:

- Powers picked more than five levels above the exemplar level are not usable. In a low task force, losing a defense toggle costs more defense than losing set bonuses.
- A crafted piece keeps its set bonus down to three levels below the piece's own level; an attuned piece keeps it down to three below the set's minimum level.
  A build of crafted level 50 pieces boosted to +5, the usual advice for four-star content, loses its regular set bonuses below level 47.

## The planning figures

One formula, with the enemy's side folded into two numbers:

```text
chance to be hit = bounded( enemy base chance to hit - defense ) x accuracy multiplier
```

| Content | Enemy base chance to hit | Defense where it bottoms out | Chance to be hit once there | Each point of defense short adds |
| --- | --- | --- | --- | --- |
| Solo, +0 | 50% | 45% | about 6% | about 1.3 points |
| Team, +3 or +4 | 50% | 45% | about 10% | about 2 points |
| Praetorian, incarnate and monster enemy classes | 64% | 59% | about 10% | about 2 points |
| Four-star Advanced Mode | 80% | 75% | 10% to 14% | about 2 points |

Two more figures:

- **Defense down about 10% of the time.** Treat a tenth of incoming attacks as landing with no defense.
- **A simple damage mix for weighing resistance:** half smashing and lethal, a quarter energy and negative, and the rest split between fire and cold, toxic, and psionic.
  This is an estimate. Logged mixes vary too much by enemy group to give one true figure.

### Where the accuracy multiplier comes from

The multiplier is rank accuracy x level accuracy x power accuracy, averaged over who deals the damage.

- Rank accuracy, weighted by each rank's share of damage taken in logged large-team play
  (minion 8, lieutenant 12, boss 24, elite boss 10, arch-villain 18, of the damage with a known rank): about 1.29.
- Level accuracy at +4: 1.4.
- Power accuracy: about 1.1.

That gives 1.29 x 1.4 x 1.1, about 2.0, for team play at +4. At +0 the level term is 1.0 and solo spawns are mostly minions, which gives about 1.3.

### A worked comparison

A build with 50% defense and 70% resistance, before the defense-down allowance:

| Content | Chance to be hit | Damage let through |
| --- | --- | --- |
| Team, +3 or +4 | 10% (at the floor) | 3% |
| Four-star Advanced Mode | (80% - 50%) x 2 = 60% | 18% |

In ordinary content this build gains nothing from more defense. In four-star content one more point of defense and one more point of resistance are each worth about 3.3%.

## How the calculation was simplified

A build is planned without knowing which enemies it will meet, how the player will stand, or how often its procs fire.
Each simplification below trades accuracy for a figure that works across that range.

| Simplification | Why | What it costs |
| --- | --- | --- |
| One accuracy multiplier in place of rank, level and power accuracy for each enemy | The make-up of a spawn is not known for the content a build will meet | Understates fights led by an arch-villain; overstates solo play |
| One defense figure in place of every typed and positional pairing | The mix of attack types by enemy group is not in the data | Wrong for a build with one weak position or type that the content happens to target |
| A fixed damage mix | Logged mixes swing from 59% smashing and lethal to 44% toxic and negative by enemy group | Wrong for any one enemy group |
| Enemy base chance to hit by content tier, not by enemy class | A plan names content, not enemies | Understates ordinary content that mixes in Praetorian-class enemies |
| A flat share of time with defense down | Logs do not record why defense dropped | The real share ran from 8.5% to over a third |
| Mids totals taken as they are | The engine reproduces Mids, which the parity tests pin | Click powers count as always on; procs, absorb and control count as nothing |
| Procs, absorb, control and team buffs listed beside the totals, not folded in | Mids does not model them, and the data does not say how long a knocked-down enemy stays down | Undervalues builds that lean on them |
| Enemy debuffs on the character left out | Stacking defense debuffs depend on fight length and enemy count | Understates the worth of defense above the point where it bottoms out |

## What is not known

- The mix of damage types and attack positions by enemy group. Nothing in `data/canonical/` adds it up.
- Whether the four-star figures hold in play. They come from patch notes, and no log has been identified as four-star.
- How often procs fire in play, against the rate the proc formula gives.
- How long knockdown and knockback keep an enemy from acting.
- How the make-up of a spawn changes with the team-size setting. Logs give a floor for large teams (median 14 enemies a fight, about 54% minions, 25% lieutenants, 18% bosses),
  but they do not record the difficulty setting.

## How the figures were checked

The check was a one-time analysis of combat logs with scripts that are not in the repository.
The log reader in `src/CohEngine.Core/CombatLog/` parses the same lines, but nothing in the engine adds them up.
Each enemy attack roll in a log states the chance it had to hit, which makes the formula directly testable.

- **Large-team play:** 14,915 enemy rolls, 10.6% hit. That is the "about 10%" in the table.
- **A solo task force at +0:** the lowest enemy rank sat at exactly 5.0% against one enemy type, and lieutenants hit 1.15 times as often as minions, as the rank table says.
- **A team task force at about +2 and +3:** 91.5% of rolls sat at the floor, averaging 7.2% for minions, 9.0% for lieutenants and 11.6% for bosses.

Two results at first disagreed with the formula. Both had causes:

- **Elite bosses appeared to hit twice as often as bosses.** Most of those rolls came from one hard-mode enemy of the Praetorian elite class.
  Its base chance to hit is 63.75%, and its powers have accuracy 1.2 and 2.0.
  Within any one character, elite bosses did not out-hit bosses. This is where "base chance to hit belongs to the class" comes from.
- **One character was hit two to three times as often as its totals predicted.** 62.5% of rolls sat exactly at the floor. The rest fell in short episodes around deaths and dropped toggles.
  This is where the defense-down allowance comes from. The logs cannot say how much of that time was debuffs and how much was toggles.

## Wiki pages read

Read on 2026-11-15:

- [Exemplar Effects on Enhancements](https://homecoming.wiki/wiki/Exemplar_Effects_on_Enhancements)
- [Invention Origin Enhancements](https://homecoming.wiki/wiki/Invention_Origin_Enhancements)
- [Attuned](https://homecoming.wiki/wiki/Attuned)
- [Enhancement Booster Salvage](https://homecoming.wiki/wiki/Enhancement_Booster_Salvage)
- [Very Rare IO Sets](https://homecoming.wiki/wiki/Very_Rare_IO_Sets)
- [PvP IO Sets](https://homecoming.wiki/wiki/PvP_IO_Sets)
- [Archetype Enhancements](https://homecoming.wiki/wiki/Archetype_Enhancements)
- [Winter Origin Enhancements](https://homecoming.wiki/wiki/Winter_Origin_Enhancements)
- [Sidekick](https://homecoming.wiki/wiki/Sidekick), which covers exemplaring

## Related

- [The exemplar-10 rule](style-guides/build-rules/exemplar-10.md)
- [Min-maxing](min-maxing.md), for soft caps and the rule of five
- [Procs](procs.md), for the proc chance formula
