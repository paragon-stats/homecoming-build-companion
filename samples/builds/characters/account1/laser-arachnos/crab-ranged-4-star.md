# Crab Spider Soldier-Crab Spider Training Arachnos Soldier - 4-Star

**This is the level 50 layout. It cannot be followed before level 24.** Until the forced respec at 24, an Arachnos Soldier picks only from
the base Arachnos Soldier and Training and Gadgets sets. That respec is what places the Crab Spider powers listed here at levels 1, 2 and 12.
This document is not a leveling plan.

This is a level 50 Arachnos Soldier on the Crab Spider branch: Crab Spider Soldier / Crab Spider Training, with the Leaping, Leadership,
Speed and Fighting pools and Leviathan Mastery. It is a **ranged Crab**: six attacks on keys 1 to 6, all fired from range, plus the full
Leadership stack for the team. It is built for 4-star task forces on a mixed team of eight.

Loadable file: `crab-ranged-4-star.mbd`. Every number comes from one of four sources
unless it is marked as arithmetic or as an estimate:

- MidsReborn's totals (`totals.json`, `TotalsCapped`, copied here as `.mids-totals.json`);
- the repository's chain planner (`coh_engine chain`);
- the exemplar script, which applies the Homecoming wiki's exemplar rules piece by piece (see "Exemplar");
- the survival score, the simplified method of `docs/survivability-planning.md` (see "Survival score").

**Database stamp.** The `.mbd` is stamped with Mids database version **2026.5.1337**, the Homecoming database installed in the operator's
Mids Reborn. A copy of that database was given to the Mids dump harness, and every figure here comes from dumps against it.

## Checks run on this file

Every check below was run on this build itself. No figure is borrowed from another variant.

1. **Repository validator** (`python -m coh_engine validate`): no violations.
2. **Slotting check** (`CohEngine.Cli.exe validate --mids-dir tests/fixtures/mids --slotting`): no violations.
3. **Mids dump harness:** totals written; the expected E21 and E14 exits only. A second dump of the delivered `.mbd` gives totals
   byte-identical to the delivered `.mids-totals.json`.
4. **Hit chance against +4:** 95% on all seven attacks on keys 1 to 6 and ALT+1, with no hit-chance fixes needed. Brawl and Boxing, never
   pressed, are at 89.3%.
5. **Chains:** single target and 10-target spawn, each with and without Venom Grenade on cooldown; bars as measured and with the proc
   estimate.
6. **Enhancement Diversification audit:** every power, every aspect, measured on this build.
7. **Exemplar:** rows at 25, 35, 45 and 50. Parity at 50 against the harness matches every field but hit points (0.10 apart). The
   hand-worked check passes: the four level 50 Luck of the Gambler globals add +30.0% recharge at exactly exemplar 47.
8. **Survival score:** both tiers and both damage mixes, against the originally delivered build and the knockdown version, with the hand check.
9. **Boost evidence:** each boosted piece's value before and after (all ×1.25). Globals and set bonuses are byte-identical. 75 boosters.
10. **Slot check** against the originally delivered file: no slot moved; 67 added slots.
11. **Every granted slot filled:** all 31 powers with slots hold a piece; no slot is empty.
12. **Pick and pool order:** the level 1 primary pick precedes the secondary; the pools are listed in the order first picked (Leaping,
    Leadership, Speed, Fighting). The reorder moved no slot.

## What this revision changes

Against the originally delivered build. **No slot moves**: the 67 added slots stay where they are.

| Measure | Originally delivered | Knockdown version (not kept) | **This build** |
| --- | --- | --- | --- |
| Defense melee / ranged / AoE | 49.15 / 49.72 / 52.27 | 49.41 / 51.16 / 52.54 | **49.68 / 50.55 / 52.80** |
| Typed defense S/L, F/C, E/N, Tox/Psi | 43.83, 49.15, 39.77, 38.52 | 44.10, 49.41, 42.54, 38.79 | 44.37, 49.68, 40.30, 39.05 |
| Resistance S/L, F/C, E/N, Tox, Psi | 72.55, 68.96, 61.46, 66.71, 37.41 | 72.32, 68.96, 61.46, 60.71, 31.26 | 72.32, 68.96, 61.46, 66.71, 37.26 |
| Hit points (Serum counted as always on) | 1,989 | 1,973 | 1,989 (1,364 with Serum down) |
| Hit chance vs +4 | 95% on every attack | 95% (with fixes) | 95% on every attack (no fixes) |
| Survival score, team simple / logs | 100 / 100 | 95.6 / 93.6 | **99.6 / 99.8** |
| Survival score, 4-star simple / logs | 100 / 100 | 97.8 / 95.8 | **101.6 / 101.7** |
| Bars, single / group | 97.5 / 107.4 s | 114.4 / 117.5 s | 98.6 / **108.8 s** |
| Damage, single / group | 155.2 / 667.7 dmg/s | 149.8 / 644.6 dmg/s | 155.2 / 667.7 dmg/s |
| Knockback protection | 0 | 4 | 0 |
| Recharge, Hasten | +150%, 130.1 s | +140%, 134.0 s | +150%, 130.1 s |
| Enhancement Boosters | 35 | 95 | 75 |

What changes against the originally delivered build:

- **Sets unchanged.** Superior Dominion of Arachnos in Frag Grenade, Ragnarok in Venom Grenade, Superior Frozen Blast in Bile Spray,
  Annihilation in Arctic Breath.
