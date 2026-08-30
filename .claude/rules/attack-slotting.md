# Rule: Attack slotting — accuracy in slot (A) and first added slot

**Status:** MUST. Applies to every attack power on every archetype emitted in this project. Exceptions exist (listed below) and must be flagged explicitly when used.

## What is required

Fill the **inherent slot (A)** and the **first added slot** of every attack with
**accuracy-bearing IOs** — set pieces that include Accuracy as one of their aspects
(Acc/Dmg, Acc/Dmg/Rchg, Acc/Dmg/EndRdx, Acc/Dmg/EndRdx/Rchg, etc.).
Damage-only or Dmg/Rchg pieces appear no earlier than slot 3.

Default slotting order, slots 1–6:

```text
(A)  Acc/Dmg
  2  Acc/Dmg/X     (X = Rchg or EndRdx)
  3  Dmg/X
  4  Dmg/X
  5  Dmg/X
  6  proc, or the set's ATO crit-proc piece, or a slot-in proc (FF +Recharge, Achilles' Heel, etc.)
```

Exact aspect picks depend on the set's available pieces.

## Why

The build is intended to remain playable at exemplar 10 (see [`exemplar-10.md`](./exemplar-10.md)). At low exemplar:

- Set bonuses below the −3 cutoff are gone.
- IOs work at their minimum-level enhancement values.
- Global to-hit buffs (Tactics, Kismet: Accuracy +6%, Focused Accuracy) may not be picked or unlocked yet.
- The slotted accuracy enhancement is what holds the attack above the +0 ToHit floor (75% baseline = 50% chance + 25% built-in attacker bonus).

Missed attacks waste the same endurance as landed ones.
A 75%-hit attack costing 10 end is effectively **13.3 end per landed hit**;
a 95%-hit attack at 10 end is **10.5 end per hit**.
Endurance bottlenecks a build long before damage ceilings do — especially before
Stamina slot 1 unlocks at character level 12.

## How to apply

- ATO and purple sets generally include an Acc/Dmg piece — that piece goes in slot (A).
- The second added slot takes an Acc/Dmg/Rchg or Acc/Dmg/EndRdx piece from the same set.
- "Damage-only" pieces (Hecatomb: Damage/Recharge, Armageddon: Damage/Recharge) move to slot 3 or later, never slots 1–2.
- For single-target attacks, AoEs, cones, snipes, damage auras, and any other power that rolls to-hit — apply the rule.
- For auto-hit powers (status toggles, Genetic Contamination, debuff toggles like Darkest Night) — no to-hit roll occurs, so no accuracy slotting is required.

## Allowed exceptions, flag explicitly

When the rule is intentionally not followed, the build's trade-offs section names the attack and the reason:

- **Pre-buffed snipe** where Aim + Tactics + Kismet: Accuracy +6% already meets the 22% to-hit threshold for fast-snipe activation. No slotted accuracy needed beyond the inherent set piece.
- **Procmonster vehicle** — a fast-recharge low-damage attack held purely as a proc
  carrier. The proc rolls aren't gated by the player's to-hit chance against the proc's
  effect, so accuracy slotting is lower priority.
- **AT-inherent accuracy boost** — e.g. Stalker Assassin's Strike from Hide grants high to-hit; the slotting profile changes accordingly.
- **Auto-hit attacks** — confirm via canonical data that `accuracy = -1` (auto-hit) or that no to-hit check fires.

Any deviation outside these exception types is an error per [`error-output.md`](./error-output.md), validated as `P-SLOT-001` by the build validator.

## Reference

- [`exemplar-10.md`](./exemplar-10.md) — universal exemplar-10 playability rule that this rule supports.
- [`build-creation.md`](./build-creation.md) § P9 — references this rule in the pre-flight checklist.
- [`io-set-naming.md`](./io-set-naming.md) — full IO set names in prose when applying this rule.
