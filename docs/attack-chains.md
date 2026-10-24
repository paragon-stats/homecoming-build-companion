# Attack Chains

An **attack chain** is a recurring sequence of attacks that fires back-to-back without gaps — every attack comes off cooldown by the time you need to fire it again.
Designing one is a math exercise: total recharge per attack must be ≤ the sum of every other attack's animation time.

---

## Core concepts

### Animation time vs. arcanatime

Every power has a `CastTime` (animation time in seconds). City of Heroes uses **server ticks** of 0.132 seconds.
An animation rounds _up_ to the next tick boundary, and the server adds one more tick before the next power can start.
The result is **arcanatime**, named for the player who documented it.

Formula, as MidsReborn implements it (`Power.ArcanaCastTime`, `Power.cs:603`):

```text
arcanatime = (ceil(CastTime / 0.132) + 1) × 0.132
```

A 1.0-second animation has an arcanatime of 1.188 seconds (9 ticks).
A 1.5-second animation has 1.716 seconds (13 ticks), and a 1.6-second animation has 1.848 seconds (14 ticks).

For chain math, **always use arcanatime**, not the displayed CastTime.

### Effective recharge

A power's recharge after enhancement and global +Recharge:

```text
effective_recharge = base_recharge / (1 + enhancement_recharge + global_recharge)
```

- `enhancement_recharge` is the slotted recharge enhancement value (post-ED, typically 0.95 for fully slotted).
- `global_recharge` is set bonuses + Hasten + Incarnate Spiritual + etc.

A power with 20-second base recharge, fully slotted (95% rech), with +200% global recharge:
`20 / (1 + 0.95 + 2.0) = 20 / 3.95 ≈ 5.06 sec`

### Chain feasibility test

For a chain `[A, B, C, ...]` to fire gaplessly, the cycle time per attack must be:

```text
cycle_time(A) = arcanatime(A) + arcanatime(B) + arcanatime(C) + ...
```

…which must equal or exceed `effective_recharge(A)`. Same for B, C, etc.

In practice, the test simplifies to: for each attack, **sum the arcanatimes of the OTHER attacks in the chain ≥ that attack's effective recharge**.

Example, 3-attack chain A → B → C:

- A is gapless iff `arcanatime(B) + arcanatime(C) ≥ effective_recharge(A)`.
- B is gapless iff `arcanatime(A) + arcanatime(C) ≥ effective_recharge(B)`.
- C is gapless iff `arcanatime(A) + arcanatime(B) ≥ effective_recharge(C)`.

If all three hold, the chain works.

### Chain depth and global +recharge requirement

- **2-attack chain** — needs the most +Recharge (each attack has only one other attack to fill its cooldown).
- **3-attack chain** — common sweet spot.
- **4-attack chain** — easier on +Recharge but each attack must be worth firing.

A typical 3-attack chain on a level-50 IO build with perma-Hasten (+167% rech baseline) works for most attacks with base recharge ≤ ~12 seconds.

---

## Why chains matter

DPS = (damage_per_attack) / (cycle_time_per_attack). A gapless chain locks cycle_time at the minimum (sum of arcanatimes). Slower attacks not in chain → wasted DPS.

A 5-second-recharge attack that hits for 200 damage out-DPSes a 2.5-second-recharge attack hitting for 90 damage. But you need the chain math to fire the heavy hitter consistently.

---

## Common attack-chain patterns by AT

### Tanker / Brute / Scrapper / Stalker (Melee primary)

**Stalker AS-cycle (Assassin Strike-centered):**

- AS (long recharge) → Build Up → AS again, with filler attacks (Sneak, Critical Strikes ATO uptime).
- Requires perma-AS via +Recharge stacking.
- Build Up → AS → BU → AS rotation deals enormous burst.

**Brute heavy-hitter cycle:**

- Foot Stomp / Knockout Blow / Total Focus / Energy Punch on rotation.
- Needs ~+150% global recharge + Spiritual Alpha to fit a gapless 3-chain on 12-sec recharge attacks.

**Scrapper crit-friendly:**

- Spin → Eviscerate → Slash → repeat (Claws example).
- Or: Greater Fire Sword → Cremate → Fire Sword (Fiery Melee) — single-target focused.

### Blaster / Sentinel / Corruptor / Defender (Ranged primary)

**Snipe-on-cooldown (post-i24 fast snipes):**

- Snipe with +22% ToHit becomes "fast snipe" (1.32 sec cast). Slot it as your tier-1 attack.
- Snipe → BU → tier-1/tier-2 fillers → snipe.

