# Slotting and Leveling

The level rules govern when you get power picks, when you get enhancement slots, and where powers are gated by tier. These are hard limits — a build that violates them is invalid.

Authoritative data:
[data/mids/database/Levels.json](../data/mids/database/Levels.json) (LevelMap array — the canonical schedule) and
[data/mids/derived/server_constants.json](../data/mids/derived/server_constants.json) (ServerData constants).

---

## The level schedule (Homecoming, levels 1–50)

Each level tick grants either a power pick, slots, both, or neither. The pattern below is the modern Homecoming version (post-i26 power-pick changes); confirm against `Levels.json` when in doubt.

| Level | Powers granted | Slots granted | Notable milestones                                                                                |
| :---: | :------------: | :-----------: | ------------------------------------------------------------------------------------------------- |
|   1   |  2 (P1 + S1)   |       0       | Primary tier-1 + Secondary tier-1; AT inherent (Brawl, Sprint, Rest, Health, Stamina via Fitness) |
|   2   |       1        |       1       | First slot at level 2                                                                             |
|   3   |       0        |       1       |                                                                                                   |
|   4   |       1        |       0       | First pool/travel power can be picked                                                             |
|   5   |       0        |       1       |                                                                                                   |
|   6   |       1        |       0       |                                                                                                   |
|   7   |       0        |       1       |                                                                                                   |
|   8   |       1        |       1       | **Health inherent slot 1** (server constant `HealthSlot1Level=8`)                                 |
|   9   |       0        |       1       |                                                                                                   |
|  10   |       1        |       0       |                                                                                                   |
|  11   |       0        |       1       |                                                                                                   |
|  12   |       1        |       1       | **Stamina inherent slot 1** (`StaminaSlot1Level=12`)                                              |
|  13   |       0        |       1       |                                                                                                   |
|  14   |       1        |       0       |                                                                                                   |
|  15   |       0        |       1       |                                                                                                   |
|  16   |       1        |       1       | **Health inherent slot 2** (`HealthSlot2Level=16`)                                                |
|  17   |       0        |       1       |                                                                                                   |
|  18   |       1        |       0       |                                                                                                   |
|  19   |       0        |       1       |                                                                                                   |
|  20   |       1        |       1       |                                                                                                   |
|  21   |       0        |       1       |                                                                                                   |
|  22   |       1        |       1       | **Stamina inherent slot 2** (`StaminaSlot2Level=22`)                                              |
|  23   |       0        |       1       |                                                                                                   |
|  24   |       1        |       0       |                                                                                                   |
|  25   |       0        |       1       |                                                                                                   |
|  26   |       1        |       1       |                                                                                                   |
|  27   |       0        |       1       |                                                                                                   |
|  28   |       1        |       0       |                                                                                                   |
|  29   |       0        |       1       |                                                                                                   |
|  30   |       1        |       1       |                                                                                                   |
|  31   |       0        |       1       |                                                                                                   |
|  32   |       1        |       0       | **Tier-9 primary unlocks** (varies by powerset; 32 is most common)                                |
|  33   |       0        |       1       |                                                                                                   |
|  34   |       0        |       0       | (gap)                                                                                             |
|  35   |       1        |       1       | **Ancillary / Patron Pool tier-1 unlocks**                                                        |
|  36   |       0        |       0       |                                                                                                   |
|  37   |       0        |       1       |                                                                                                   |
|  38   |       1        |       0       | **Tier-9 secondary** typically unlocks                                                            |
|  39   |       0        |       1       |                                                                                                   |
|  40   |       0        |       0       |                                                                                                   |
|  41   |       0        |       1       |                                                                                                   |
|  42   |       0        |       0       |                                                                                                   |
|  43   |       0        |       1       |                                                                                                   |
|  44   |       0        |       0       |                                                                                                   |
|  45   |       0        |       1       |                                                                                                   |
|  46   |       0        |       0       |                                                                                                   |
|  47   |       0        |       1       |                                                                                                   |
|  48   |       0        |       0       |                                                                                                   |
|  49   |       0        |       1       |                                                                                                   |
|  50   |       0        |       0       | Incarnate slot Alpha unlocks via Mender Ramiel arc                                                |

**Always verify with `Levels.json`** — the schedule above is the standard Homecoming pattern, but a future patch could shift it.

---

## Slot budget

