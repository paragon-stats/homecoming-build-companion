# Enhancements and IO Sets

Enhancements are slotted into powers to improve them. There are several distinct **types**, each with different rules.
**Invention Origin (IO) Sets** are the endgame system: slot 2+ pieces from the same set into one power and you collect **set bonuses** that apply to your whole character.

Authoritative data: [data/mids/database/Enhancement.json](../data/mids/database/Enhancement.json), [data/mids/database/EnhancementClasses.json](../data/mids/database/EnhancementClasses.json).
[data/mids/derived/effect_tables.json](../data/mids/derived/effect_tables.json).

---

## Enhancement type taxonomy

`Enhancement.TypeID` and `Enhancement.SubTypeID` (from `MidsReborn/Core/Enhancement.cs`):

| Type                       | Subtype         | Examples                                      | Notable mechanics                                                                                                           |
| -------------------------- | --------------- | --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| **TO** (Training Origin)   | Normal          | "Training: Damage", "Training: Accuracy"      | Lowest grade. Schedule A: 8% Damage. Cheap, level 1+.                                                                       |
| **DO** (Dual Origin)       | Normal          | "DO: Magic/Mutation Damage"                   | Mid grade. Origin-restricted. ~16% Damage.                                                                                  |
| **SO** (Single Origin)     | Normal          | "SO: Magic Damage"                            | Pre-IO endgame. ~33% Damage at +0 to +3 level.                                                                              |
| **HO** (Hamidon / Special) | SpecialO        | "Hami-O: Damage/Mez Resist"                   | Multi-aspect (two stats in one slot). Level 50 only. Subject to ED on each aspect.                                          |
| **IO** (Invention Origin)  | InventO         | "Damage IO Lvl 50"                            | Crafted single-aspect IO. IOLevel 10–50. ~42% Damage at lvl 50. No set bonus.                                               |
| **Set IO**                 | SetO            | "Crushing Impact: Damage", LotG, Numina, etc. | Member of an `EnhancementSet`. Carries set bonuses by piece count (1–6 pieces slotted).                                     |
| **Attuned** flag           | SetO + Attuned  | "Crushing Impact: Damage (Attuned)"           | Always exemplars to your character level. No "+5 boost" support but works at any level.                                     |
| **Superior** flag          | SetO + Superior | ATO+ purples (e.g. "Superior Avalanche")      | ×1.25 multiplier on the base. Granted by ATO unlock missions; also crafted from purples.                                    |
| **Boosters**               | flag on IOs     | "Booster +5"                                  | RelativeLevel +1 to +5 above the base IOLevel. Stacks with the IO's level cap (so a level-50 IO +5 = effectively level 55). |
| **Catalyst-converted**     | flag            | n/a                                           | Converts a set IO to its Attuned form.                                                                                      |

### Enhancement schedules (A/B/C/D)

Each enhancement type belongs to a **schedule** that controls its multiplier:

- **Schedule A** — strongest (Damage, Heal, Mez, etc.). SO at +0 = 33.3%. IO at lvl 50 = 42.4%.
- **Schedule B** — medium (Defense, ToHit, Recovery, EndRdx, Recharge, Range, Run/Fly/Jump, Accuracy). SO at +0 = 21%. IO at lvl 50 = 26.5%.
- **Schedule C** — weak (Confuse, Stealth, KB Distance, ToHit Debuff). SO at +0 = 12%. IO at lvl 50 = 16%.
- **Schedule D** — special (Range Increase). SO at +0 = 8%.

Multiplier tables: `data/mids/derived/effect_tables.json::MultED/TO/DO/SO/IO`.

### RelativeLevel (the "level shift" for non-attuned enhancements)

Crafted IOs and SOs have a `RelativeLevel` from −3 to +5 relative to the character's level:

- **+5 / +4 / +3** — overcap; full strength.
- **+2 / +1** — full strength.
- **+0** — full strength, baseline.
- **−1** — −10% effectiveness.
- **−2** — −20%.
- **−3** — turns red, severely reduced (~−40%).
- **Below −3** — does not enhance at all.

**Boosters** push an IO's RelativeLevel up by +1 per booster (max +5). A level-50 IO with 5 boosters performs as if it were level 55 (full strength + 5% bonus per shift).

Attuned IOs always equal character level (RelativeLevel = 0); they sidestep the level-shift system entirely.

---

## IO Sets and set bonuses

An **IO Set** (e.g. _Luck of the Gambler_) is a group of related IOs (typically 5 or 6 distinct IOs in the set).
Slot two or more from the same set into the same power and you collect set bonuses applied to your whole character.

### Bonus structure

Each `EnhancementSet` has:

- `Bonus[5]` — five tiers of bonuses, awarded for slotting 2/3/4/5/6 pieces.
- `SpecialBonus[6]` — additional bonuses tied to specific named IOs in the set.
  (e.g. "LotG: +Recharge" is a special bonus that applies whenever you slot the named IO, independent of how many other LotG pieces are in the power).

Two examples:

