# Proc Strategy

The deeper how-to of proc-loaded slotting, beyond the mechanics in [procs.md](procs.md).

---

## When procs beat enhanced damage

A long-recharge single-target attack loaded with 4–5 damage procs typically out-DPSes the same attack slotted with a traditional damage IO set. The math:

```text
proc_damage_avg = sum(per_proc_chance × per_proc_damage)
```

For a 12-second-recharge attack with four 3.5 PPM procs at 71 base damage each, at perma-Hasten (~+167% global rech), effective recharge ≈ 4.5 sec. PPM math gives ~26% chance per proc per activation.

- 4 × 0.26 × 71 ≈ 74 average proc damage **per fire**.
- Compared to a 6-piece Crushing Impact at +95% damage post-ED on a base 89 dmg attack: 89 × 1.95 = 174 enhanced damage per fire.

Looks like Crushing Impact wins (174 > 74)? Not so fast — the procs **add to** the base damage. A proc-loaded attack does:

- Base damage: 89 (no enhancement)
- Plus 4 procs: 74 average
- Plus Build Up / Fury / Containment / etc.: situational

…vs. enhanced attack: 174 enhanced + Build Up/etc. The enhanced attack wins on raw damage, **but** the proc attack wins when:

1. The base damage is already high enough that you don't need enhancement (purple-tier attacks).
2. The build is hitting the **damage cap** from buffs (Fury, Build Up, Aim, Power Boost, Hybrid Assault, Musculature) — at the cap, enhancement does nothing extra.
3. You can fit **6 procs** instead of 4 — at perma-Hasten, that's 6 × ~26% × 71 ≈ 110 average added damage, enough to compete with enhancement.
4. You're slotting a power that already has +Recharge from set bonuses — slotting Crushing Impact for the rech bonus doesn't help if you can get rech elsewhere.

**Bottom line:** Procs win on pure damage when base damage is high, the build is capped on buffs, or set bonuses aren't needed.
Enhancement wins when the build needs the set bonuses or has spare damage cap.

---

## The "procmonster" build philosophy

A build that maximizes procs by accepting trade-offs:

- **Less +Recharge.** Procs lose chance per activation as global +Recharge rises (it shortens effective recharge → smaller PPM window).
  Procmonster builds typically run with +100% global rech, NOT +167% perma-Hasten.
- **Heavy hitters loaded with damage procs.** AS (Stalker), Knockout Blow (Brute), Total Focus (Energy Melee), Lance from Fortunata, Sniper Blast, etc.
- **Secondary hits via procs.** Damage procs are secondary effects — they fire alongside the regular attack damage; they don't replace it.
- **Less set-bonus stacking.** The build skips full-set commitments and Frankenslots procs in.
- **Survival via Incarnates.** Hybrid Melee Core Embodiment + Destiny Barrier carries the survivability budget that set bonuses normally would.

### Canonical procmonster examples

**Stalker AS-cycle procmonster:**