- Total power picks (excluding inherents): **24** (one per level granted).
- Total enhancement slots: **67** (`ServerData.MaxSlots = 67`) — 4 inherent (3 from Fitness + 1 from Health) + 63 placed.
- Per-power slot cap: **6** (1 inherent slot that comes with the power + 5 added). `Power.MaxBoosts = 6` for slottable powers.

You cannot place more than 5 added slots in a single power.
You cannot place more than 67 added slots total.
The leveling schedule controls when those slots become available — typically you have all 67 by mid-40s.

---

## Power-tier gating

A power's `Level` field is the **earliest** level at which you can pick it. This implies a tier system within each powerset:

| Tier | Typical unlock level | What it usually is                  |
| :--: | :------------------: | ----------------------------------- |
|  T1  |          1           | First attack / first defense toggle |
|  T2  |          1           | Second attack / second toggle       |
|  T3  |          2           | Stronger attack / utility           |
|  T4  |          6           | Mid-tier attack                     |
|  T5  |          8           | Click defense / first heal          |
|  T6  |          12          | Stronger attack                     |
|  T7  |          18          | Major buff / Build Up / Aim         |
|  T8  |          26          | Heavy hitter                        |
|  T9  |          32          | Crash power / signature ult         |

Pool powers have their own tier schedule:

- Pool T1: level 4
- Pool T2: level 4 (most pools)
- Pool T3: level 14
- Pool T4: level 18

Ancillary / Patron pools are gated to level 35 (T1) → 41 (T2) → 41 (T3) → 44 (T4).

You must take at least one tier-1 or tier-2 power from a pool before tier-3, and tier-3 before tier-4.

---

## Inherent powers

Inherent powers don't take a power pick — they're auto-granted. They DO take enhancement slots.

- **Brawl** (level 1) — universal melee attack. Slottable. Skipped by most builds (one inherent slot only).
- **Sprint** (level 1) — toggle run speed. Slot 1x Celerity +Stealth proc and call it done.
- **Rest** (level 1) — out-of-combat heal. Optional 1–2 slots for global heal proc.
- **Fitness pool** (free since i19 on Homecoming):
  - **Health** — auto-granted, 2 slots from Health@8 + Health@16 system. Standard slotting: Numina +Regen/+Recovery, Miracle +Recovery, Panacea +HP/+End, Regenerative Tissue +Regen.
  - **Stamina** — auto-granted, 2 slots from Stamina@12 + Stamina@22 system. Standard slotting: Performance Shifter +End proc + 2x EndMod IOs (or Performance Shifter set bonuses).
  - **Swift** / **Hurdle** — passive movement; rarely slotted beyond inherent.

The four inherent slots from Health/Stamina are critical real estate — they give you 4 of the strongest global procs in the game without using any of your 67 added slots.

### Powers MidsReborn grants without a pick

A saved `.mbd` has no `Chosen` flag, so a validated `.mbd` counts every granted pool sub-power as a pick.
Only the Pool-group grants can make `H-POWER-001` over-count; the Inherent-group ones are excluded by name.
Rows marked "not in the repository" trace to a web source that was read for this table only.

