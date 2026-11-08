# Min-Maxing

Min-maxing is the discipline of pushing every build lever to its limit while staying within the hard constraints. It's where the difference between a "decent" build and a "top-tier" build lives.

This is the playbook of levers experienced builders pull. Each section gives the lever, the math, and when to use it.

---

## Lever 1: Soft-cap defense

**Target:** 45% defense (PvE) on the relevant defense types. At 45%, mobs hit you 5% of the time — the floor.

**Why it matters:** Going from 40% to 45% reduces incoming damage by ~50%. Going from 45% to 50% reduces it by ~10%.
The marginal value drops off a cliff above the soft-cap; below it, every percent matters.

**How to budget defense slots:**

- Identify primary defense types (positional or typed — see [build-types-and-goals.md](build-types-and-goals.md)).
- Sum: defense from the set's base + defense IO set bonuses + Maneuvers + Combat Jumping + Weave + Stealth pool + Incarnate Alpha (Agility).
- Pick set bonuses that target the right type. Typed defense bonuses are common in Defense IO sets.

**Common +Defense set bonuses:**

- 5x Reactive Defenses → +1.88% Energy/Negative def, scaling resist proc.
- 5x Red Fortune → +2.5% Ranged def + +5% Ranged def at 6th piece.
- 5x Luck of the Gambler → +9% mez resist + the LotG +Recharge global mule.
- 5x Shield Wall → +5% Resist (All) global.
- 4x Kismet → +6% ToHit (in defense toggles).

---

## Lever 2: Stack to resist cap

**Target:** AT's resist cap on at least Smashing/Lethal. Tanker = 90%; Brute/Scrapper/Stalker/Sentinel = 75%.

**Math:** 60% (set base) + 15% (Tough) + 5% (Shield Wall global) + 10% (set bonuses) = 90% on a Tanker. Brutes/Scrappers stop at 75%.

**Stacking with defense:** Soft-cap defense + resist cap on S/L is the dual-stack target. A mob at 5% to-hit AND 75% resist on the damage they do land = practical immortality vs S/L sources.

---

## Lever 3: Perma-Hasten

**Target:** +167.5% global recharge (sufficient to keep Hasten always-on).

**The math:** Hasten's recharge is 450 sec, duration 120 sec. To make recharge ≤ duration:
`450 / (1 + 0.95 + global) = 120 → global = 1.675`

Where the +167.5% comes from on a typical build:

- 5x LotG +Recharge global = 37.5%
- 5–6x set bonuses at +5% rech = 25–30%
- 2x set bonuses at +6.25% rech (purples) = 12.5%
- 2x set bonuses at +7.5% rech (purples) = 15%
- Incarnate Alpha Spiritual Core Paragon = +33% (post-ED)
- Force Feedback +Recharge proc procs (situational)

Total: ~100–130% from set bonuses + 33% from Alpha = ~135–165%. Tight; often needs Spiritual + 5x LotG + careful set selection.
Worth it: perma-Hasten unlocks every other recharge-gated mechanic (perma-Dom, perma-Build Up, perma-Aim, perma-pet-summon, etc.).

---

## Lever 4: Rule of Five

**Rule:** No more than 5 instances of any single numeric set-bonus value stack.

**Examples:**

- 5x +7.5% Recharge (LotG global) caps. A 6th LotG slotted gives nothing.
- 5x +5% Recharge from set bonuses caps independently of the LotG stack.
- 5x +1.88% S/L defense from a Decimation 2-piece bonus caps.

**Implication:** When stacking a particular bonus, mix sets that give the _same value_ differently.
E.g. five LotG (+7.5% rech) PLUS five Crushing Impact 4-piece (+5% rech) PLUS five Decimation 4-piece (+6.25% rech) all stack with each other (different values).

**Tooling:** Mids-in-app shows when you've hit the cap on a bonus (the bonus turns red). Re-run the build through Mids before trusting it.

---

## Lever 5: Frankenslotting