**Luck of the Gambler (Defense set):**

- 2 pcs: +10% Regen
- 3 pcs: +9% Accuracy
- 4 pcs: +7.5% Recharge
- 5 pcs: +9% +Hold/Sleep/Confuse Resistance
- 6 pcs: (no 6th in this set)
- **Special:** "LotG: 7.5% Recharge" — a unique enhancement that grants 7.5% global recharge whenever slotted; you can stack up to 5 of these across the build.

**Crushing Impact (Melee Damage set):**

- 2 pcs: +1.88% HP
- 3 pcs: +7% Accuracy
- 4 pcs: +5% Recharge
- 5 pcs: +3% Damage Buff
- 6 pcs: +2.5% Negative Energy / Toxic Resistance

### Rule of Five

A set bonus value of "+7.5% Recharge" can stack at most **5 times** across the entire build. So if you have 5 LotG +Recharge globals, slotting a 6th gives you no additional bonus.
You can have many _different_ recharge bonuses (e.g. 5x 7.5%, plus 5x 5%, plus 5x 3%) — each numeric value caps independently.

This is the "Rule of Five" — internalize it before recommending any set.

### Set categories (which sets a power accepts)

A power's `SetTypes[]` array tells you which IO set categories can slot in:

- **Single-target attacks** typically accept: Melee Damage, Ranged Damage (or both via "Universal Damage"), Knockback, etc.
- **AoE attacks** accept: Targeted AoE, PBAoE Damage, etc.
- **Defense toggles** accept: Defense, Resist Damage.
- **Click heals** accept: Healing.
- **Mez powers** accept: Holds, Stuns, Sleeps, Immobilizes, etc.
- **Travel powers** accept: Travel (run, fly, jump), Stealth.

Use the power's `SetTypes[]` directly — don't guess.

### Attuned vs. crafted

- **Attuned** IOs auto-exemplar to your character level. Buy with merits or Empyrean Merits. No level shift, no "+5" support.
- **Crafted** IOs are level-specific (lvl 10–50 in 5-level steps). At your level you get full strength; below your level you take RelativeLevel penalties.

For most modern builds, **attuned is preferred** unless you specifically want to stack +5 boosters on a level-50 piece.

### Superior IOs (purples and ATOs)

- **Purple sets** (Apocalypse, Hecatomb, Ragnarok, Armageddon, etc.) — global rarity, available at level 50+, full set bonuses on first piece, very strong damage procs.
- **Archetype Origin (ATO) sets** — AT-specific, drop from missions or convertible. Each AT has 2 ATO sets.
  **Superior ATO** versions are unlocked via Hero/Villain Merit purchase; the Superior versions get ×1.25 multiplier on the enhancement aspects + improved set bonuses.

---

## Slotting decision rules

When picking enhancements for a power:

1. **Check `SetTypes[]`** — which set categories can slot here?
2. **Decide goal**: do I want set bonuses (slot a single set), or maximum stat enhancement (Frankenslot a mix), or procs (slot proc IOs)?
3. **For set bonuses**: pick the set whose bonuses align with the build goal; slot the right number of pieces (often 5 or 6 for the full chain).
4. **For Frankenslotting**: pick single-aspect IOs from different sets that maximize the desired aspects (e.g. Damage + Accuracy + Recharge in a single-target attack).
5. **For procs**: long-recharge slow attacks → damage procs (PPM works in your favor); fast AoEs → mostly avoid procs (PPM punishes them).

See [docs/min-maxing.md](min-maxing.md) and [docs/proc-strategy.md](proc-strategy.md) for the deeper strategy.

---

## Common slotting patterns

- **Attack power**: 5–6 piece set bonus IOs (Crushing Impact for melee, Decimation/Thunderstrike for ranged), or Frankenslot Damage/Accuracy/Recharge/EndRdx single-aspects.
- **Defense toggle**: 5x LotG (5x +Recharge globals + the 5-piece bonus), or 6x Reactive Defenses (typed defense + scaling resist).
- **Resist toggle**: 6x Unbreakable Guard (Resist + max HP global), or Frankenslotted Resist + EndRdx.
- **Heal click**: 5–6 piece Numina's Convalescence (regen + recovery global) or Doctored Wounds (recharge bonus).
- **Hold/Stun/Sleep**: 5–6 piece Basilisk's Gaze (recharge), Lockdown (chance for +2 Mag Hold), or Coercive Persuasion (purple-tier).
- **Travel**: 1x LotG +Recharge global (in Combat Jumping or Hover), 1x Celerity Stealth proc (in Sprint), 1x Blessing of the Zephyr (KB protection in any travel power).
- **Health (inherent)**: Numina's +Regen/+Recovery (unique), Miracle +Recovery (unique), Panacea +Health/+End (unique), Regenerative Tissue +Regen (unique).
  These four go in Health, +1 LotG slot for the 4th IO.
- **Stamina (inherent)**: Performance Shifter +End proc (unique), Power Transfer +Heal proc (unique), then 2-3 EndMod IOs.
