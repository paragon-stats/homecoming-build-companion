# Lessons — TW/Bio Scrapper v1 vs. seasoned-expert builds

**Status:** case study.
Read this before producing a TW/Bio Scrapper build, and when generalizing to any other Scrapper / Brute / Tanker combo where Bio Armor is the secondary or where Titan Weapons is the primary.

The v1 TW/Bio Scrapper Claude produced as a benchmark comparison failed in concrete ways captured in [`style-guides/build-rules/build-creation.md`](style-guides/build-rules/build-creation.md) (see L1–L8).
Those lessons addressed *format and process* failures — slot notation, slot accounting, JSON+markdown round-tripping, citing canonical numbers.

Beyond format, four community TW/Bio builds (Jaegernault, brasilgringo, mythozz, beowulfinia —
  see [`community/builds/scrapper/`](../community/builds/scrapper/) and [`community/archetypes/scrapper/jaegernault-guide-build-tw-bio.md`](../community/archetypes/scrapper/jaegernault-guide-build-tw-bio.md))
  reveal *strategic* assumptions that experts converge on but training memory tends to miss.
Those are recorded here.

The pattern: Claude's defaults treat each power's slotting in isolation, optimize each slot for the slotted power's own output,
and pick from "what's good in this slot" without modeling the whole-build economy.
Experts model the build economy first and let individual slots fall out of it.

---

## 1. Soul Mastery is the default ancillary, and Shadow Meld is *why*

**Claude's assumption:** the ancillary pool is a place to add a ranged attack and a defense layer; Mu / Soul / Body / Mace are roughly interchangeable on a melee AT, pick whichever pairs thematically.

