# Procs

A **proc** is an enhancement that has a *chance* to fire an effect when the slotting power activates. Distinct from a "buff" enhancement, which always provides its multiplier.
Procs are the highest-leverage slotting choice in modern endgame builds — and the trickiest to optimize.

Authoritative data: filtered subset of [data/mids/database/Enhancement.json](../data/mids/database/Enhancement.json) where `IsProc: true` or any effect has `EffectChance > 0`.

---

## How a proc differs from a buff enhancement

Same slot, different behavior:

- **Buff enhancement** (e.g. "Crushing Impact: Damage") — adds a fixed % to the power's damage every time it fires. Predictable.
- **Proc** (e.g. "Crushing Impact: Chance for Smashing Damage") — has a chance per activation to add a *separate* damage hit.
  The proc's damage is independent of the power's base damage and ignores the power's enhancement values.

The two main flavors of proc:

1. **Flat-chance procs** — `EffectChance` is a hard percentage (e.g. 20%). Older procs work this way.
2. **PPM (Procs Per Minute) procs** — chance is computed at slotting time from the slotting power's recharge, area, and target count. **Almost all modern procs are PPM.**

---

## The PPM formula

For a PPM proc slotted into a power, the per-activation chance is:

```text
chance = PPM × (recharge_after_slotting + cast_time × area_factor) / 60
```

…clamped to `[0.05, 0.90]` (5% floor, 90% ceiling).

Where:

- **`PPM`** — the proc's procs-per-minute rating (printed on the IO; e.g. 2.5 PPM for most damage procs, 3.5 for some, 4.5 PPM for things like Force Feedback +Recharge).
- **`recharge_after_slotting`** — the power's recharge **including** enhancement bonuses and global +recharge. Critical: more global recharge → less proc chance.
- **`cast_time`** — animation time (arcanatime, the rounded-up tick value).
- **`area_factor`** — for AoEs, reduces chance based on radius and target cap. Empirically:
  - Single-target: 1.0
  - Cone (small/large): ~0.65 / ~0.5
  - Sphere AoE 8 ft / 15 ft / 20 ft: ~0.75 / ~0.55 / ~0.42

Reproduce the exact formula MidsReborn uses by reading `MidsReborn/Core/Base/Data_Classes/Power.cs` (search for "PPM" or "ProcsPerMinute"). The numbers above are community-validated approximations.

### Practical implications

- **Long-recharge attacks proc more reliably.** A 20-second-recharge attack with a 3.5 PPM proc procs at ~67% per activation. The same proc in a 5-second attack procs at ~26%.
- **Fewer global +recharge points → more procs per attack.** This is why "procmonster" builds skip +recharge set bonuses and instead lean on long-recharge attacks loaded with damage procs.
- **AoEs lose proc chance per-target.** The area_factor punishes AoEs sharply. A 20 ft sphere proc averages roughly 40% per target.
- **Floor and ceiling matter.** Even a fast attack can't drop below 5%; even a slow attack can't exceed 90%.

---

## Common proc archetypes

### Damage procs

- **Standard damage procs** — most IO sets have a damage proc piece (e.g. "Crushing Impact: Chance for Smashing Damage", "Decimation: Chance for Build Up"). 2.5–3.5 PPM, ~71 base damage at level 50.
- **Purple damage procs** — Apocalypse, Hecatomb, Ragnarok, Armageddon. Higher base damage (~107). Available level 50+.
- **Archetype Origin (ATO) damage procs** — AT-specific, often with secondary effects (Stalker's Assassin's Mark gives +Build Up; Tanker's Might of the Tanker gives +Damage Resistance).
- **Achilles' Heel: Chance for Resist Debuff** — −20% Resistance proc, single-target. Massive DPS amplifier when slotted in a power that hits often.
- **Force Feedback: Chance for +Recharge** — slotted into a knockback or damaging power → 4.5 PPM proc gives +100% recharge for 5 seconds. Essentially a portable Hasten.

### Defense / Resist procs

- **Reactive Defenses: Scaling Damage Resistance** — slotted into a Defense set; grants up to ~9% Resist that *increases* as your HP drops. "Save my ass" insurance.
- **Steadfast Protection: Knockback Protection** — −4 Mag KB protection. Stack a few for KB-immune builds.
- **Shield Wall: +Resist (All)** — +5% Resist (All) global. Unique.

### Endurance / sustain procs

- **Performance Shifter: Chance for +End** — every Stamina tick has a chance to refund 10 endurance. Unique.
- **Power Transfer: Chance for Self Heal** — heals on attack. Slot in toggles or attacks.
- **Panacea: +Hit Points/Endurance** — unique global; slotted in Health.
- **Numina's Convalescence: +Regeneration/+Recovery** — unique global; slotted in Health.
- **Miracle: +Recovery** — unique global; slotted in Health.
- **Theft of Essence: Chance for +Endurance** — slotted in heal click; refunds end on activation.

### Mez / control procs

- **Lockdown: Chance for +2 Mag Hold** — stacks Mag with your hold. Stalker control breakpoint.
- **Coercive Persuasion: Contagious Confusion** — chance to spread confuse to nearby enemies.
- **Ghost Widow's Embrace: Chance for Psionic Damage** — proc damage in holds.

### Debuff procs

- **Touch of the Nictus: Chance for Negative Energy Damage** — works in heal-debuff powers like Sap (Dark Miasma).
- **Achilles' Heel: Chance for Res Debuff** (also under damage section) — single-target -res in defender debuffs is a force multiplier.
- **Cloud Senses: Chance for Negative Energy Damage** — damage proc that fits in to-hit debuff powers.

### Travel / utility procs

- **Celerity: +Stealth** — slotted in Sprint = invisibility outside combat.
- **Blessing of the Zephyr: Knockback Protection** — −4 Mag KB. Slotted in any travel power.
- **Winter's Gift: Slow Resistance** — slotted in any travel power for slow resist.

---

## Build implications

### When procs beat enhanced damage

A long-recharge single-target attack loaded with 4–5 damage procs often **out-damages** the same attack slotted with traditional damage IOs.
E.g. Tanker's Total Focus, Brute's Knockout Blow, Stalker's Assassin Strike.
The math: each proc fires at 60–90% chance for ~71+ damage, completely outside ED, while damage IOs are ED-capped.

Rule of thumb: **slow + single-target = procmonster; fast + AoE = enhance normally.**

### When procs lose

- **Fast attacks** (recharge < 4 sec) — proc chance drops below 30%, low yield per activation.
- **Wide AoEs** (radius > 15 ft, max targets > 8) — area_factor sharply reduces chance per target.
- **Already-damage-capped builds** — procs are damage; if you're already hitting the AT damage cap from buffs, the proc damage adds, but you might prefer set bonuses for survivability.

### Anti-patterns

- Stacking damage procs in a 3-second-recharge AoE — chance per target near floor.
- Slotting Force Feedback +Rech in a power that doesn't have knockback or fast-recharge attacks — wasted slot.
- Six-slotting a power with all procs and no Accuracy — procs don't have accuracy boosts; if you miss, no procs fire.

### "Procmonster" builds

A build philosophy that maximizes proc damage at the cost of set-bonus optimization:

- Skip +recharge bonuses (they hurt PPM math).
- Lean on long-recharge heavy hitters loaded with 4–5 damage procs.
- Accept fewer set-bonus globals; rely on Incarnate buffs for survivability.
- Common ATs: Stalkers (AS + Build Up + crits + procs), Sentinels (snipes), some Brute / Scrapper builds.

See [docs/proc-strategy.md](proc-strategy.md) for worked examples and breakpoints.
