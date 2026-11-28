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

## Pick order

A build lists its power picks in the order the game's level-up view offers them.

- Each pick sits on the level the schedule grants its position among the 24. (H-POWER-003)
- The two level 1 picks are a primary pick and then a secondary pick. (H-POWER-006)
  A pair the other way round fails, and so does any other pair whose first pick is not in the primary's category or whose second is not in the secondary's:
  two picks from one category, even from two of its sets, or a pick from neither category.

The check compares the text before the first dot of each level 1 pick's power name with the text before the first dot of `PowerSets[0]` (primary) and `PowerSets[1]` (secondary).
`Blaster_Ranged.Fire_Blast.Flares` is a primary pick in a file whose `PowerSets[0]` is `Blaster_Ranged.Fire_Blast`.
The comparison is exact, letter case included, as the file and the Mids database spell the names.
That first part is the category the archetype's `primary_category` and `secondary_category` fields name in `data/canonical/archetypes/`,
but the game data spells some categories in another case (`Blaster_RANGED`), so the check reads the file, not those fields.
An Arachnos Soldier's or Widow's base sets and branch sets sit in one category (`Arachnos_Soldiers` holds `Arachnos_Soldier`, `Bane_Spider_Soldier` and `Crab_Spider_Soldier`),
so a level 1 pick from the base sets counts as primary or secondary in a file that lists the branch sets.

The respec view lets a player place picks and slots in any order and does not show the level a slot was gained at; it is not the reference order.
A Mids dump directory has no `PowerSets`, so `validate` and `check_hard_limits` on a dump do not check the level 1 pair; on a `.mbd` file both do.
The `.mbd` writer puts the primary pick first at level 1 and refuses, with E46, any pair the check would fail; both read one decision (`LevelOnePair` in `Registry/HardLimitRules/`).

## Slot rules

A slot placed at level X satisfies all three:

1. X is a slot-grant level in the in-game schedule.
2. The receiving power was picked at level ≤ X.
3. The receiving power has fewer than 6 total slots before the addition.

Slot placements are ascending across the build. Confirm by walking levels 2–50 in order and verifying each slot lands on a real grant level in a power picked at or before that level.

## Enhancement rules

- **Enhancement Diversification (ED).** Same-aspect stacking past ~95% diminishes sharply. ED applies per-aspect, per-power.
  Set bonuses, global buffs, procs, inherent power buffs, and across-power buffs sit outside ED.
  ED is a game mechanic, so an over-slotted power is legal; slotting past it is a P-ED-001 or P-ED-002 warning under `validate --slotting`.
  See [`slotting.md`](./slotting.md).
- **Rule of Five.** Up to 5 instances of any *exact* numeric set-bonus value stack across the build. Different values from different sources stack independently.
  (author guidance, no registered check)
- **Unique globals.** Slot once per build. (H-ENH-004)

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

1. Total added slots ≤ 67. (H-SLOT-001)
2. Per-power added slots ≤ 5. (H-SLOT-002)
3. Power picks = 24 (excluding inherents). (H-POWER-001)
4. Each power picked at or after its `Level` field. (H-POWER-002)
5. Picks in the level-up view's order: each on its position's schedule level, and at level 1 one primary pick and then one secondary pick. (H-POWER-003, H-POWER-006)
6. Each picked powerset is in the AT's allowed list (Primary[], Secondary[], Ancillary[], or universal pools). (author guidance, no registered check)
7. Pool sets ≤ 4. (H-POOL-001)
8. Tier prerequisites respected within pool sets. (H-POWER-004, H-POWER-005)
9. Slot placements ascending and at real grant levels. (H-SLOT-003, H-SLOT-004, H-SLOT-005)
10. Stat estimates land at or under AT caps. (author guidance, no registered check)

A build that passes all 10 is valid. When a draft fails one, surface the failure and propose the closest valid alternative before emitting.