**What three of four experts did:** Soul Mastery, taken explicitly for **Shadow Meld** (32% def to all, 15s up / 25s base recharge — perma-able with the build's recharge bonuses).
Moonbeam is the secondary Soul pick: a Hami-O `HO:Centri` mule (beowulfinia) or a 6-piece Superior Winter's Bite set (brasilgringo),
used as a runner-stopper / AV-separator more than a steady-state attack.

The fourth expert (mythozz) skipped the ancillary entirely — but only to spend those four picks on a deeper Leadership commitment (Maneuvers + Vengeance + Assault + the LotG-mule layer that comes with).
The ancillary is opportunity-cost, not architecture.

**What to take from this:** TW/Bio's defensive ceiling without the ancillary sits around 25% def in standard play (per Jaegernault: Tough+Weave +13%, Maneuvers +5%, CJ +2.5%, P2W amplifier +5%).
Bio's Adaptation toggles and set bonuses lift that, but the gap to the 45% soft cap is closed by **Shadow Meld** as a clickable defense spike, not by stacking more toggles.
When proposing a TW/Bio build, default to Soul Mastery unless you have a specific reason to skip it; if you skip it,
document the trade like mythozz did (more global +Rch via Leadership pool LotG mules).

## 2. The "procmonster" pattern is the rule, not the exception

**Claude's assumption:** slot Whirling Smash, Rend Armor, and Arc of Destruction with a 6-piece purple set (Armageddon, Hecatomb, Obliteration) for the maximum set bonuses.

**What experts did:** **5 pieces** of the purple set, plus a damage proc or a Force Feedback +Recharge in the 6th slot.
All four builds load Whirling Smash with Armageddon + FF +Recharge proc.
Rend Armor takes Hecatomb 5-piece (sometimes with the `Hct-Dam%` proc as the 5th, sometimes `AchHee-ResDeb%` or `FrcFdb` in slot 6).
Arc of Destruction takes Superior Critical Strikes 6-piece (3 of 4 builds) — the AT-set's `+50% Crit` proc is core, not optional.
Whirling Smash and Arc of Destruction are the FF +Recharge proc homes because both are long-recharge AoEs that proc the +100% recharge buff frequently.

**What to take from this:** every long-recharge attack on a melee AT is a procmonster candidate. The 6th piece of a purple set is almost always worse than:

- A `FrcFdb-Rechg%` proc (in any KB-tagged power — Whirling Smash, Follow Through, Rend Armor)
- An `AchHee-ResDeb%` proc (universal -res debuff)
- A `Hct-Dam%` / `Arm-Dam%` / `Obl-%Dam` proc
- The set's own ATO crit-proc (`SprCrtStr-Rchg/+50% Crit`, `SprScrStr-Rchg/+Crit`)

PPM math (see [`procs.md`](procs.md)) explains it: a 4-second-recharge attack procs at ~25%; a 20-second-recharge AoE procs at ~85% and triggers FF +Recharge, which feeds back into the next attack.
The 6th-piece bonus is one fixed number; the proc is recursive value.

## 3. Hardened Carapace is the +Def-unique bucket, not "the resist toggle"

**Claude's assumption:** Hardened Carapace is a resist toggle, slot it for resist (Aegis 4-piece for the S/L bonus, or Unbreakable Guard for resist + HP).

**What experts did:** layered the resist set *and* dropped both `+3% Def (All)` uniques into it. Standard pattern:

- **brasilgringo:** Aegis 4-piece + Steadfast Protection +3% Def + Gladiator's Armor +3% Def TP proc (6 slots).
- **beowulfinia:** Unbreakable Guard 4-piece + Steadfast Protection +3% Def + Gladiator's Armor +3% Def TP proc (6 slots).
- **mythozz:** Gladiator's Armor 6-piece (the proc is a member of the set — same outcome).

The two unique +Def globals (Steadfast + Gladiator's Armor) are 6% def-to-all baseline; on a TW/Bio they're worth more than any single set's S/L bonus.
They live in Hardened Carapace because Hardened Carapace is the always-on resist set with 6 slots available — no other always-on resist toggle exists pre-Tough.

(Jaegernault placed both uniques in Tough instead. Both placements are valid; the choice is "which resist set has the spare slot.")

**What to take from this:** every melee build with a resist set in its primary/secondary needs to plan for the two +Def uniques.
They're **the** highest-density defense bonus in the game per slot.
The slotting question is *which* resist toggle hosts them, not *whether* to take them.

## 4. Inexhaustible is a healing-set mule, not a heal

**Claude's assumption:** Inexhaustible is Bio's regen passive; slot it for heal/regen value.

**What experts did:** slot it for the **set bonuses**, never for the heal value.

- **beowulfinia:** Numina's Convalescence 5-piece (heal/end, regen/recovery proc, heal/rech, heal/end/rech, heal). Slotted for the set's +12% regen + 6% recovery + 7.5% recharge spread.
- **mythozz:** single Numina's `Regen/Rcvry+` (the +regen/+recovery proc) and nothing else.
- **brasilgringo:** Preventive Medicine `Absorb%` + Panacea `Heal/+End` + Power Transfer `+Heal` + Preventive Medicine `Heal` (4 globally-useful uniques in a passive).
- **Jaegernault:** Panacea `+End` proc + Panacea `Heal` (for +Max HP) + Performance Shifter `+End` proc.

None of them slot for heal magnitude. The passive's heal scales with the AT's regen formula and is not improved meaningfully by enhancement.

**What to take from this:** Bio's Inexhaustible (and analogous always-on regen passives
  — Stone's Earth's Embrace, Willpower's Fast Healing/High Pain Tolerance, Regen's Quick Recovery) are *unique-global homes*, not active heals.
Slot them like Health and Stamina — for the set bonus + procs, not for the heal number on the power itself.

## 5. Parasitic Aura is recharge-only

**Claude's assumption:** Parasitic Aura is Bio's T9 PBAoE heal/absorb; slot it heavily for heal.

**What experts did:** **2× Recharge IO** and nothing else (Jaegernault explicit; brasilgringo and beowulfinia confirmed — single `RechRdx-I` slotted in the inherent slot only).
The reason given by Jaegernault and beowulfinia: the absorb caps at the AT's base HP (~1300 for Scrappers), so heal slotting doesn't extend the cap.
Recharge is the only lever that increases the power's value (more frequent shields).

**What to take from this:** powers with a hard-capped output (absorb capped at base HP, damage capped at AT cap, etc.) reject set-bonus slotting because the marginal slot doesn't improve output.
Recharge is the only meaningful enhancement.
This generalizes to:

- Bio's Ablative Carapace (capped absorb, but only ~50% of base HP, so heal slotting still helps fill the cap — 6-piece Numina's or Preventive Medicine here is correct).
- Bio's Parasitic Aura (capped absorb at base HP, recharge-only).
- Most "panic button" defensives (One With the Shield, Hibernate, etc.).