- **Boosts:** eight survival pieces at +5 (see "Enhancement Boosters").
- **Luck of the Gambler at level 50 everywhere**, the four global recharge pieces included (the operator's rule for a four-star build).
- **The in-game levels** of the other global and unique pieces are recorded.
- **Every granted slot filled:** Swift, Hurdle, Boxing, Brawl, Sprint and Rest.
- **Pools listed in pick order** in the file.

**The knockdown version is not kept.** It put Overwhelming Force in Frag Grenade to turn the knockback to knockdown. In four-star content most
enemies resist knockback and knockdown anyway, so the change was not worth what it cost:

- 2 to 6 points on the survival score;
- 6 points each of Toxic and Psionic resistance;
- Superior Dominion's −damage and fear proc.

It stays in this document as a comparison row.

## Key map

The operator's tray has four rows:

- **Keys 1 to 6:** the attack chain.
- **ALT row:** opportunity clicks.
- **CTRL row:** self-buffs.
- **Fourth bar:** toggles and travel, never pressed by key.

Hasten is on auto-fire (key 0) and takes no slot. The 1 to 6 and ALT rows hold 8 click powers here, against a limit of 12.

| Row | Key | Power | What it is for |
| --- | --- | --- | --- |
| Primary | 1 | Longfang | Hardest single-target hit; −7.5% defense on the target |
| Primary | 2 | Channelgun | Fast filler; −7.5% defense on the target |
| Primary | 3 | Spirit Shark | Second single-target hit; prerequisite for the two cones |
| Primary | 4 | Frag Grenade | Ranged AoE with knockback; Superior Dominion of Arachnos −damage and fear proc |
| Primary | 5 | Bile Spray | Ranged cone (Mids stores it as `Chum_Spray`); Superior Frozen Blast's immobilize proc |
| Primary | 6 | Arctic Breath | Ranged cone: −15% resistance, −11.25% defense, −30% recharge and slow for 10 s; Annihilation's proc |
| ALT | ALT+1 | Venom Grenade | −20% resistance on the spawn for 16 s; Ragnarok's knockdown proc; recharge 7.06 s |
| ALT | ALT+2 | Ion Core Final Judgement | Nuke on a fresh spawn |
| ALT | ALT+3 to ALT+6 | empty | All 24 picks are in use; Wide Area Web Grenade in place of Aim is the first change to test if the team lacks control |
| CTRL | CTRL+1 | Aim | Burst: +50% damage and to-hit before Frag Grenade and the cones, or before Longfang on an arch-villain |
| CTRL | CTRL+2 | Serum | +625 HP for 120 s and a heal; recharges in 130.2 s, so it is up about 92% of the time (arithmetic) |
| CTRL | CTRL+3 | Assault Radial Embodiment | Hybrid; on for the hard fights |
| CTRL | CTRL+4 | Barrier Core Epiphany | Destiny, on the team's Barrier rotation |
| Auto-fire | 0 | Hasten | +70% recharge for 120 s; recharges in 130.1 s, about 92% uptime (arithmetic) |
| Fourth bar | — | Tactical Training: Maneuvers, Assault and Leadership; Maneuvers, Assault, Tactics | Leadership toggles; on once, never pressed |
| Fourth bar | — | Fortification, Tough, Weave, Combat Jumping | Survival toggles; Combat Jumping is also the travel power |
| Fourth bar | — | Sprint | Inherent travel toggle |
| — | — | Boxing | Prerequisite, not on a key. Taken only to unlock Tough and Weave; never pressed |

Auto powers need no key: Crab Spider Armor Upgrade, Combat Training: Defensive and Mental Training.

**Why Venom Grenade is a click, not an attack.** It does little damage per activation (202), but it carries the build's largest
resistance debuff (−20% for 16 s). Pressed whenever it is up, ahead of the chain, it keeps the debuff on the whole spawn. As a seventh
attack it would break the six-key limit.

## Slotting rules applied

- **Hasten.** Two Recharge Reduction IOs at +5, the norm for a character that clicks constantly. Permanence is not a target: it recharges in
  130.1 s for a 120 s buff, about 92% uptime.
- **Luck of the Gambler at level 50.** Every piece is crafted at 50, the four global recharge pieces included (the operator's four-star rule).
  Combat Jumping and Maneuvers hold three pieces each, Tactical Training: Maneuvers five. Combat Training: Defensive holds the global piece
  with Shield Wall's +5% resistance. No defense toggle holds a lone piece.
- **Uniques by exemplar level.** Crab Spider Armor Upgrade (a level 1 power) holds Steadfast Protection (crafted 10), Gladiator's Armor
  (crafted 10) and Impervium Armor's Psionic piece (crafted 15). Tough holds the Unbreakable Guard set, its +Max HP piece and Aegis
  (crafted 25). Placement table below.
- **Tough earns its pick.** Unbreakable Guard Resistance, Resistance/Endurance, RechargeTime/Resistance and Resistance/Endurance/RechargeTime,
  the set's +Max HP unique and Aegis. Its bonuses: +3.13% melee defense (4) and +3.75% Toxic/Psionic resistance (5).
- **Health survivability pieces.** Numina's Convalescence (crafted 30), Preventive Medicine Chance for +Absorb (attuned in game, crafted 50
  in the file; see "Unique placement"), Panacea +Hit Points/Endurance (crafted 50), Regenerative Tissue +Regeneration (crafted 10) and
  Miracle (crafted 20).
- **Exemplar pick order.** Mental Training at 22 (where it opens) and Maneuvers at 8. The pick order is unchanged.
- **Pools listed in pick order:** Leaping (Combat Jumping, 6), Leadership (Maneuvers, 8), Speed (Hasten, 14), Fighting (Boxing, 28).
- **Rarer sets.** Superior Dominion of Arachnos in Frag Grenade, Ragnarok in Venom Grenade, Superior Frozen Blast in Bile Spray, Annihilation
  in Arctic Breath, six pieces each. Ragnarok's four-piece and Superior Dominion's two-piece bonus are the same +15% accuracy, and both count,
  which holds every attack at 95% without extra pieces.
- **Sets outrank diversification.** No purple, Winter or archetype set is shortened. Apocalypse holds five pieces because Spirit Shark has
  five slots.
- **Boosts where they earn their boosters.** A piece is boosted when at least half its boost survives Enhancement Diversification.
- **Every granted slot holds the most useful enhancement that adds value** (the operator's rule): Swift, Hurdle, every attack and every
  power with only its granted slot. No slot in the file is empty (table below).
- **Survivability is layers that multiply** (`docs/survivability-planning.md`). Builds are compared by the survival score (see
  "Survival score").

### Single-slot powers

Allowed enhancement types are from `boosts_allowed` in `data/canonical/powers`.

| Power | Slotted | Why it is the most useful choice |
| --- | --- | --- |
| Tactical Training: Assault | Endurance Reduction IO +5 | Takes only Endurance Reduction or Recharge; recharge does nothing on a toggle |
| Assault | Endurance Reduction IO +5 | As above |
| Tactics | Endurance Reduction IO +5 | Every attack is at 95% without it, so a To-Hit Buff IO would only add team to-hit and cost endurance |
| Mental Training | Run Speed IO +5 | Takes only Run Speed or Fly Speed; this build has no flight |
| Aim | Recharge Reduction IO +5 | More uptime on the +50% burst; Aim is not in the totals either way |
| Boxing | Accuracy IO, level 50 (as in game) | Never pressed; the operator's piece is kept |
| Swift | Run Speed IO, level 50 (as in game) | Takes Run Speed or Fly Speed; no flight here |
| Hurdle | Jump IO, level 50 (as in game) | Takes only Jump; Combat Jumping is the travel power |
| Stamina | Performance Shifter: Chance for +End, crafted 21 | Its proc adds about 0.26 end/s (estimate); an EndMod piece needs a second slot to pay off |
| Brawl | Accuracy IO, level 50 (not boosted) | An attack with one granted slot; accuracy lifts its hit chance to 89.3% |
| Sprint | Run Speed IO, level 50 | Takes Run Speed, Jump or Endurance Reduction; Run Speed raises its speed buff from +50% to +71.2% |
| Rest | Endurance Modification IO, level 50 | Takes Endurance Modification, Heal or Interrupt Reduction; endurance is the build's short layer |

Brawl, Sprint and Rest are outside Mids' totals, so their IOs move no total; they change only those powers' own values.

**The prestige travel power is not in the file.** It accepts Endurance Reduction, Run Speed and Jump (`boosts_allowed` in
`data/canonical/powers/inherent/inherent/prestige_generic_sprintp.json`). The `.mbd` format and the build writer carry only the class's own
inherent powers, and Mids offers no build entry for prestige powers. In game a Run Speed IO is the natural choice there, as in Sprint.

### Unique placement

The lowest exemplar level at which a piece's own bonus works follows the wiki's rules, piece by piece, as the exemplar script applies them:

- **A power** is usable while its pick level is at most the exemplar level + 5. Every slot in a usable power works.
- **Purple, PvP-set, archetype and Winter pieces** (the data's `bonuses_ignore_exemplar`) keep their everyday bonuses at every level.
- **An attuned piece** works from its data floor, `min_bonus_level` in its `boost_info` (the wiki states the set minimum − 3; neither has been checked in game).
- **A crafted piece** works from its own level − 3. Boosting does not change that level.
- **A proc** works whenever its power is usable.
- **Set bonuses and globals** stay on even while their power is greyed out.

| Unique | Power | Piece as slotted | Lowest exemplar level | Reason |
| --- | --- | --- | --- | --- |
| Steadfast Protection: Resistance/+Def 3% | Crab Spider Armor Upgrade | crafted 10, +5 | 7 | +3% defense to every position, needed at every level |
| Gladiator's Armor: TP Protection +3% Def (All) | Crab Spider Armor Upgrade | crafted 10 | every level | PvP set; as above |
| Shield Wall: +Res (Teleportation), +5% Res (All) | Combat Training: Defensive | crafted 50 | every level | PvP set; the earliest defense power |
| Unbreakable Guard: +Max HP | Tough | crafted 50 | 47 | Hit points; sits with its own set |
| Aegis: Psionic/Status Resistance | Tough | crafted 25 | 22 | Psionic and status resistance |
| Reactive Defenses: Scaling Resist Damage | Weave | crafted 50 | 47 | Defense-set unique |
| Preventive Medicine: Chance for +Absorb | Health | crafted 50 in the file; attuned in game | 47 as the file holds it; 14 attuned | Crafted 50 − 3; attuned: the data floor, `min_bonus_level` 13 stored as level − 1 (the wiki's set minimum 20 − 3 gives 17) |
| Panacea: +Hit Points/Endurance | Health | crafted 50 | every level | PvP set, and a proc |
| Regenerative Tissue: +Regeneration | Health | crafted 10 | every level | A proc in always-usable Health |
| Miracle: +Recovery | Health | crafted 20 | every level | As above |
| Numina's Convalescence: +Regeneration/+Recovery | Health | crafted 30 | every level | As above |

The four Luck of the Gambler +7.5% recharge globals are crafted at 50 and work from exemplar 47.

**Proc or global** is the game data's: `boost_info.is_proc` in `data/canonical/powers/boosts/crafted_<piece>/crafted_<piece>.json`.
Panacea, Regenerative Tissue, Miracle, Numina's Convalescence and Performance Shifter's Chance for +End have `is_proc: true` and
carry their effect in the piece. The other pieces in the table have `is_proc: false` and carry it in a `global_bonuses` power, which
follows the level rule unless the piece has `bonuses_ignore_exemplar: true` (Gladiator's Armor, Shield Wall).

**PvP-set pieces count only their everyday bonuses.** Each tier of a PvP set from two pieces up in `data/canonical/boost_sets/<set>.json` has two bonuses:
one with `is_pvp_bonus: false`, which works on every map, and one with `is_pvp_bonus: true` and `requires: isPVPMap?`, which works only on PvP maps.
The unique globals (Shield Wall +5% resistance, Gladiator's Armor +3% defense) are `is_pvp_bonus: false`.
No PvP-only bonus is counted in any figure here (the engine computes in PvE mode), and none is a reason for a pick.

**Preventive Medicine's absorb is crafted 50 in the file.** By the game data it is a global, not a proc: `is_proc: false`, and the
absorb comes from its `global_bonuses` power `set_bonus.set_bonus.preventive_medicine`, which fires by chance. An `.mbd` marks a piece
attuned only by an `Attuned_...` identifier, and the
Mids 3.8.6 database the file is written against has none for Preventive Medicine. Slotted as the file holds it, the absorb is off below
exemplar 47. To have it from 14, buy it attuned in game (the data floor; crafted 20 works from 17). The absorb counts in no total here, so
no figure changes.

**Impervium Armor: Psionic Resistance is not a unique.** The earlier table listed it as one. In
`data/canonical/boost_sets/impervium_armor.json` its `is_unique` is false. Its boost data also carries no one-per-character
`slot_requires`, unlike every unique above. It is a crafted level 15 piece in Crab Spider Armor Upgrade and works from exemplar 12.
More copies could be slotted, each adding the same psionic resistance bonus; this build does not add any.

**What the Health and Stamina procs give.** From `data/canonical/powers/boosts/` and `set_bonus/set_bonus/preventive_medicine_proc.json`:

- **Preventive Medicine absorb.** It checks every 5 s: a 10% chance at 31 to 75% health, 100% below 31%. Each trigger grants absorb equal
  to 20% of max HP for 30 s, **at most once every 120 s** (the earlier text said 90 s). That is about 398 absorb at 1,989 HP with Serum up,
  273 with Serum down, and at most 398 / 120 = 3.3 HP/s (arithmetic; the earlier 90 s implied 4.4). By the data it is a chance-based
  global, not a proc (`is_proc: false`; see "Unique placement").
- **Panacea.** 3 PPM in an auto power, checked every 10 s: 3 × 10 / 60 = a 50% chance per check, about three triggers a minute. Each heals
  (scale 0.67 on the heal table) and restores 7.5% endurance: 0.5 × 7.87 / 10 = **0.39 end/s** (estimate).
- **Performance Shifter: Chance for +End** (crafted 21). 1.5 PPM, checked every 10 s: a 25% chance, +10% endurance: 0.25 × 10.5 / 10 =
  **0.26 end/s** (estimate).
- **None of these procs is modelled by Mids.** "Endurance against real encounter lengths" shows the bars with and without them.

## What the totals count

- **Counted as always on:** every toggle and auto power, Health, Stamina, Hasten and Serum. Serum's two +HP effects add 624.93 HP (harness
  effect magnitudes): 1,989.1 with Serum and 1,364.2 without (arithmetic). Serum recharges in 130.2 s for a 120 s buff, so those 625 HP are
  there about 92% of the time (arithmetic). The survival score uses that uptime.
- **Not counted:** Aim, Barrier Core Epiphany and Assault Radial Embodiment (set to off in the file), Brawl, Sprint and Rest, and the damage
  procs.
- **One proc is counted that should not be.** Mids counts Gaussian's Synchronized Fire-Control: Chance for Build Up in Tactical Training:
  Leadership as always on. To-hit, damage and the chains below come from a measurement copy without that piece: standing to-hit
  **+25.75%** and damage **+44.5%**.
- **The harness exit.** The Mids dump harness ends with E21 and E14 only because its built-in exemplar build is not in a one-build folder.

## Targets and achieved numbers

**Resistance cap: 85%.** The Mids fixture `tests/fixtures/mids/archetypes.json` (`"ResCap": 0.85` for `Class_Arachnos_Soldier`) and
`data/canonical/archetypes/arachnos_soldier.json` (`attrib_resistance_max`, 0.85 at every level) agree. `strategy/breakpoints.json` lists 75
and was not changed.

| Target | Goal | Originally delivered | **Final** | Met |
| --- | --- | --- | --- | --- |
| Melee / ranged / AoE defense | ≥ 45% with margin | 49.15 / 49.72 / 52.27% | 49.68 / 50.55 / 52.80% | yes |
| Typed defense S/L, F/C, E/N, Tox/Psi | report | 43.83, 49.15, 39.77, 38.52% | 44.37, 49.68, 40.30, 39.05% | noted |
| Resistance S/L, F/C, E/N, Tox, Psi | as high as allowed | 72.55, 68.96, 61.46, 66.71, 37.41% | 72.32, 68.96, 61.46, 66.71, 37.26% | noted |
| Hit points | as high as allowed | 1,989.1 | 1,989.1 (83% of the 2,409.5 cap); 1,364.2 with Serum down | noted |
| Regeneration (`HPRegen`) | as high as allowed | 1.67 | 1.67 | noted |
| Survivability procs | in Health | absorb, Panacea, Regenerative Tissue | the same | yes |
| Knockback protection | report | 0 | 0 | noted |
| Hit chance vs +4, +25% defense | 95% on every attack | 95% | **95% on every attack** | yes |
| Endurance, toggles only | positive | `EndRec` 0.645, `EndUse` 1.341 | `EndRec` 0.645, `EndUse` 1.329; recovery 3.02 end/s, net +1.69 end/s | yes |
| Bar, group chain (six attacks) | past 108 s | 107.4 s | **108.8 s**; about 340 s with the procs (estimate) | yes |
| Bar, single-target chain (six attacks) | past 146 s | 97.5 s | **98.6 s**; about 257 s with the procs (estimate) | **no** as measured |
| Recharge, Hasten, Serum (fact) | — | +150%, 130.1 s, 130.2 s | +150%, 130.1 s, 130.2 s (about 92% uptime each) | — |

Recovery per second uses Mids' formula from `Statistics.cs`: (1 + `EndRec`) × BaseRecovery 1.05 × 1.6667 × (1 + `EndMax`/100)
= 1.645 × 1.75 × 1.0495 = 3.02 end/s. The bar times are the planner's worst case: it fires every power the instant it recharges.

### Hit chance

min(95%, accuracy multiplier × (75% + 25.75% − 25%) × 0.65). The 0.65 level factor for a +4 foe is approximate. The 95% line is a
multiplier of 1.929.

| Key | Attack | Accuracy multiplier | Hit chance | Recharge (s) | End cost |
| --- | --- | --- | --- | --- | --- |
| 1 | Longfang | 2.235 | 95% | 3.23 | 4.82 |
| 2 | Channelgun | 2.205 | 95% | 1.43 | 2.60 |
| 3 | Spirit Shark | 1.986 | 95% | 3.82 | 3.96 |
| 4 | Frag Grenade | 2.205 | 95% | 4.57 | 7.58 |
| 5 | Bile Spray | 2.354 | 95% | 9.92 | 6.63 |
| 6 | Arctic Breath | 2.247 | 95% | 10.12 | 7.98 |
| ALT+1 | Venom Grenade | 1.986 | 95% | 7.06 | 6.46 |
| — | Brawl (never pressed) | 1.814 | 89.3% | 0.80 | 0 |
| — | Boxing (never pressed) | 1.814 | 89.3% | 1.00 | 2.63 |

**The knockdown version's hit-chance fixes are removed.** That version lost Superior Dominion's +15% accuracy. To hold 95% it needed a
To-Hit Buff IO in Tactics and +5 boosts on the two accuracy pieces in each of Spirit Shark and Venom Grenade. With Superior Dominion back
in Frag Grenade, global accuracy is +39% again and every attack is at 95% without them:

- Tactics is back to its Endurance Reduction IO at +5.
- None of the four accuracy boosts is kept.

## Survival score

The method is the simplified one in `docs/survivability-planning.md`: survivability is layers that multiply, so a point of defense is
weighed against a point of resistance by what each is worth in the content. One script, inputs at its top:

- **Chance to be hit, per position:** min(95%, bounded(enemy base chance to hit − defense, 5% to 95%) × 2.0). Team at +3/+4: base 50%.
  Four-star: base 80%. Positions weighted for a ranged character behind a tank: melee 10%, ranged 45%, AoE 45%.
- **Defense down:** a tenth of attacks land with defense taken as zero.
- **Damage let through:** 1 − resistance, weighted by damage type. The simple mix: half smashing and lethal, a quarter energy and negative,
  the rest split between fire, cold, toxic and psionic. The logs mix (large-team play): lethal 22, toxic 22, negative 17, energy 12,
  smashing 11, fire 6.5, psionic 6, cold 0.6, unresistable 3 (resistance 0), normalised.
- **Hit points** at Serum's real uptime: the with- and without-Serum figures weighted by 120 s / Serum's recharge.
- **Index** = hit points ÷ (chance to be hit × damage let through), with the originally delivered build = 100.

The 20/40/40 weighting changes no winner (team columns identical, 4-star columns within 0.1 for this build).

| Build | Team, simple | Team, logs | 4-star, simple | 4-star, logs | Lowest hit | Bars single / group | Dmg/s single / group | KB prot | Procs | Boosters | Slots moved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Originally delivered | 100.0 | 100.0 | 100.0 | 100.0 | 95.0 | 97.5 / 107.4 | 155.2 / 667.7 | 0 | SDoA, Ann | 35 | 0 |
| **This file** | **99.6** | **99.8** | **101.6** | **101.7** | 95.0 | 98.6 / 108.8 | 155.2 / 667.7 | 0 | SDoA, Ann | 75 | 0 |
| Knockdown version (OF in Frag Grenade, not kept) | 95.6 | 93.6 | 97.8 | 95.8 | 95.0 | 114.4 / 117.5 | 149.8 / 644.6 | 4 | Ann | 95 | 0 |
| N1/F1: SDoA in Venom Grenade, no Ragnarok | 95.1 | 94.1 | 97.4 | 96.3 | 90.4 | 113.7 / 116.8 | 151.4 / 651.3 | 4 | SDoA, Ann | 75 | 0 |
| W1: N1 + two slots moved to Longfang + hit fixes | 86.8 | 89.1 | 95.3 | 97.8 | 95.0 | 114.4 / 113.1 | 152.0 / 651.7 | 4 | SDoA, Ann | 120 | 2 |
| N5B: Ragnarok Bile, SFB Arctic, no Annihilation | 98.8 | 98.8 | 96.3 | 96.3 | 95.0 | 102.9 / 98.7 | 155.1 / 684.7 | 4 | SDoA | 75 | 0 |
| V6: N1, Fortification −1 → Stamina EndMod | 91.3 | 91.9 | 93.4 | 94.0 | 90.4 | 142.4 / 147.3 | 151.4 / 651.3 | 4 | SDoA, Ann | 95 | 1 |

The comparison rows (all but this file) kept the Luck of the Gambler globals at level 25, as bought in game.

**Hand check** on this file, four-star, simple mix, 10/45/45:

- melee min(95, (80 − 49.68) × 2) = 60.64%; ranged (80 − 50.55) × 2 = 58.90%; AoE (80 − 52.80) × 2 = 54.40%;
- blended 0.10 × 60.64 + 0.45 × 58.90 + 0.45 × 54.40 = 57.05%; with the allowance 0.9 × 57.05 + 0.1 × 95 = 60.84%;
- let through 0.25 × (1 − 0.7232) × 2 + 0.125 × (1 − 0.6146) × 2 + 0.0625 × [(1 − 0.6896) × 2 + (1 − 0.6671) + (1 − 0.3726)] = 33.36%;
- hit points 0.922 × 1,989.1 + 0.078 × 1,364.2 = 1,940.1;
- index 1,940.1 / (0.6084 × 0.3336) = 9,559; the originally delivered build's is 9,410; 9,559 / 9,410 = 101.6, as the table shows.

**Reading the scores.** Against the originally delivered build, this file gives up 0.2 to 0.4 in team content: Steadfast Protection is
crafted at 10, not 30, which costs 0.23 points of Smashing/Lethal and 0.15 of Psionic resistance. It gains 1.6 to 1.7 in four-star content,
from the boosted and level 50 Luck of the Gambler pieces' defense. Against the knockdown version it gains 3.8 to 6.2 in every column.

### Resistance by Frag Grenade arrangement

Against the originally delivered sets with the in-game levels (72.29, 68.96, 61.46, 66.71, 37.24). Bonuses by piece count, from
`data/canonical/boost_sets`:

- **Ragnarok:** 2 +4% recovery; 3 +6% fire/cold resistance; 4 +15% accuracy; 5 +10% recharge; 6 +6% toxic/psionic resistance.
- **Superior Dominion of Arachnos:** 2 +15% accuracy; 3 +3% HP; 4 +4% damage; 5 +10% recharge; 6 +6% toxic/psionic resistance.

The two share the accuracy, recharge and toxic/psionic bonuses; with both slotted each counts twice, and dropping either loses one copy.

| Arrangement | Set left out | Resistance lost | From which bonus |
| --- | --- | --- | --- |
| **This file: SDoA in Frag Grenade, Ragnarok in Venom Grenade** | none | none | — |
| Knockdown version: OF in Frag Grenade | Superior Dominion | Toxic −6, Psionic −6 | SDoA six-piece (one of two copies) |
| N1/F1: SDoA in Venom Grenade, no Ragnarok | Ragnarok | Fire/Cold −6; Toxic −6, Psionic −6 | Ragnarok three-piece; Ragnarok six-piece |
| N2: Superior Frozen Blast left out | Superior Frozen Blast | Fire/Cold −6 | SFB three-piece |
| N5B: Ragnarok in Bile Spray, SFB in Arctic Breath | Annihilation | Energy/Negative −2.25 | Annihilation three-piece |

**Correction.** An earlier summary said the arrangement with Superior Dominion in Venom Grenade kept toxic and psionic resistance. It does
not: it keeps one copy of the +6% bonus and loses Ragnarok's (60.71% Toxic and 31.24% Psionic, against 66.71% and 37.24%).

### Proc chance per target

Mids' formula (`Effect.cs`, `ActualProbability`): P = PPM × (recharge with local enhancement only + cast time) / (60 × (0.75 × AoE
modifier + 0.25)).

| Power | AoE modifier | Local recharge | Proc | Chance per target |
| --- | --- | --- | --- | --- |
| Arctic Breath | 2.74 | 19.25 s | Annihilation, 3 PPM, −12.5% resistance for 10 s | **46.8%** |
| Frag Grenade | 2.50 | 8.01 s | Superior Dominion, 5 PPM, −damage and fear | 37.9% |

Arctic Breath is fired once per 12.63 s group cycle, so each target carries the Annihilation debuff about 37% of the time (arithmetic).

## Enhancement Boosters

**The rule.** A boost raises the piece's own enhancement values by 5% each (25% at +5). It does not raise global bonuses, set bonuses or
procs, and attuned pieces cannot be boosted. Sources:

- the Homecoming wiki's Enhancement Booster Salvage page: "increases the effectiveness of the enhancement by 5% per application";
  "Procs and Globals are unaffected", but "the enhancement portion is boosted" (a Luck of the Gambler's defense is boosted, its recharge
  bonus is not); attuned enhancements "cannot be boosted"; "Boosted enhancements function at their natural level";
- the game data's `boostable` flag in `data/canonical/boost_sets`;
- the harness measurement below.

**How Mids applies it.** `docs/engine/mids-port-spec.md`, step 1 of the enhancement value (`GetRelativeLevelMultiplier`,
`I9Slot.cs:138-153`): the piece's table value × (1 + 0.05 per level above even), "+5→1.25". This is Mids' model. Not confirmed in game:
that the level 10 Steadfast Protection at +5 gives exactly 1.25 times its own value.

**Which Luck of the Gambler pieces are boosted.** Boosted where at least half the defense gain survives Diversification:

- **Combat Training: Defensive** (one piece): defense 15.94 → 19.92%, all of it kept. **Boosted.**
- **Combat Jumping** (three pieces): defense 44.68 → 55.84% before ED, 44.21 → 53.09% after, so 80% of the gain is kept. **Boosted.**
- **Maneuvers** (three pieces): the same, 80% kept. **Boosted.**
- **Tactical Training: Maneuvers** (five pieces): defense 86.11 → 107.64% before ED, 59.92 → 63.15% after, so 15% is kept (arithmetic
  through the engine's ED function). **Not boosted.**

**What is boosted** (8 set pieces and 7 common IOs; 15 pieces × 5 = **75 boosters**). Values are each piece's own enhancement before ED, from
the harness slots of this build without and with the boosts. Every ratio is exactly 1.2500.

| Power | Piece (level) | Enhancement value, unboosted → +5 | Kept after ED |
| --- | --- | --- | --- |
| Crab Spider Armor Upgrade | Steadfast Protection: Resistance/+Def 3% (10) | Resistance 4.37 → 5.47% | 100% |
| Combat Training: Defensive | Luck of the Gambler: Defense/+Recharge (50) | Defense 15.94 → 19.92% | 100% |
| Combat Jumping | Luck of the Gambler: Defense/+Recharge (50) | Defense 15.94 → 19.92% | 80% (defense) |
| Combat Jumping | Luck of the Gambler: Defense/Endurance (50) | Defense 15.94 → 19.92%; Endurance 26.50 → 33.12% | 80% / 100% |
| Combat Jumping | Luck of the Gambler: Defense/Endurance/Recharge (50) | Defense 12.80 → 16.00%; End, Recharge 21.20 → 26.50% | 80% / 100% |
| Maneuvers | the same three pieces | the same values | 80% / 100% |
| Hasten, Tactical Training: Assault, Assault, Tactics, Mental Training, Aim | common IOs (50) | each value × 1.25 | — |

**For each Luck of the Gambler Defense/+Recharge piece:** the defense part is boosted (15.94 → 19.92%); the 7.5% global recharge is not.
The same holds for Steadfast Protection: its resistance part is boosted, its +3% defense global is not.

**The proof that only the power's own value moves.** This build without and with the eight boosts, from the harness:

- the set-bonus virtual power, which carries every set bonus and global, is byte-identical;
- `BuffHaste` (+150%), `BuffAcc`, `BuffToHit`, `BuffDam`, `HPMax`, `HPRegen`, `EndRec`, mez protection and mez resistance are identical;
- only three totals move, each through a boosted power's own effect:
  - defense: +0.53 melee, +0.83 ranged, +0.53 AoE (Combat Training: Defensive, Combat Jumping, Maneuvers);
  - resistance: +0.03 S/L, +0.02 Psionic (Steadfast Protection);
  - `EndUse`: 1.341 → 1.329 (the endurance parts).

**Not worth their boosters:** the heavily slotted toggles (Tactical Training: Maneuvers, Fortification, Tough, Weave) keep 15% of a boost and
are not boosted. **Steadfast Protection** survives ED whole, but on a level 10 piece it adds 0.03 points for 5 boosters. It is kept in the
plan because it passes the half-survives rule; it is the operator's to skip.

### The pieces as they are in the game

The other global and unique pieces keep the levels the character holds them at, now in the file:

- the armor's Steadfast Protection at 10, Gladiator's Armor at 10 and Impervium Armor at 15;
- Aegis in Tough at 25;
- in Health, Numina's Convalescence at 30, the Preventive Medicine absorb attuned (crafted 50 in the file), Panacea at 50, Regenerative Tissue at 10 and Miracle at 20;
- the Performance Shifter proc at 21;
- level 50 IOs in Swift (Run Speed), Hurdle (Jump) and Boxing (Accuracy), as the operator confirmed.

The Luck of the Gambler global pieces, bought at 25 in game, are planned at 50: the operator's rule for a four-star build.

### Exemplar

The exemplar script starts from the engine's level 50 recompute (`compute_base_totals`) on this build's harness dumps and applies the
wiki's rules for exemplar level N:

- powers usable while their pick level ≤ N + 5;
- set bonuses and globals piece by piece (see "Unique placement"), with the rule of five;
- each enhancement value scaled by the wiki's table;
- base hit points and power magnitudes from the level N tables;
- incarnates only from 45.

**Parity at 50.** The script matches the harness on every defense, resistance and mez field, and on regeneration, recovery, endurance,
recharge, to-hit, damage and accuracy. Hit points differ by 0.10 (1,988.99 against 1,989.09). Hand check: the four crafted level 50 Luck of
the Gambler globals add 4 × 7.5 = 30% recharge at exactly exemplar 47. The script moves recharge by 30.0 at 47 and by nothing at 43 to 46
or 48 to 49, under both floor modes.

| Build, level | Melee / Ranged / AoE defense | S/L, F/C, E/N, Tox, Psi resistance | HP | Regen | Recharge | `EndUse` | KB protection |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Originally delivered (every piece at 50), 45 | 43.52 / 47.22 / 46.02 | 60.55, 65.96, 52.46, 59.96, 25.66 | 1,769 | 1.27 | +120% | 1.41 | 0 |
| Originally delivered, 35 | 43.52 / 47.22 / 46.02 | 55.65, 62.38, 48.88, 56.38, 19.26 | 1,461 | 1.27 | +120% | 1.29 | 0 |
| Originally delivered, 25 | 32.54 / 36.24 / 35.04 | 55.34, 62.12, 48.62, 56.12, 19.26 | 969 | 1.27 | +120% | 1.18 | 0 |
| **Final, 50** | 49.68 / 50.55 / 52.80 | 72.32, 68.96, 61.46, 66.71, 37.26 | 1,989 | 1.67 | +150% | 1.33 | 0 |
| **Final, 45** | 44.05 / 48.05 / 46.55 | 60.32, 65.96, 52.46, 59.96, 30.51 | 1,769 | 1.27 | +120% | 1.40 | 0 |
| **Final, 35** | 44.05 / 48.05 / 46.55 | 55.42, 62.38, 48.88, 56.38, 30.11 | 1,461 | 1.27 | +120% | 1.27 | 0 |
| **Final, 25** | 36.08 / 40.07 / 38.58 | 55.11, 62.12, 48.62, 56.12, 30.11 | 969 | 1.27 | +120% | 1.16 | 0 |

**What changed from the earlier table.** The earlier table used Mids' rule: every set bonus counts while its power's pick level is at or
below the exemplar level, and hit points use the level 50 base. On the delivered build it read 47.62 / 48.19 / 39.50 at 35 and
42.14 / 45.83 / 37.14 at 25, with 1,940 and 1,656 HP. Under the wiki's rules:

- **Melee and ranged defense are lower.** Crafted level 50 pieces lose their set bonuses below 47: Luck of the Gambler, Titanium Coating,
  Unbreakable Guard, Gaussian's and Annihilation.
- **AoE defense is higher at 35.** Superior Frozen Blast is a Winter set, so its bonuses work at every level, even while Bile Spray is
  greyed out.
- **Hit points are far lower.** Base hit points follow the class table at the exemplar level.
- **Recharge falls to +120% below 47.** The level 50 Luck of the Gambler globals stop counting there. At level 25 they counted from 22.
- **The final build** keeps AoE and ranged defense above 45% down to 35; melee is 44.05% from 45 down. Psionic resistance stays at 30% down
  to 25: Ragnarok and Superior Dominion ignore exemplaring.

### Rule findings

From `CohEngine.Cli.exe validate <mbd> --mids-dir tests/fixtures/mids --slotting` and `python -m coh_engine validate`, both run on the
final file: **no violations** from either.

## Recommended attack chains

**Method.** The measurement copy removes only Gaussian's Build Up proc. The six-attack chains run the repository's planner with Venom
Grenade left out of its attack list, so its set bonuses stay in the totals. The earlier copy swapped Venom Grenade out, which also took away
its set; on the originally delivered file that method read 91.2 / 106.4 s and this one 97.5 / 107.4 s. The "with ALT+1" lines fire Venom
Grenade on cooldown. Bar times assume no pause. The planner uses Mids' damage per activation.

Per-activation damage at the standing +44.5% damage, per target:

| Power | Damage |
| --- | --- |
| Longfang | 331 |
| Spirit Shark | 259 |
| Bile Spray | 231 |
| Frag Grenade | 227 |
| Channelgun | 226 |
| Venom Grenade | 202 |
| Arctic Breath | 201 |
| Ion Core Final Judgement | 665 |

**Single hard target, `--target single`:**

- Order: **4 Frag Grenade → 3 Spirit Shark → 1 Longfang → 2 Channelgun**. Cycle 6.86 s, gap 0.00 s.
- **155.2 damage per second.** The chain costs 2.65 end/s, so the net is −1.06 end/s and the bar lasts **98.6 s**.
- **With ALT+1:** Arctic Breath → Bile Spray → Venom → Frag → Spirit Shark → Longfang → Channelgun. Cycle 13.07 s, **171.7 damage per
  second**, net −1.35 end/s, **77.6 s**.

**Groups, `--target spawn:10`:**

- Order: **6 Arctic Breath → 5 Bile Spray → 4 Frag Grenade → 3 Spirit Shark → 1 Longfang → 2 Channelgun**. Cycle 12.63 s, gap 1.41 s.
- **667.7 damage per second.** The chain costs 2.55 end/s, so the net is −0.96 end/s and the bar lasts **108.8 s**.
- **With ALT+1:** cycle 12.63 s (gap 0.75 s), **977.8 damage per second**, net −1.26 end/s, **83.5 s**.

**Openers:**

- New spawn: ALT+2 Judgement, then ALT+1 Venom Grenade, then CTRL+1 Aim, then the cones.
- Arch-villain: ALT+1 Venom Grenade, then CTRL+1 Aim before Frag Grenade.

**Refill between fights** (arithmetic): with toggles running, the bar refills at 3.02 − 1.33 = 1.69 end/s, or about 2.35 end/s with the
Panacea and Performance Shifter procs (estimate).

- A 10 s pause returns 16.9 endurance (16% of 104.95).
- A 20 s pause returns 33.8 (32%).
- The 31 s median pause returns 52.4 (50%).

## Endurance against real encounter lengths

The operator's combat logs cover 2,972 non-farming engagements over 40 hours.

- **Typical fight:** 30 s at the median, 108 s at the 90th percentile.
- **Fight with a long-lived target:** 103 s at the median, 146 s at the 75th percentile, 210 s at the 90th.
- **Pause between fights:** 31 s at the median, and at least 18 s three times in four.

Each bar as measured and with the Panacea (0.39 end/s) and Performance Shifter (0.26 end/s) procs added. The second column is an estimate:
time to empty = 104.95 / (measured net − 0.66).

| Chain | As measured | With the procs (estimate) |
| --- | --- | --- |
| Single target, six attacks | 98.6 s | about 257 s |
| Group, six attacks | 108.8 s | about 340 s |
| Single target, with ALT+1 | 77.6 s | about 151 s |
| Group, with ALT+1 | 83.5 s | about 175 s |

- **Group chain:** 108.8 s, just past the 108 s 90th percentile of typical fights.
- **Single-target chain:** 98.6 s, 4.4 s short of the 103 s median of long fights and 47.4 s short of the 146 s 75th percentile, as measured.
- **With the procs counted,** both six-attack bars pass the 210 s 90th percentile (estimate).

These are the planner's worst case: the operator's logs show a player uses each power two to three times less often than the planner. On a
long arch-villain fight, fire Venom Grenade at its 16 s debuff duration rather than on cooldown, weave Longfang and Channelgun, and use the
team's endurance buffs. **Caveats:** the logs come from the operator's other characters and from all content; hard-mode content could not
be identified in them.

## Enhancement Diversification audit

Every power of this build, measured with `coh_engine.enh_pipeline.aggregate_and_ed` on its harness slots. These aspects pass the last
threshold (100% on schedule A, 60% on schedule B). Every one is held by a named set bonus or is a planned boost; no plain IO is past it.

| Power | Aspect | Pre | Post | What holds the slots |
| --- | --- | --- | --- | --- |
| Channelgun | Damage / Recharge | 142.4 / 132.5% | 101.4 / 99.9% | Superior Spider's Bite: +5% melee defense, +3.6% max endurance |
| Longfang | Damage | 119.2% | 97.9% | Overflow under one piece of Superior Winter's Bite |
| Tactical Training: Maneuvers | Defense | 86.1% | 59.9% | Luck of the Gambler: +9% accuracy, +3.75% Smashing/Lethal resistance |
| Venom Grenade | Damage | 145.7% | 101.9% | Ragnarok: +6% F/C (3), +15% accuracy (4), +10% recharge (5), +6% Tox/Psi resistance (6) |
| Hasten | Recharge | 106.0% | 95.9% | Two +5 IOs, the operator's norm |
| Frag Grenade | Damage / Recharge | 142.4 / 132.5% | 101.4 / 99.9% | Superior Dominion of Arachnos: +15% accuracy, +3% HP, +10% recharge, +6% Tox/Psi resistance, proc |
| Tactical Training: Leadership | To-hit | 70.1% | 57.5% | Gaussian's: +2.5% defense in every position |
| Fortification | Resistance | 70.1% | 57.5% | Titanium Coating: +1.5% HP (3), +3% S/L resistance (4) |
| Tough | Resistance | 70.1% | 57.5% | Unbreakable Guard: +3.13% melee defense (4), +3.75% Tox/Psi resistance (5) |
| Weave | Defense | 70.1% | 57.5% | Shield Wall: +2.25% HP (3), +4.5% Energy/Negative resistance (4) |
| Spirit Shark | Damage | 145.7% | 101.9% | Apocalypse five-piece |
| Bile Spray | Accuracy / Damage | 109.3 / 142.4% | 96.4 / 101.4% | Superior Frozen Blast: +5% AoE defense (5), +2.5% AoE and +5% Fire/Cold defense (6) |
| Arctic Breath | Damage | 114.0% | 97.1% | Annihilation: +3.75% AoE and +1.875% Fire/Cold defense (6) |

Every other aspect of every power is under its last threshold. That includes Combat Jumping and Maneuvers' boosted defense (55.8% before
ED), Serum's heal (95.4%) and the single IOs (42.4 to 53.0%).

## Team value

| Source | Effect | Uptime |
| --- | --- | --- |
| Tactical Training: Maneuvers | +15.99% defense, all positions and types | toggle, always |
| Maneuvers (Leadership pool) | +5.36% defense | toggle, always |
| Tactical Training: Assault, Assault | +15% damage each | toggle, always |
| Tactical Training: Leadership | +15.75% to-hit, +perception | toggle, always |
| Tactics (Leadership pool) | +10% to-hit, +perception | toggle, always |
| Venom Grenade (ALT+1) | −20% resistance, 16 s | recharges in 7.06 s |
| Arctic Breath | −15% resistance, −11.25% defense, −30% recharge and movement, 10 s | about 79% (once per 12.63 s cycle; arithmetic) |
| Annihilation proc, Reactive Interface | further −resistance | proc; not modelled |
| Channelgun, Longfang | −7.5% defense, 5 s | every hit |
| Frag Grenade | knockback; Superior Dominion's −damage and fear proc (37.9% per target) | every 4.57 s |

Together the two Maneuvers give the team +21.35% defense, and the two Assaults +30% damage.

Not covered:

- heals and absorbs for others;
- resistance buffs for others;
- regeneration debuffs;
- mez protection for teammates;
- holding aggro.

Control is light: Frag Grenade's knockback and fear proc, Ragnarok's knockdown proc, and Arctic Breath's slow.

## Pick order

Unchanged from the previous file.

| Level | Power | Opens at | Reason when later |
| --- | --- | --- | --- |
| 1 | Channelgun | 1 | |
| 1 | Crab Spider Armor Upgrade | 1 | |
| 2 | Longfang | 2 | |
| 4 | Combat Training: Defensive | 2 | Level 2 is the second attack, needed for exemplar 10 |
| 6 | Combat Jumping | 4 | Combat Training: Defensive comes first; travel by 12 |
| 8 | Maneuvers | 4 | After the travel power |
| 10 | Tactical Training: Maneuvers | 10 | |
| 12 | Venom Grenade | 12 | |
| 14 | Hasten | 4 | Picks 4 to 12 hold the defense auras and the debuff click |
| 16 | Tactical Training: Assault | 16 | |
| 18 | Frag Grenade | 18 | |
| 20 | Tactical Training: Leadership | 20 | |
| 22 | Mental Training | 22 | |
| 24 | Fortification | 24 | |
| 26 | Serum | 24 | Fortification (mez protection) first |
| 28 | Boxing | 4 | Prerequisite, placed just before Tough and Weave |
| 30 | Tough | 14 | Branch survival powers first |
| 32 | Weave | 14 | As Tough |
| 35 | Spirit Shark | 35 | |
| 38 | Bile Spray | 38 | |
| 41 | Arctic Breath | 41 | |
| 44 | Assault | 4 | The Leviathan attacks take 35, 38 and 41 |
| 47 | Tactics | 14 | As Assault; Tactical Training: Leadership gives to-hit from 20 |
| 49 | Aim | 6 | The lowest-value pick |

## Powers by level, with slotting

Each piece is named with its real kind and level:

- **Superior, attuned:** Superior Spider's Bite, Superior Winter's Bite, Superior Dominion of Arachnos, Superior Frozen Blast. No level.
- **Attuned:** the Preventive Medicine absorb global, in game. The file holds it crafted 50 (see "Unique placement").
- **Crafted, with its level:** every other piece. "Invention" pieces are common IOs, crafted at 50.

`(A)` is the free slot; numbers in brackets are the levels the added slots are placed at. "+5" marks a planned boost.

- **1 Channelgun (key 2):** Superior Spider's Bite: Accuracy/Damage (A), Accuracy/Damage/RechargeTime (3), Damage/RechargeTime (5),
  Damage/Endurance/RechargeTime (29), Accuracy/Damage/Endurance/RechargeTime (37), RechargeTime/Global Toxic (46).
- **1 Crab Spider Armor Upgrade:** Steadfast Protection: Resistance/+Def 3%, crafted 10, +5 (A); Gladiator's Armor: TP Protection +3% Def
  (All), crafted 10 (3); Impervium Armor: Psionic Resistance, crafted 15 (21).
- **2 Longfang (key 1):** Superior Winter's Bite: Accuracy/Damage (A), Accuracy/Damage/Endurance (5), Damage/RechargeTime (25),
  Accuracy/Damage/Recharge (45).
- **4 Combat Training: Defensive:** Luck of the Gambler: Defense/Increased Global Recharge Speed, crafted 50, +5 (A); Shield Wall: +Res
  (Teleportation), +5% Res (All), crafted 50 (9).
- **6 Combat Jumping:** Luck of the Gambler, crafted 50, +5: Defense/Increased Global Recharge Speed (A), Defense/Endurance (7),
  Defense/Endurance/Recharge (7).
- **8 Maneuvers:** Luck of the Gambler, crafted 50, +5: Defense/Increased Global Recharge Speed (A), Defense/Endurance (13),
  Defense/Endurance/Recharge (34).
- **10 Tactical Training: Maneuvers:** Luck of the Gambler, crafted 50: Defense/Increased Global Recharge Speed (A), Defense (11),
  Defense/Endurance (11), Defense/Endurance/Recharge (34), Defense/Recharge (45).
- **12 Venom Grenade (ALT+1):** Ragnarok, crafted 50: Damage/Recharge/Accuracy (A), Recharge/Accuracy (15), Damage/Recharge (19),
  Damage/Endurance (29), Damage (40), Chance for Knockdown (48).
- **14 Hasten (auto-fire):** Invention: Recharge Reduction +5 (A), Recharge Reduction +5 (17).
- **16 Tactical Training: Assault:** Invention: Endurance Reduction +5 (A).
- **18 Frag Grenade (key 4):** Superior Dominion of Arachnos: Accuracy/Damage (A), Accuracy/Damage/Recharge (19), Damage/Recharge (23),
  Damage/Endurance/Recharge (31), Accuracy/Damage/Endurance/Recharge (40), Recharge/Chance for -Dmg and Terrorize (48).
- **20 Tactical Training: Leadership:** Gaussian's Synchronized Fire-Control, crafted 50: To Hit Buff (A), To Hit Buff/Recharge (21),
  To Hit Buff/Recharge/Endurance (23), Recharge/Endurance (33), To Hit Buff/Endurance (40), Chance for Build Up (48).
- **22 Mental Training:** Invention: Run Speed +5 (A).
- **24 Fortification:** Titanium Coating, crafted 50: Resistance/Endurance (A), Resistance/Recharge (25), Resistance/Endurance/Recharge
  (27), Resistance (45).
- **26 Serum (CTRL+2):** Preventive Medicine, crafted 50: Heal (A), Heal/Endurance (27), Heal/RechargeTime (36).
- **28 Boxing (prerequisite, not on a key):** Invention: Accuracy, crafted 50 (A).
- **30 Tough:** Unbreakable Guard: Resistance, crafted 50 (A); Aegis: Psionic/Status Resistance, crafted 25 (31); Unbreakable Guard: +Max HP
  (31), Resistance/Endurance (34), RechargeTime/Resistance (43), Resistance/Endurance/RechargeTime (50), crafted 50.
- **32 Weave:** Reactive Defenses: Scaling Resist Damage, crafted 50 (A); Shield Wall, crafted 50: Defense/Endurance (33), Defense/Recharge
  (33), Defense/Endurance/Recharge (37), Defense (46).
- **35 Spirit Shark (key 3):** Apocalypse, crafted 50: Damage/Recharge/Accuracy (A), Recharge/Accuracy (36), Damage/Recharge (36),
  Damage/Endurance (37), Damage (46).
- **38 Bile Spray (key 5):** Superior Frozen Blast: Accuracy/Damage (A), Accuracy/Damage/Endurance (39), Accuracy/Damage/Recharge (39),
  Damage/Endurance/Accuracy/RechargeTime (39), Damage/Endurance (43), Recharge/Chance for Immobilize (50).
- **41 Arctic Breath (key 6):** Annihilation, crafted 50: Accuracy/Damage (A), Accuracy/Damage/RechargeTime (42), Accuracy/Damage/Endurance
  (42), Accuracy/Damage/Endurance/RechargeTime (42), Damage/RechargeTime (43), Chance for Res Debuff (50).
- **44 Assault:** Invention: Endurance Reduction +5 (A).
- **47 Tactics:** Invention: Endurance Reduction +5 (A).
- **49 Aim (CTRL+1):** Invention: Recharge Reduction +5 (A).
- **Swift:** Invention: Run Speed, crafted 50 (A). **Hurdle:** Invention: Jump, crafted 50 (A).
- **Brawl:** Invention: Accuracy, crafted 50 (A). **Sprint:** Invention: Run Speed, crafted 50 (A). **Rest:** Invention: Endurance
  Modification, crafted 50 (A).
- **Health:** Numina's Convalescence: +Regeneration/+Recovery, crafted 30 (A); Preventive Medicine: Chance for +Absorb, attuned in game, crafted 50 in the file (9);
  Panacea: +Hit Points/Endurance, crafted 50 (13); Regenerative Tissue: +Regeneration, crafted 10 (15); Miracle: +Recovery, crafted 20 (17).
- **Stamina:** Performance Shifter: Chance for +End, crafted 21 (A).

### Slot accounting

The build uses 67 added slots, 24 picks and 4 pools. Every power's slot levels are identical to the originally delivered file, checked slot
by slot; listing the pools in pick order moved none.

### Verbatim MidsReborn export (power lines)

The export prints neither the +5 boosts nor the piece levels; the `.mbd` carries both.

```text
Level 1:	Channelgun			SprSpdBit-Acc/Dmg(A), SprSpdBit-Acc/Dmg/Rchg(3), SprSpdBit-Dmg/Rchg(5), SprSpdBit-Dmg/EndRdx/Rchg(29), SprSpdBit-Acc/Dmg/EndRdx/Rchg(37), SprSpdBit-Rchg/Global Toxic(46)
Level 1:	Crab Spider Armor Upgrade		StdPrt-ResDam/Def+(A), GldArm-3defTpProc(3), ImpArm-ResPsi(21)
Level 2:	Longfang			SprWntBit-Acc/Dmg(A), SprWntBit-Acc/Dmg/EndRdx(5), SprWntBit-Dmg/Rchg(25), SprWntBit-Acc/Dmg/Rchg(45)
Level 4:	Combat Training: Defensive		LucoftheG-Def/Rchg+(A), ShlWal-ResDam/Re TP(9)
Level 6:	Combat Jumping			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(7), LucoftheG-Def/EndRdx/Rchg(7)
Level 8:	Maneuvers			LucoftheG-Def/Rchg+(A), LucoftheG-Def/EndRdx(13), LucoftheG-Def/EndRdx/Rchg(34)
Level 10:	Tactical Training: Maneuvers		LucoftheG-Def/Rchg+(A), LucoftheG-Def(11), LucoftheG-Def/EndRdx(11), LucoftheG-Def/EndRdx/Rchg(34), LucoftheG-Def/Rchg(45)
Level 12:	Venom Grenade			Rgn-Acc/Dmg/Rchg(A), Rgn-Acc/Rchg(15), Rgn-Dmg/Rchg(19), Rgn-Dmg/EndRdx(29), Rgn-Dmg(40), Rgn-Knock%(48)
Level 14:	Hasten				RechRdx-I(A), RechRdx-I(17)
Level 16:	Tactical Training: Assault		EndRdx-I(A)
Level 18:	Frag Grenade			SprDmnofA-Acc/Dmg(A), SprDmnofA-Acc/Dmg/Rchg(19), SprDmnofA-Dmg/Rchg(23), SprDmnofA-Dmg/EndRdx/Rchg(31), SprDmnofA-Acc/Dmg/EndRdx/Rchg(40), SprDmnofA-Rchg/DmgFear%(48)
Level 20:	Tactical Training: Leadership		GssSynFr--ToHit(A), GssSynFr--ToHit/Rchg(21), GssSynFr--ToHit/Rchg/EndRdx(23), GssSynFr--Rchg/EndRdx(33), GssSynFr--ToHit/EndRdx(40), GssSynFr--Build%(48)
Level 22:	Mental Training			Run-I(A)
Level 24:	Fortification			TtnCtn-ResDam/EndRdx(A), TtnCtn-ResDam/Rchg(25), TtnCtn-ResDam/EndRdx/Rchg(27), TtnCtn-ResDam(45)
Level 26:	Serum				Prv-Heal(A), Prv-Heal/EndRdx(27), Prv-Heal/Rchg(36)
Level 28:	Boxing				Acc-I(A)
Level 30:	Tough				UnbGrd-ResDam(A), Ags-Psi/Status(31), UnbGrd-Max HP%(31), UnbGrd-ResDam/EndRdx(34), UnbGrd-Rchg/ResDam(43), UnbGrd-ResDam/EndRdx/Rchg(50)
Level 32:	Weave				Rct-ResDam%(A), ShlWal-Def/EndRdx(33), ShlWal-Def/Rchg(33), ShlWal-Def/EndRdx/Rchg(37), ShlWal-Def(46)
Level 35:	Spirit Shark			Apc-Acc/Dmg/Rchg(A), Apc-Acc/Rchg(36), Apc-Dmg/Rchg(36), Apc-Dmg/EndRdx(37), Apc-Dmg(46)
Level 38:	Bile Spray			SprFrzBls-Acc/Dmg(A), SprFrzBls-Acc/Dmg/EndRdx(39), SprFrzBls-Acc/Dmg/Rchg(39), SprFrzBls-Dmg/EndRdx/Acc/Rchg(39), SprFrzBls-Dmg/EndRdx(43), SprFrzBls-Rchg/ImmobProc(50)
Level 41:	Arctic Breath			Ann-Acc/Dmg(A), Ann-Acc/Dmg/Rchg(42), Ann-Acc/Dmg/EndRdx(42), Ann-Acc/Dmg/EndRdx/Rchg(42), Ann-Dmg/Rchg(43), Ann-ResDeb%(50)
Level 44:	Assault				EndRdx-I(A)
Level 47:	Tactics				EndRdx-I(A)
Level 49:	Aim				RechRdx-I(A)
Level 1:	Brawl				Acc-I(A)
Level 1:	Sprint				Run-I(A)
Level 2:	Rest				EndMod-I(A)
Level 1:	Swift				Run-I(A)
Level 1:	Hurdle				Jump-I(A)
Level 1:	Health				NmnCnv-Regen/Rcvry+(A), Prv-Absorb%(9), Pnc-Heal/+End(13), RgnTss-Regen+(15), Mrc-Rcvry+(17)
Level 1:	Stamina				PrfShf-End%(A)
```

## Incarnates (all tier 4; Lore left empty, as 4-star bans pets)

- **Alpha: Cardiac Core Paragon.** The operator chose it. On an earlier layout Agility Core gave 3 to 5 more points of defense but cost
  5.7 points of Smashing/Lethal resistance and cut both bars to under half. Agility was not re-run on this layout.
- **Judgement: Ion Core Final Judgement** (ALT+2). 665 per activation.
- **Interface: Reactive Radial Flawless Interface.** Its −resistance stacks with Venom Grenade and Arctic Breath.
- **Destiny: Barrier Core Epiphany** (CTRL+4). Off in the totals. An experienced hard-mode player reports that Barrier is the best
  all-round Destiny for hard mode.
  - On an eight-player team, six take Barrier and two float (Clarion, Rebirth or Ageless). With six Barriers the rotation is two at once
    every 30 seconds.
  - Clarion is situational, for a team with little mez protection.
  - Three and four stars at level 55 bring large increases in enemy accuracy, damage, hit points, regeneration and defense.
  - This build takes Barrier Core to be one of the six.
- **Hybrid: Assault Radial Embodiment** (CTRL+3). The petless choice in the VEAT handbook. Off in the totals.

## Pets: left out

Summon Spiderlings and Call Reinforcements are not taken. The petless guide reports the pets "get thrashed at +4", and a pet summon would
cost a pick the build cannot spare. Lore pets are banned in 4-star play.

## Trade-offs

- **Frag Grenade keeps its knockback.** In four-star content most enemies resist knockback and knockdown, so the build keeps Superior
  Dominion's bonuses and proc instead of converting it. There is no knockback protection.
- **The single-target bar is 98.6 s against the 146 s target**, as measured. With the procs counted it is past it (estimate).
- **Exemplared below 47, recharge falls to +120% and melee defense to 44.05%.** The level 50 Luck of the Gambler pieces stop counting there.
  AoE and ranged defense stay above 45% down to 35.
- **Recharge is +150%.** Hasten and Serum are up about 92% of the time.

## 4-star play

- Stay at range behind the tank.
- **New spawn:** ALT+2, ALT+1, CTRL+1, then 6 → 5 → 4, and fill with 3, 1, 2.
- **Arch-villain:** ALT+1 every 16 s, 6 when it is up, and 4 → 3 → 1 → 2 between.
- **Before the pull:** CTRL+2 Serum. Hasten fires itself on auto.
- **Below 31% health** the Preventive Medicine absorb fires: about 398 points with Serum up, at most once every 120 s.
- Keep the six Leadership toggles and Fortification up.

## What I would verify

- **Standing to-hit** (+25.75%) in game, and **the 0.65 level factor**, which is approximate.
- **The boost value on the level 10 Steadfast Protection** (Mids' 1.25 times the piece's own value).
- **The absorb, Panacea and Performance Shifter procs:** none is modelled by Mids. Check their trigger rate in the combat log.
- **The survival score's planning figures** (enemy base chance to hit, the accuracy multiplier, the damage mixes); see
  `docs/survivability-planning.md`.
- **Exemplar totals:** computed by the wiki's rules piece by piece, not measured in game.

## Guides used (paraphrased, short quotes only)

Under `homecoming-source-material/community/archetypes/`:

- `arachnos/the-spider-s-handbook-an-incomplete-guide-to-veats.md` — "The Spider's Handbook: An Incomplete Guide to VEATs." Leadership
  double stacks, Venom Grenade's resistance debuff, the Leviathan AoE chain, the Reactive Interface and the Assault Hybrid.
- `arachnos/my-crab-build-petless-but-well-rounded.md` — "My Crab build: petless, but well-rounded."
- `arachnos/crab-discussion-omega-maneuver-worth-it.md` — "Crab discussion: Omega Maneuver worth it?"
- `tanker/inv-rad-hardmode-version-i-hope.md` — "Inv/Rad: Hardmode version (I hope?)."
- The Homecoming wiki's Enhancement Booster Salvage page, for the boost rule.

## Files

Sibling files in this folder, all named "crab-ranged-4-star" plus an ending: `.mbd`, `.md`,
`.mids-totals.json`.
