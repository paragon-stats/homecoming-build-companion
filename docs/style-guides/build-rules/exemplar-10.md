# Rule: Exemplar-10 playability (universal)

**Status:** MUST. Applies to every build emitted in this project.

## Priority order

1. **Optimize for level-50 endgame.** Pick powers and enhancements that maximize level-50 performance.
2. **Verify exemplar-10 playability.** The character can attack, has a defensive layer running, has a travel power, contributes to a team. *Playable*, not optimized.
3. **Level 50 wins on conflict.** PvP set-bonus persistence at deep exemplar is informational; treat it as gravy when present, never as a slotting goal.

## What "playable at exemplar 10" means

When exemplared to level 10, the available powers and slots include the build's *functional core*:

- ≥ 2 attacks usable at exemplar 10 (picked at level ≤ 15).
- ≥ 1 defensive layer running where the AT has one (toggle picked at level ≤ 15, with at least 2 added slots placed at level ≤ 15).
- A travel power picked **by level 12** at the latest. Hard floor.
- Build Up / Aim / equivalent damage spike if the AT has one (picked by level 15).
- A sustain mechanic (heal click, regen, drain) by level 15 if the AT requires one.

A build that satisfies these is playable at exemplar 10. That's the bar.

## Mechanics summary

- **Power availability — the +5 rule.** Exemplared to N, you have access to powers picked at level ≤ N + 5. At exemplar 10 → picks at levels 1, 1, 2, 4, 6, 8, 10, 12, 14 are available.
- **Slot availability — also the +5 rule.** Slots placed at level X are available while effective level ≥ X − 5. Slots placed at level 16 and later don't exist on the character at exemplar 10.
- **IO set bonuses — the −3 rule (regular sets).** Bonuses persist while effective level ≥ (set min level − 3).
  Most meta sets (min 30+) lose bonuses at exemplar 10.
  *That's expected* — the build's level-50 set bonuses still work at level 50, where the character lives most of the time.
  - **Engine note (Mids-modeled vs actual):** This −3 rule is *actual in-game* behavior and remains the guidance for human build-authoring.
    MidsReborn does **not** implement it — Mids retains a power's set bonuses whenever the power's pick level ≤ the exemplar (Force) level, with no −3 offset.
    The build-calculation engine (see [`docs/engine/mids-port-spec.md`](../../engine/mids-port-spec.md)) matches Mids as its reference implementation,
    so any exemplar totals it reports are labeled *Mids-modeled* and are optimistic at deep exemplar relative to live CoH.
- **PvP IO set bonuses — informational exception.** Persist at every exemplar level. Slot PvP sets when their level-50 bonuses are competitive with the alternative; never as an exemplar-driven choice.
- **ATO sets, Winter-O sets** — min level 10, persist at exemplar 10 by the −3 rule. ATOs slot at level 10; Winter-Os are attuned-only.
- **Purples** — min 50; lose bonuses below effective 47. Slot freely at level 50; loss on exemplar is acceptable.
- **Incarnates** — Alpha shift applies only at exemplar 45+. No incarnate buffs at exemplar 10. The build functions on its baseline at low exemplar.

## What this rule actually constrains

The constraint is almost entirely on **pick ordering**, which matches normal leveling anyway.
A character that levels through 1–50 *naturally* picks attacks early, gets a defensive layer online by 8–10, takes a travel power around 12.

The rule's job is to confirm the build doesn't deliberately *defer* essentials to chase some level-50 trick.

## Slot-aspect priority for attacks (accuracy-first)

The order of *enhancement aspects* in attack slots matters as much as the order of pick levels — at low exemplar, set bonuses fall off and the slotted enhancement values are what carry the attack.

**Default attack slotting order, slots 1–6:**

1. **Slot (A), inherent:** Acc/Dmg combo (e.g. `SprScrStr-Acc/Dmg`, `Hct-Acc/Dmg/Rchg`, `Mako-Acc/Dmg`).
2. **Slot 2, first added:** another Acc-bearing combo from the same set (`Acc/Dmg/Rchg`, `Acc/Dmg/EndRdx`, `Acc/Dmg/EndRdx/Rchg`).
3. **Slots 3–5:** damage-bearing combos without accuracy (`Dmg/Rchg`, `Dmg/EndRdx/Rchg`).
4. **Slot 6:** the set's proc, the AT-set crit proc, or a slot-in proc (FF +Recharge, AchHee -Res, etc.).

**Why accuracy-first:** at exemplar 10–20, set bonuses below their −3 cutoff are gone, IOs work at their minimum-level enhancement values,
and global to-hit buffs (Tactics, Kismet, Focused Accuracy) may not yet be available.
The slotted Acc enhancement is what holds the attack above the +0 ToHit floor (75% with default 50% chance + 25% built-in attacker bonus).
Missed attacks waste the same endurance as landed ones; a 75%-hit attack at 10 end is effectively 13.3 end per landed hit, a 95%-hit attack at 10 end is 10.5.
Endurance bottlenecks a build long before damage ceilings do.

**Exceptions, flag explicitly:**

- Pre-buffed snipes (Aim + Tactics) where the to-hit threshold is met without slotted accuracy.
- Attacks held purely as procmonster vehicles where proc rolls aren't gated by player to-hit.
- AT inherents with built-in accuracy boosters (Stalker Assassin's Strike from Hide, etc.).
- Auto-hit powers (Genetic Contamination, status auras, most debuff toggles) — no to-hit roll, no need for accuracy.

## Pre-emit verification

Before emitting a build, confirm:

- The 9 picks made at levels 1–14 cover the functional core (above).
- Slots placed at levels 1–15 populate exemplar-10-available powers, not just late-game heavy hitters.
- Defensive toggles have ≥ 2 added slots by character level 15.
- Anchor attack has ≥ 2 added slots by character level 14.
- Every attack's inherent slot **(A)** holds an Acc-bearing IO; every attack's first added slot also holds an Acc-bearing IO. Damage-only or Dmg/Rchg pieces don't appear before slot 3.
- Travel power picked at level ≤ 12.
- The level-50 build remains the optimization target. When exemplar playability and a level-50 optimization conflict, level 50 wins; explain the trade-off and proceed.

## Reference

Full mechanics, lookup tables, and IO-type breakdown: [`docs/slotting-and-leveling.md` § Exemplar mechanics](../../slotting-and-leveling.md#exemplar-mechanics-and-the-universal-exemplar-10-rule).