**Definition:** Using single-aspect IOs from different sets to maximize per-aspect contribution without committing to a full set.

**When to Frankenslot:**

- The power's set bonuses don't help your build goal.
- You want maximum enhancement per aspect (post-ED ~95% per slotted aspect).
- The power is critical and needs every drop of effectiveness.

### Example: Foot Stomp slotting

- 6-set Crushing Impact: +95% damage (post-ED), set bonuses ≈ +7% acc + +5% rech + +3% damage + +2.5% Neg/Tox res.
- Frankenslotted (Acc/Dmg + Dmg/Rech + Acc/Dmg/EndRdx + Acc/Dmg/Rech + Dmg/EndRdx + Knockback/Dmg/Acc): +95% damage + +50%+ acc + +40% rech + +40% endrdx — better at every aspect.
- Trade-off: no set bonuses. If the build doesn't need those bonuses, Frankenslotting wins.

---

## Lever 6: Set bonus mules

**Definition:** Slotting an unwanted/low-impact power purely for its set bonuses.

**Common mules:**

- **Combat Jumping** — 1 slot (LotG +Rech global). Cheap defense.
- **Hover** — 1 slot (LotG +Rech) or 4-slot Blessing of the Zephyr (KB protection, ranged def bonus).
- **Maneuvers** — 5x Reactive Defenses (typed def bonus + scaling resist proc).
- **Stealth** — 4-slot Red Fortune (ranged def + 5-piece bonus).
- **Tough** — 4-slot Unbreakable Guard (resist + max HP global).
- **Weave** — 5x Luck of the Gambler.

A 4-pool budget can comfortably accommodate 4–6 mules across Fighting/Speed/Leadership/Concealment.

---

## Lever 7: Slotting priorities by power role

For each power role, slot the aspects in this rough priority order:

| Role                 | Priority order                                                              |
| -------------------- | --------------------------------------------------------------------------- |
| Single-target attack | Damage > Accuracy > Recharge > EndRdx                                       |
| AoE attack           | Damage > Accuracy > Recharge > EndRdx > Range/Radius (if applicable)        |
| Defense toggle       | Defense > EndRdx                                                            |
| Resist toggle        | Resist > EndRdx                                                             |
| Heal click           | Heal > Recharge > EndRdx                                                    |
| Hold/Stun/Sleep      | Accuracy > Mez Duration > Recharge > EndRdx                                 |
| Travel power         | Run/Fly/Jump > EndRdx (often 1-slot mule for KB protection or stealth proc) |
| Summon               | Recharge > Damage (procs in pets) > Accuracy                                |
| Buff (Self / Team)   | Recharge > Effect (Defense, ToHit, etc.) > EndRdx                           |

**Alpha bypass note:** Cardiac Alpha makes EndRdx less critical (its +33% End red bypasses ED). Spiritual makes Recharge less critical. Plan slot priorities WITH your Alpha choice in mind.

---

## Lever 8: Endurance management

**Common end issues + fixes:**

- **Bursty drain** (toggles + attacks together exceed recovery): Slot Performance Shifter +End proc in Stamina, Theft of Essence proc in heal click, more EndRdx in toggles, Cardiac Alpha.
- **Sustain drain** (recovery < cost over time): Numina/Miracle/Panacea uniques in Health, Ageless Destiny on rotation, more Recovery from set bonuses.
- **Single-power drain spikes** (e.g. nuke): Power Sink (Energy Aura), Conserve Power (Energy Manipulation), Energize (Electric Armor) — set up before nuke firing.

**Recovery cap = 500%.** With Stamina + Cardiac + sets you can hit ~150–200% — well below cap. Cap rarely matters; getting _to_ 100%+ recovery is the practical battle.

---

## Lever 9: Travel powers as utility

Travel powers double as set-bonus mules and slot-investment opportunities:

- **Combat Jumping** — almost always picked. LotG +Rech mule + immobilize protection + tiny defense.
- **Super Jump** — ranged def via Blessing of the Zephyr 4-set + KB protection.
- **Hover** — KB protection, ranged def slotting opportunity, vertical mobility.
- **Super Speed** — Celerity +Stealth proc (in Sprint also) → invisible.
- **Stealth** (Concealment pool) — defense + stealth, but suppresses on attack.
- **Teleport** — situational; stays for unique movement only.
- **Mystic Flight** (Sorcery pool) — flight + utility teleport.

A canonical 4-pool budget on a melee AT: Speed (Hasten + Super Speed), Leaping (Combat Jumping + Super Jump), Fighting (Boxing/Kick + Tough + Weave), Leadership (Maneuvers + Tactics + Assault).

---

## Lever 10: Incarnate priority

The Alpha slot is the headline. Pick it based on your build's biggest gap:

- **Endurance starved** → Cardiac Core Paragon (+33% End red bypass).
- **Defense soft-cap hard to reach** → Agility Core Paragon (+33% Def + Recharge + EndMod).
- **Recharge below 167.5%** → Spiritual Core Paragon (+33% Rech + Heal).
- **Damage build** → Musculature Core Paragon (+33% Damage + EndMod).
- **Resist build** → Resilient Core Paragon (+33% Res + Tox/Psi Res).

Plan slots first; let Alpha fill the gap. Don't slot AROUND Alpha — that locks you into one Alpha forever.

After Alpha:

- **Hybrid** — pick Assault (damage), Melee (resist + def), Support (team), or Control (mez).
- **Destiny** — Barrier (universal defense) or Ageless (recharge/recovery) most common.
- **Lore** — Cimerorans Radial for damage burst.
- **Judgement** — Ion or Pyronic for AoE damage.
- **Interface** — Reactive Radial Flawless for damage proc.

See [docs/incarnates.md](incarnates.md) for full detail.

---

## Putting it together: a level-50 IO build checklist

1. **Pick AT + Primary + Secondary** — set the goal (soft-cap defense, resist cap, perma-Hasten, procmonster, etc.).
2. **Pick Pools (4 max)** — Speed (Hasten), Leaping (CJ), Fighting (Tough/Weave), Leadership (Maneuvers).
3. **Lay out power picks per level** — primary tier-1/2 at 1, secondary tier-1 at 1, fill in by tier and level rule. Tier-9 primary at 32; ancillary at 35.
4. **Slot survivability first** — defense toggles, resist toggles, Tough, Weave, mules.
5. **Slot recharge second** — LotG mules in defense toggles, set 5-pieces with rech bonus, Spiritual Alpha if needed for perma-Hasten.
6. **Slot heavy hitters** — Frankenslot or 6-set, depending on bonus alignment.
7. **Slot procs in long-rech attacks** — Total Focus, Knockout Blow, Assassin Strike, etc. with damage procs.
8. **Slot inherents** — Numina/Miracle/Panacea/Regen Tissue in Health; Performance Shifter +End in Stamina.
9. **Run through Mids** — verify ED, Rule of Five, slot count, level rules. Turn red bonuses → fix.
10. **Pick Incarnates** — Alpha first; Hybrid + Destiny based on role.
11. **Pylon test** (optional) — solo a Rikti Pylon, time the kill, compare to expected DPS.

---

## What to skip

These are common traps that look optimal but aren't:

- **Slotting damage in autos.** Auto powers (e.g. resistance passives) don't damage. Slot for the actual effect.
- **6-piece sets in low-impact powers.** Five LotG mules give 5x +Rech globals; a 6-piece in Combat Jumping locks 5 slots for one set bonus. Mules win on slot economy.
- **Stacking +5% Recharge bonuses past 5.** Rule of Five caps it.
- **Frankenslotting a power that has a great set.** If 5x Crushing Impact gives bonuses you want, take the bonuses.
- **Heavy procs in auto/toggle.** Procs work on activation; toggles tick (some do proc, but rates vary).
- **Choosing Alpha before knowing the build.** Alpha is Lever 10 because it should fill a gap, not be your foundation.