- 5–6 damage procs in Assassin Strike (e.g. Hecatomb proc, Apocalypse proc, Mako's Bite proc, Touch of Death proc, Crushing Impact proc, Decimation proc).
- Build Up + AS critical → ~600+ damage on a single hit.
- Build for ~+100% global rech (not +167% — preserves proc chance).

**Sentinel snipe procmonster:**

- Snipe (Defender's Bastion proc, Apocalypse proc, Devastation proc, Sting of the Manticore proc, Sniper sets proc).
- Aim + snipe → 400+ damage spike.
- Best on ranged-heavy ATs (Sentinel, Blaster, Corruptor) where snipes have native long recharge.

**Fortunata Lance procmonster:**

- Total Domination → Subdue → Lance with damage procs → AV-shredding burst.

---

## Proc-by-proc usage notes

### Damage procs (Schedule A baseline)

- **Apocalypse: Chance for Negative Energy Damage** — purple, 3.5 PPM, ~107 dmg. Slot in the highest-base-damage attack you have.
- **Hecatomb: Chance for Negative Energy Damage** — purple, melee version of Apocalypse.
- **Ragnarok: Chance for Knockdown** — *not* damage; +KD Mag. Useful in AoE.
- **Armageddon: Chance for Fire Damage** — purple, melee AoE, ~107 fire dmg.
- **Crushing Impact: Chance for Smashing Damage** — non-purple, 3.5 PPM, ~71 dmg. Cheaper than purples.
- **Mako's Bite: Chance for Lethal Damage** — non-purple. ~71 lethal dmg.
- **Touch of Death: Chance for Negative Energy Damage** — non-purple. ~71 neg dmg.
- **Decimation: Chance for Build Up** — fires +100% damage buff for 5 sec. NOT a damage proc itself; effectively procs Build Up.

### Special-effect procs

- **Force Feedback: Chance for +Recharge** — 4.5 PPM, slotted in any KB-capable or fast-recharge power → +100% recharge for 5 sec. Stack the proc on multiple attacks for near-perma uptime.
  Game-changer.
- **Achilles' Heel: Chance for Resist Debuff** — 3 PPM, single-target -20% Resist for 10 sec. Massive DPS amplifier; one attack carrying this debuffs the target for the whole rotation.
- **Touch of Lady Grey: Chance for Negative Energy Damage** — debuff set proc that does damage.
- **Cloud Senses: Chance for Negative Energy Damage** — slottable in to-hit debuff powers.
- **Glimpse of the Abyss: Chance for Psionic Damage** — fear set damage proc.

### Defense / sustain procs

- **Reactive Defenses: Scaling Damage Resistance** — slottable in any defense set. Grants up to ~9% Resist that scales as your HP drops. Save-my-ass insurance; slot in your most-used defense toggle.
- **Steadfast Protection: Knockback Protection** — −4 Mag KB protection.
- **Steadfast Protection: Resistance/+Defense** — +3% Defense (All) when slotted. Unique.
- **Shield Wall: +Resist (All)** — +5% Resist (All) global. Unique.
- **Gladiator's Armor: Teleport Protection** — TP protection. Niche (PvP / specific TFs).

### Endurance / sustain procs

- **Performance Shifter: Chance for +Endurance** — slot in Stamina. ~10% chance per tick to refund 10 end. Effectively +20% recovery. Unique.
- **Power Transfer: Chance for Self Heal** — slot in any toggle/auto/click. ~10% per activation to heal a small amount. Unique.
- **Numina's Convalescence: +Regeneration/+Recovery** — slot in Health. +12% Regen + +12% Recovery passive. Unique.
- **Miracle: +Recovery** — slot in Health. +15% Recovery passive. Unique.
- **Panacea: +Hit Points/Endurance** — slot in Health. +12% HP + +12% End/Recovery. Unique.
- **Regenerative Tissue: +Regeneration** — slot in Health. +25% Regen passive. Unique.
- **Theft of Essence: Chance for +Endurance** — slot in heal click. ~30% chance to refund 22 end on heal activation.

### Mez / control procs

- **Lockdown: Chance for +2 Mag Hold** — slot in any Hold power. Stacks Mag with the base hold (most ATs hold at Mag 3; Lockdown stacks +2 → bosses lock at 5).
- **Coercive Persuasion: Contagious Confusion** — slot in Confuse. ~10% chance to spread confuse to nearby targets.
- **Ghost Widow's Embrace: Chance for Psionic Damage** — slot in Hold. Damage proc on a control power.

### Travel / utility procs

- **Celerity: +Stealth** — slot in Sprint. ~30% chance to grant Stealth (full invis) when activated. Effectively permanent stealth out of combat.
- **Blessing of the Zephyr: Knockback Protection** — slot in any travel power. −4 Mag KB protection.
- **Winter's Gift: Slow Resistance** — slot in any travel power. +20% Slow Resistance.

---

## Slotting patterns by power archetype

### Long-recharge single-target heavy hitter (Total Focus, Knockout Blow, Assassin Strike, Sniper Blast, Lance)

Procmonster path:

- Apocalypse: Damage (purple, ~107 dmg per chance)
- Apocalypse: Chance for Negative (purple proc)
- Hecatomb: Chance for Negative (purple proc, melee version)
- Crushing Impact: Chance for Smashing
- Mako's Bite: Chance for Lethal (or Touch of Death)
- Achilles' Heel: Chance for Resist Debuff (if S/L attack)

= 5–6 procs + base damage + Build Up etc. = 400+ avg damage per fire.

### Fast single-target attack (T1 punch, jab, etc.)

Don't procmonster. Slot for set bonuses or Frankenslot:

- 6x Crushing Impact (full bonuses) — universal melee.
- Or Frankenslotted Acc/Dmg, Dmg/Rech, Acc/Dmg/EndRdx, Acc/Dmg/Rech, Dmg/EndRdx.

### AoE attack (Foot Stomp, Lightning Rod, Spines auto, nukes)

Mixed. Some procs work, but area_factor reduces chance:

- 4-piece Crushing Impact (or Targeted AoE set) for set bonuses.
- 1x Force Feedback +Recharge proc (in any KB AoE — game-changer).
- 1x Achilles' Heel proc (single-target portion of cone hits hard).

### Defense toggle

- 1-slot LotG +Recharge global (most efficient).
- Or 5x Reactive Defenses (typed def + scaling resist proc + 5-piece bonuses).
- Don't slot procs in toggles; toggle ticks ≠ activation procs.

### Heal click (Healing Flames, Aid Self, etc.)

- 5x Doctored Wounds (recharge bonus + heal/recharge IOs).
- Or 5x Numina's Convalescence (regen + +recovery global) — stack Numina + Miracle + Panacea + Regen Tissue + LotG mules in Health for the famous "4-IO Health build."
- Theft of Essence: Chance for +Endurance proc (slot in heal click for endurance refund).

### Hold / Stun / Sleep

- 5–6x Basilisk's Gaze (Hold sets) for recharge bonus + IO mez/recharge.
  - Lockdown: Chance for +2 Mag Hold proc (essential for boss-Mag holds).
  - Ghost Widow's Embrace: Chance for Psionic Damage (proc damage in hold).

### Travel power

- 1x Blessing of the Zephyr: KB Protection (KB immune).
- 1x LotG +Recharge global (in CJ, Hover, Maneuvers).
- 1x Celerity: +Stealth (in Sprint or Stealth).
- 1x Winter's Gift: Slow Resistance (in any travel — slot in Sprint to free other slots).

---

## Anti-patterns to avoid

- **Stacking Force Feedback +Rech in 1-second-recharge attacks.** Capped chance, doesn't help recharge much; better in heavy hitters.
- **Damage procs in fast AoEs.** Area_factor + low recharge = low chance per target; usually not worth the slot.
- **Procmonster + perma-Hasten in the same build.** Pick one; they fight each other.
- **Multiple "unique" procs of the same name.**
  Each unique can only be slotted ONCE per build (Numina, Miracle, Panacea, Reg Tissue, Performance Shifter, Steadfast +Def, Shield Wall +Res, Gladiator's TP).
- **Forgetting Accuracy** — procs need the parent attack to *hit* before they get a chance to fire. Slot Acc somewhere or rely on Tactics / Kismet / Build Up.