| Power | Granted by | Game change | Source |
| --- | --- | --- | --- |
| Swift, Hurdle, Health, Stamina | every character (Inherent group) | Issue 19 (launch patch notes dated 2010-11-30) made the four Fitness powers inherent at level 2; Issue 27 Page 2 moved the grant to level 1 | In the repository: this document, above, and `community/mechanics/guide-to-power-pools.md`. Not in the repository: [Issue 19 launch notes](https://forumarchive.cityofheroes.dev/topic/245825), [Homecoming wiki, Fitness](https://homecoming.wiki/wiki/Fitness) |
| Translocation, Arcane Power | Mystic Flight and the Sorcery pool (Pool group) | Pool Powerset Update: Sorcery, 20 April 2021 | In the repository: `data/diff/patch_history/Issue_27_Page_2.md` (the Translocation endurance row only); the page is [Issue 27 Page 2](https://homecoming.wiki/wiki/Issue_27_Page_2). The grant of Arcane Power itself is not traced |
| Double Jump, Afterburner, Speed Phase | the Jump, Flight and Speed pools (Pool group) | Travel Power Updates, Issue 27 | Not traced: the repository holds only the Sorcery rows and no release note naming these grants was found |
| Fast Snipe | a prerequisite power named in its `Requires` (Inherent group; present in the samples at level 50) | The snipe rework | Not traced: no source found in the repository or the web search |
| Prestige sprints, Prestige Ninja Run, Prestige Athletic Run | the Prestige power-slide purchases (Inherent group) | The Pay-to-Win store | In the repository: `community/mechanics/a-p2w-store-primer.md` |

---

## Powerset availability rules

Each AT publishes `Primary[]`, `Secondary[]`, and `Ancillary[]` arrays of allowed powerset indices in `Archetypes.json`.
The pool list is universal but you can only take **4 pools** per build (each with up to 4 powers).

**Hard-gated powers** (level requirement above what `Level` says):

- **Travel powers** historically required taking a tier-1 or tier-2 prerequisite from the same pool. As of late Homecoming, this prerequisite has been relaxed for many pools.
- **Tier-9 primaries with crashes** (Hibernate, Unstoppable, Light Form, etc.) — usable from level 32 but typically saved as emergency buttons.

---

## Reading `Levels.json`

The structure is a parallel array indexed by level (0-based for level 1, etc.):

```json
[
  {"Powers": 2, "Slots": 0},   // level 1
  {"Powers": 1, "Slots": 1},   // level 2
  ...
  {"Powers": 0, "Slots": 1},   // level 49
  {"Powers": 0, "Slots": 0}    // level 50
]
```

`LevelType()` derives whether a level grants a power, a slot, both, or neither. See `MidsReborn/Core/LevelMap.cs`.

---

## Build validation rules

For every build Claude proposes, verify:

1. **Slot count**: total added slots ≤ 67. (Inherent power-pick slots and Fitness slots don't count toward 67.)
2. **Per-power slot cap**: ≤ 5 added slots in any one power.
3. **Power picks ≤ 24** for non-inherent picks.
4. **Each power picked at or after its `Level` field** in `Powers.json`.
5. **Each picked powerset is in the AT's allowed list** (Primary[], Secondary[], Ancillary[], or universal pools).
6. **No more than 4 pool sets**.
7. **Tier prerequisites within a pool** (T1/T2 before T3 before T4).
8. **Exemplar-to-10 viability** — see the next section. This is a universal house rule and applies to every build.

If a build violates any of these, name the violation and suggest the closest valid alternative.

---

## Exemplar mechanics and the universal exemplar-10 rule

Exemplaring is when the game temporarily lowers your character's effective level.
It happens when teaming with lower-level players, when running task forces that mandate level brackets (Posi 1 caps you at 16, Synapse at 20, etc.), or when you Ouroboros into older-content arcs.

### Priority order — read this first

The build target is **level-50 endgame play.** That's where all the optimization goes — slot choices, set bonuses, perma-Hasten, Incarnates, the works.
The exemplar-10 rule sits *underneath* that as a playability floor, not a competing optimization target.

Concretely:

1. **Level-50 endgame quality is the primary objective.** Pick powers and enhancements that maximize the level-50 build.
2. **Exemplar-10 must be playable** — the character can attack, has some defensive layer running, has a travel power, isn't dead weight.
    That's it.
    Not optimized; not fully soft-capped; not hitting any specific number.
    *Playable.*
3. **Never trade level-50 quality for exemplar bonuses.** If retaining a set bonus at exemplar 10 would weaken the level-50 build, don't do it.
    PvP IO set persistence (below) is interesting trivia, not a slotting goal.

The constraint is therefore almost entirely about **pick ordering** (the early picks happen to land in the levels that exemplar 10 can use)
and **occasional enhancement choices** (use attuned IOs where they're equally good for level 50; otherwise slot for level 50).
It is *not* about chasing exemplar set bonuses.

### Mechanics (reference, not a slotting agenda)

- **Power availability — the +5 rule.** When exemplared to level N, you have access to powers picked at level ≤ N + 5.
  At exemplar 10 → powers picked at levels 1, 1, 2, 4, 6, 8, 10, 12, 14 are available.
  The pick scheduled at 16 and later is not.
- **Slot availability — also +5.** Slots placed at level X in any power are accessible while your effective level ≥ X − 5.
  Slots placed at level 16 and later don't exist on your character at exemplar 10.
- **IO set bonuses — the −3 rule (regular IO sets).** A regular set's bonuses persist while your effective level ≥ (set's minimum craftable level) − 3.
  Most meta IO sets have min level 30 → bonuses lost at effective level < 27.
  **Most level-50 builds will lose most of their set bonuses at exemplar 10.
  That is fine.** The build's level-50 set bonuses still work at level 50, which is where it lives most of the time.
- **PvP IO set bonuses — exemplar-agnostic (informational).**
  PvP IO sets (Shield Wall, Panacea, Gladiator's Armor / Net / Strike / Javelin / Rage, etc.) retain their set bonuses at every exemplar level.
  **This is interesting trivia, not a slotting recommendation.** Use PvP sets when their level-50 bonuses are competitive with the alternative for the slot — not because they survive exemplar.
- **ATO and Winter-O sets — also retain at exemplar 10.** Both are attuned-only, min level 10, and 10 ≥ 10 − 3 = 7.
  ATOs slot at level 10 (a useful scaling perk during leveling, not a constraint on level-50 build choice).
  Catalyze to Superior at 50 for the level-50 benefit, not for the exemplar effect.
- **Attuned IOs (in general).** Per-IO enhancement values scale to your effective level, which prevents the level penalty crafted IOs take below their crafted level.
  **Use attuned IOs when they're as good as the crafted alternative for the level-50 build.**
  Set bonuses for attuned IOs still follow the −3 rule by *set* min level — attuned regular sets still lose bonuses below (set_min − 3).
- **Purples — bonuses lost at any exemplar < 47.** Min level 50, so −3 puts the floor at 47.
  **Slot purples freely; they're a level-50 optimization.** They're useless on deep exemplar but that's not a reason to avoid them.
- **Incarnates.** Alpha shift applies only when exemplared to 45+. At exemplar 10, no incarnate buffs apply. Build for level 50 with full Incarnate kit and accept the reduced power at exemplar 10.

### What this actually means for build design

Most of the rule reduces to **pick ordering**, which matches normal leveling anyway.
A character that levels through the 1–50 range *naturally* picks attacks early, gets a defensive layer online by level 8–10, takes a travel power around 12.
The exemplar-10 rule is mostly a check that the build doesn't deliberately *defer* essentials to chase some level-50 trick.

**Pick-level checklist** (just confirm before emitting a build):

- **Tier-1 and Tier-2 of primary by level 1.** Free at character creation; don't skip.
- **Tier-1 of secondary by level 1.** Same — free.
- **At least one strong primary attack by level 6.** Anchors the low-level attack chain.
- **A defensive layer by level 8** for ATs that have one in the secondary (Tank/Brute/Scrap/Stalk/Sent). For squishy ATs, this is whatever the AT relies on (toggles, drains, control, etc.).
- **Build Up / Aim / equivalent by level 8** if the AT has one.
- **Travel power picked by level 12 at the latest.** Hard floor.
- **Mez protection (where applicable) by level 12.**
- **A sustain mechanic (heal click, regen, drain) by level 12** if the AT depends on one.

If the build naturally picks these in this order during leveling, the exemplar rule is satisfied without further effort.

**Slot-level checklist:**

- **At least three slots in your anchor attack by level 14** (inherent + 2 added).
- **Defensive slots in the relevant toggles by level 14** (at least 2 added).
- **Don't dump early slots into Brawl or Sprint.** Inherents are fine 1-slotted.

**Enhancement checklist (level-50 first; exemplar bonuses are a side effect):**

- **Slot for the level-50 build first.** Pick the IO sets and pieces that maximize level-50 performance. Don't let exemplar viability change what gets slotted at 50.
- **Prefer attuned IOs when they're as good as crafted for level 50.** They scale automatically and avoid the exemplar penalty as a free bonus.
  If a crafted +5 boosted level-50 IO is *better* at 50, take it.
- **ATOs slot at level 10 if you're playing through the leveling content.** This isn't a level-50 build constraint, just a "you'll have ATO bonuses earlier" perk.
- **Don't pick PvP sets for exemplar set-bonus persistence.** Pick them when their level-50 bonuses are competitive with the alternative. The exemplar persistence is gravy when it happens.

### Validation when proposing builds

When emitting a build, verify:

- The 9 picks made at levels 1–14 cover: attacks (≥ 2), defensive layer (≥ 1) where AT has one, travel power (1, by level 12), Build Up / Aim / spike (1 if available), sustain (≥ 1 if AT requires).
- Slots placed at levels 1–15 populate those exemplar-10 powers, not just late-game heavy hitters.
- The level-50 build is *not* compromised. If exemplar viability and a level-50 optimization conflict, level 50 wins; explain the trade-off and proceed.