Cap-bound powers want recharge; under-cap powers want set bonuses. Check the power's mechanics before slotting heal/absorb sets in.

## 6. Genetic Contamination is a set-bonus mule + knockdown proc, not a damage aura

**Claude's assumption:** Genetic Contamination is a damage aura, slot Obliteration / Eradication 6-piece for damage.

**What experts did:** slot it for **the Avalanche `Rchg/KDProc`** (knockdown proc) and a procmonster grab-bag.

- **mythozz:** Obliteration `%Dam` proc + Avalanche `Rchg/KDProc` +
  Fury of the Gladiator `ResDeb%` proc + Eradication `%Dam` proc + Scirocco's Dervish `Dam%` proc + Fury of the Gladiator `Acc/End/Rech` (all 6 slots are procs or uniques).
- **brasilgringo:** Superior Avalanche 5-piece + Fury of the Gladiator `ResDeb%` proc.
- **beowulfinia:** Obliteration 6-piece (a more conventional set choice).

Per Jaegernault: "knockdown is 100% defence for 2 seconds." The Avalanche KD proc (in a damage aura that hits every 2 seconds) keeps adjacent enemies face-planted,
which on a melee build is more valuable than the aura's tick damage.

**What to take from this:** damage auras with a built-in PPM trigger are knockdown-proc carriers.
Slot for the proc and the set bonuses; the aura's own damage is already low and procs don't appreciably scale it.
The same pattern applies to other small-damage auras (Death Shroud, Mud Pots, Blazing Aura) — recharge / proc / set-bonus slotting beats pure damage slotting.

## 7. Defensive Sweep and Titan Sweep are leveling picks, not final-build picks

**Claude's assumption:** every primary T1/T3 attack belongs in the chain.

**What experts did:** **drop them on the final build.** Jaegernault is explicit: "Defensive Sweep and Titan Sweep are levelling picks, skippable on the final build." brasilgringo, mythozz,
and beowulfinia all skip Defensive Sweep entirely; mythozz takes Defensive Sweep at level 1 as a LotG `+Rch` mule with no other slots and never uses it as an attack.

The TW attack chain at endgame is **Crushing Blow → Follow Through → Rend Armor → Arc of Destruction → Whirling Smash**, with Build Up sliding in.
Defensive Sweep is for the early levels when Crushing Blow's recharge is slow and Build Momentum's three-tier system needs feeding.
By 50 with set bonuses, the chain runs without it.