**Aim → BU → nuke:**

- Aim + Build Up + nuke (Inferno, Blizzard, etc.). Stack damage temporarily.
- After nuke, fall back to single-target chain while the nuke recharges.

**3-attack ranged chain:**

- Tier-1 quick → tier-2 medium → tier-3 heavy → loop.
- Decimation / Thunderstrike / Devastation set bonuses help reach the recharge threshold.

### Dominator (Control + Assault)

**Permadom rotation:**

- Domination button must be available always (need ~+165% global rech).
- Heavy hitter assault attack on cooldown (Fiery Embrace, Power Boost, etc.).
- Hold + Aim + Power Boost → big ST damage spike.

### Mastermind (Pet primary)

- Pets do the DPS; chain math is per-pet (henchmen don't take orders that fast).
- Player attack chain is mostly buff/debuff cycle (fortifying pets, debuffing enemies).
- Personal attacks are filler.

---

## Worked example: Tanker Super Strength chain

A simple 3-attack chain with Punch (T1), Haymaker (T3), Knockout Blow (T8).

| Attack        | Base Rech | CastTime | Arcanatime |
| ------------- | --------- | -------- | ---------- |
| Punch         | 4 sec     | 1.0      | 1.056      |
| Haymaker      | 8 sec     | 1.83     | 1.848      |
| Knockout Blow | 25 sec    | 2.5      | 2.508      |

With 95% slotted recharge:

- Punch effective rech = 4 / 1.95 = 2.05 sec
- Haymaker effective rech = 8 / 1.95 = 4.10 sec
- Knockout Blow effective rech = 25 / 1.95 = 12.82 sec

Test gapless 3-chain (Punch → Haymaker → Knockout Blow → Punch → ...):

- Cycle = 1.056 + 1.848 + 2.508 = 5.41 sec total
- Punch needs ≤ 5.41 sec recharge ✓ (has 2.05)
- Haymaker needs ≤ 5.41 sec recharge ✓ (has 4.10)
- Knockout Blow needs ≤ 5.41 sec recharge ✗ (has 12.82) — **chain breaks here**

To make Knockout Blow fit a 3-chain at this enhancement level, you need:

- Cycle of 12.82 sec → impossible with these three (sum of arcanatimes = 5.41).
- Add global +Recharge: at +167% global (perma-Hasten), KB recharge becomes 25/(1+0.95+1.67) = 6.94 sec. Still doesn't fit.
- At +250% global (perma-Hasten + Spiritual Alpha + 3x LotG + sets), KB rech = 25/4.62 = 5.41 sec. **Now KB fits the 3-chain exactly.**

That's why "build for perma-Hasten + Spiritual Alpha" is the conventional Tanker advice.

Alternative: **drop KB from the chain**, use it on cooldown (every 6.94 sec at perma-Hasten) and run a 2-chain Punch → Haymaker between fires. Slightly lower DPS but lower recharge requirement.

---

## Tools and resources

- **The engine's chain planner** — `python -m coh_engine chain <dump>` picks the best chain a build can afford under its endurance budget and stance. See [`engine/chain-planner.md`](./engine/chain-planner.md).
- **Mids' Reborn in-app DPS calculator** — shows attack-chain DPS in the build view.
- **Sythlin DPS Tool** — bundled with MidsReborn at `%APPDATA%\LoadedCamel\MidsReborn\Sythlin_DPS_Tool.exe`. Standalone DPS analyzer for chain math.
- **City of Data v2.0** — exact recharge / cast / arcanatime values per power.
- **Pylon DPS test** — community standard test: solo a Rikti Pylon (3M HP, regen-heavy), measure time-to-kill, compute DPS. ~300 DPS = strong endgame; 400+ = top-tier.
- See `strategy/attack_chain_examples.json` for canonical chains seeded from forum posts.

---

## Heuristics for chain design

- **More recharge = more options.** Build for +Recharge first, then choose chains.
- **Heavy hitters belong in chains** when you can afford their recharge. Otherwise on cooldown.
- **Build Up / Aim / Power Boost** are not chain participants — they're stacked bursts. Pop them, then chain.
- **AoE attacks** can replace ST in chains during multi-target fights (Foot Stomp instead of Knockout Blow), but check arcanatime — many AoEs animate slower.
- **Procmonster vs. chain DPS** — pick one philosophy per attack. Mid procs are wasted in chain attacks; chains get full damage from set bonuses + enhancement.
