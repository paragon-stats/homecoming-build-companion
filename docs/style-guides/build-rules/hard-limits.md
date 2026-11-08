# Rule: Honor the hard game limits

**Status:** MUST. These are invariant constraints from the Homecoming game itself. A build that satisfies all of them is valid; a build that violates any is invalid.

Track these as continuous constraints during draft, not as at-end checks.

## Character-level limits

- **50 character levels.** Powers and slots granted on a fixed schedule (`Levels.json`).
- **24 power picks** for non-inherent powers: 1 + 1 at level 1, then 1 every other level
  (2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 35, 38, 41, 44, 47, 49). Inherent powers are auto-granted and don't count.
- **67 added enhancement slots per character** (`ServerData.MaxSlots`).
  The Homecoming database sets `ServerData.EnableInherentSlotting = false` (verified in `MidsReborn/Databases/Homecoming/SData.mhd`),
  so the `HealthSlots`/`StaminaSlots` bonus slots are **not** granted.
  Only each power's single inherent (A) slot sits outside the 67; every other enhancement — Health and Stamina included — is drawn from this 67.
  Budget Health/Stamina slots against the cap like any other power.
- **6 enhancement slots per power max** (`Power.MaxBoosts`): 1 inherent + up to 5 added.

## Power gating

- Each power has a `Level` field — the earliest level at which it can be picked. Pick the power at or after that level.
- Tier-9 primaries unlock at level 32 (verify per powerset in `Powers.json`).
- Ancillary / Patron Pool tier-1 unlocks at level 35.
- Pool tier prerequisites: T1/T2 before T3 before T4 within a pool.
- 4 power pools per build max.

## Slot rules

A slot placed at level X satisfies all three:

1. X is a slot-grant level in the in-game schedule.
2. The receiving power was picked at level ≤ X.
3. The receiving power has fewer than 6 total slots before the addition.

Slot placements are ascending across the build. Confirm by walking levels 2–50 in order and verifying each slot lands on a real grant level in a power picked at or before that level.

## Enhancement rules

- **Enhancement Diversification (ED).** Same-aspect stacking past ~95% diminishes sharply. ED applies per-aspect, per-power.
  Set bonuses, global buffs, procs, inherent power buffs, and across-power buffs sit outside ED.
- **Rule of Five.** Up to 5 instances of any *exact* numeric set-bonus value stack across the build. Different values from different sources stack independently.
- **Unique globals.** Slot once per build.

## AT-specific caps

Per [`data/canonical/archetypes/<at>.json`](../../../data/canonical/archetypes/) and [`strategy/breakpoints.json`](../../../strategy/breakpoints.json):

- **Resist cap** — Tanker 90%; all others 75%.
- **Damage cap** — varies (Brute up to 850%; Blaster 500%; most others 400%).
- **Recharge cap** — 500% global (post-buff).
- **Regen cap** — typically 2000%.
- **Recovery cap** — typically 500%.
- **HP cap** — varies; see archetype JSON.

Build estimates land at or under these caps.

## Validation pass

Run all checks before emitting:

1. Total added slots ≤ 67.
2. Per-power added slots ≤ 5.
3. Power picks = 24 (excluding inherents).
4. Each power picked at or after its `Level` field.
5. Each picked powerset is in the AT's allowed list (Primary[], Secondary[], Ancillary[], or universal pools).
6. Pool sets ≤ 4.
7. Tier prerequisites respected within pool sets.
8. Slot placements ascending and at real grant levels.
9. Stat estimates land at or under AT caps.

A build that passes all 9 is valid. When a draft fails one, surface the failure and propose the closest valid alternative before emitting.