**What to take from this:** "every primary attack in the chain" is a low-level habit.
At endgame with 100%+ global recharge, fast-recharging T1s become slot-budget drains.
Bias toward dropping low-DPS T1s and using their slots elsewhere; the sole exception is when a T1 is the LotG-mule home (mythozz's Defensive Sweep at 1 slot), at which point it's a set-bonus slot,
not an attack.

## 8. Hami-Os and IO-frankenslots fill the gaps where set bonuses don't matter

**Claude's assumption:** every slot is a set IO; Hami-Os are legacy and rarely used.

**What experts did:** drop a Hami-O into single-slot powers where set bonuses don't matter and a multi-aspect IO maximizes the slot's enhancement value.

- **beowulfinia:** `HO:Ribo` (Damage / Resist) in Evolving Armor (1 slot, resist toggle, set bonuses elsewhere). `HO:Centri` (Damage / Endurance) in Moonbeam (1 slot, ranged-attack).
- **brasilgringo:** Evolving Armor takes a single `UnbGrd-Max HP%` proc — the Hami equivalent.
- **mythozz:** Evolving Armor takes a single `StdPrt-ResDam/EndRdx`.

Evolving Armor is a 1-slot toggle on every TW/Bio build because Bio's defense set bonuses come from Hardened Carapace, Environmental Modification, and Ablative Carapace;
spending more than 1 slot on Evolving Armor double-pays for resistance Bio already has from elsewhere.

**What to take from this:** Hami-Os and the `+Max HP` / `+Resist` unique procs are the right answer when:

1. The slot is a 1-slot mule.
2. Set bonuses elsewhere already cover the role.
3. Multi-aspect enhancement (Damage / End, Resist / Heal, etc.) is more useful than single-aspect.

The build economy treats slots as scarce; spending 6 slots to set-bonus a power that's already covered is wasted budget.

## 9. The "rule of 5" is a real ceiling, not a soft guideline

**Claude's assumption:** stack defense set bonuses freely; more is better.

**What experts did:** spread defense bonuses across **different sets** to avoid the rule-of-5 cap (only 5 instances of a given numeric bonus stack).

Jaegernault explicit: "Skip the pure-damage 6th piece of purple sets —
diminishing returns make it wasted." His Maneuvers / Weave use Red Fortune (5-piece) for the same defense tail that the LotG `Def/+Rch` provides;
he doesn't 6-piece both with LotG because the +7.5% recharge is the only LotG bonus that stacks past 5 instances, and he's already using LotG `+Rch` in 5 slots elsewhere.

**What to take from this:** the LotG `+Rch` global is a hard 5-of-5 ceiling (it stacks to 37.5% global recharge and stops).
Don't slot a 6th LotG looking for more recharge — slot it looking for a *different* set's bonus tail.
See [`strategy/set_bonus_stacking_rules.json`](../strategy/set_bonus_stacking_rules.json) and [`docs/min-maxing.md`](min-maxing.md) for the full bonus-stacking ceiling list.

## 10. Accuracy lives in the inherent and first added slot of every attack

**Claude's assumption:** the inherent slot is whatever set piece "fits the role"; damage-bearing pieces are interchangeable; the set's overall enhancement totals are what matter.

**What experts did:** front-load **Acc-bearing IOs** in the inherent (A) slot and the first added slot of every attack, every time.

- **brasilgringo Crushing Blow:** `(A) SprScrStr-Acc/Dmg`, `(3) SprScrStr-Dmg/Rchg`, `(3) SprScrStr-Acc/Dmg/Rchg`, `(5) SprScrStr-Acc/Dmg/EndRdx/Rchg` — Acc in slot (A),
  Acc again in the third slot (a 2nd Acc-bearing piece by the time both slot-3 pieces are placed).
- **brasilgringo Rend Armor:** `(A) Hct-Dmg/Rchg`, `(3) Hct-Acc/Dmg/Rchg` — Acc-bearing combo in slot 2.
- **beowulfinia Crushing Blow:** `(A) SprScrStr-Acc/Dmg`, `(13) SprScrStr-Dmg/Rchg`, `(15) SprScrStr-Acc/Dmg/Rchg`, `(15) SprScrStr-Dmg/EndRdx/Rchg` — same pattern, Acc in slot (A),
  Acc/Dmg/Rchg as the next Acc-bearing piece.
- **mythozz Rend Armor:** `(A) Hct-Dmg`, `(25) Hct-Dmg/Rchg`, `(25) Hct-Acc/Dmg/Rchg`, `(27) Hct-Acc/Rchg` — even when the (A) slot is Dmg-only,
  two Acc-bearing pieces follow within the next two added slots.

The pattern across all four builds: an Acc-bearing IO appears in the slot (A) and either slot 2 or slot 3 of every attack.
When the inherent slot has a non-Acc piece (Hct-Dmg, Hct-Dmg/Rchg), two Acc-bearing pieces appear in the next two added slots to compensate.

**Why:** at low exemplar, set bonuses below their −3 cutoff are gone, IOs work at minimum-level enhancement values,
and global to-hit buffs (Tactics, Kismet, Focused Accuracy) may not be picked or unlocked yet.
The slotted Acc enhancement holds the attack above the +0 ToHit floor (75% with default).
Missed attacks waste the same endurance as landed ones — a 75%-hit attack at 10 end is effectively 13.3 end per landed hit; a 95%-hit attack at 10 end is 10.5.
Endurance bottlenecks a build long before damage ceilings do, especially before Stamina slot 1 unlocks at level 12.

**What to take from this:** default attack slotting order is **Acc/Dmg → Acc/Dmg/X → Dmg/X → Dmg/X → Dmg/X → proc**.
Damage-only pieces never appear before slot 3.
The codified rule is in [`exemplar-10.md` § Slot-aspect priority for attacks](style-guides/build-rules/exemplar-10.md#slot-aspect-priority-for-attacks-accuracy-first);
flag exceptions explicitly (pre-buffed snipes, procmonster vehicles, auto-hit powers).

## 11. Slot allocation has a budget; experts run lean

**Claude's assumption:** powers want as many slots as can be put in them.

**What experts did:** make hard cuts.
Crushing Blow / Follow Through / Rend Armor / Whirling Smash / Arc of Destruction get 5–6 slots each (the heavy hitters).
Hardened Carapace / Ablative Carapace get 5–6 (the survival layer + uniques).
Combat Jumping / Maneuvers / Weave / Scorpion Shield (defense toggles) get 1–2 slots each (LotG `+Rch` mule + occasional set tail).
Hasten gets 2 slots.
**Hami-O / single-slot powers** absorb the rest of the budget where set bonuses are irrelevant.

The 67-slot cap is *binding*. Every build above hits exactly 67 (or under by 1–2 with explicit "I don't need a 6th slot here" reasoning). The trim is intentional, not at-end clean-up.

**What to take from this:** budget the heavy hitters first (30–36 slots), then survival (12–18), then mules (10–15), then cleanup.
Walk the cap as you go.
The rule in [`build-creation.md`](style-guides/build-rules/build-creation.md#p3-track-running-constraints-during-draft) (P3) exists because running over the cap is the most common build failure.

---

## What the v1 build was missing, in one paragraph

Claude's v1 TW/Bio Scrapper drafted each power's slotting in isolation, picked 6-piece set bonuses by default, and treated the build as a writing task with a soft slot budget.
Expert builds invert that: they architect the survival layer (Hardened Carapace as the +Def-unique bucket, Shadow Meld as the click defense spike,
Inexhaustible / Health / Stamina as the global-unique homes) first;
bias the heavy hitters toward 5-piece purples + procs (Whirling Smash + Armageddon + FF, Rend Armor + Hecatomb + AchHee, Arc of Destruction + Superior Critical Strikes);
fill the rest of the picks with LotG-mule defense toggles (Combat Jumping, Maneuvers, Weave, Environmental Modification — capped at 5 LotG `+Rch` instances);
skip Defensive Sweep / Titan Sweep on the final build;
treat 1-slot powers (Evolving Armor, Boxing, Moonbeam) as Hami-O / unique-proc homes;
and walk the 67-slot cap as a binding budget, not a soft guideline.
The slotting falls out of the architecture, not the other way around.

## Reference

- Build samples: [`community/builds/scrapper/brasilgringo-tw-bio.md`](../community/builds/scrapper/brasilgringo-tw-bio.md), [`mythozz-tw-bio.md`](../community/builds/scrapper/mythozz-tw-bio.md),
  [`beowulfinia-tw-bio.md`](../community/builds/scrapper/beowulfinia-tw-bio.md).
- Guide: [`community/archetypes/scrapper/jaegernault-guide-build-tw-bio.md`](../community/archetypes/scrapper/jaegernault-guide-build-tw-bio.md).
- Format/process rules: [`style-guides/build-rules/build-creation.md`](style-guides/build-rules/build-creation.md).
- Procmonster math: [`docs/procs.md`](procs.md), [`docs/proc-strategy.md`](proc-strategy.md).
- Set bonus ceilings: [`strategy/set_bonus_stacking_rules.json`](../strategy/set_bonus_stacking_rules.json), [`docs/min-maxing.md`](min-maxing.md).
