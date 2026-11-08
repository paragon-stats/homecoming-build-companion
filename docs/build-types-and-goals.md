# Build Types and Goals

Every build optimizes for *something*. Knowing which something is the first decision — it shapes every slotting choice that follows.
This is a taxonomy of the canonical build archetypes plus heuristics for picking among them.

> **Universal house rule (applies on top of every build type below).**
> Every build must remain *playable* when exemplared as low as level 10 — character can attack, has a defensive layer running, has a travel power.
> **Level-50 endgame quality is the primary objective; never trade level-50 build quality for exemplar performance.** Mostly a pick-ordering check (matches normal leveling anyway).
> See [slotting-and-leveling.md § Exemplar mechanics and the universal exemplar-10 rule](slotting-and-leveling.md#exemplar-mechanics-and-the-universal-exemplar-10-rule).

---

## The major build types

### Soft-cap defense

**Goal:** Reach 45% defense to the relevant defense types (PvE) before any incoming buffs. At soft-cap, mobs hit you 5% of the time (the floor).

**Defense types** (pick which to soft-cap based on the AT's primary defense set):

- **Positional**: Melee, Ranged, AoE — what most "positional defense" sets give (Super Reflexes, Ninjitsu, Shield Defense, etc.).
- **Typed**: Smashing/Lethal, Fire/Cold, Energy/Negative, Psionic, Toxic — what most "typed defense" sets give (Invulnerability, Energy Aura, Cold Domination, Ice Armor, etc.).

**Where the +Defense comes from:**

- The defense primary/secondary itself (typically 15–25% on relevant types after slotting).
- Set bonuses (LotG +Recharge IOs typically slot in defense toggles; the defense-set IOs themselves give +Defense set bonuses).
- Maneuvers (Leadership pool, +3.5% Defense to All).
- Combat Jumping (small defense).
- Weave (Fighting pool, +3.5% Defense to All).
- Incarnate Alpha **Agility** (+33% Defense bypass to ED).
- Incarnate Destiny **Barrier** (decaying +5%/+10% Def + Resist for 2 minutes).

**PvP soft-cap is 30%**, not 45%, because PvP has different ToHit math.

### Resist cap

**Goal:** Reach the AT's resist cap (75% for most ATs, 90% for Tanker) on the relevant damage types — typically Smashing/Lethal first, then Fire/Cold, then Energy/Negative.

**Where the +Resistance comes from:**

- The resist primary/secondary (typically 30–60%).
- Tough (Fighting pool, +Resist S/L).
- Set bonuses (Reactive Defenses scaling resist proc, Unbreakable Guard global, etc.).
- Resistance-typed sets in resistable powers.
- Incarnate Hybrid **Melee** (+Resist all when active).
- Incarnate Alpha **Resilient** (+33% Resist bypass to ED).

For Tankers specifically, **90% S/L resist** is the comfort target. Brutes/Scrappers/Stalkers cap at 75%, so 75% S/L + 45% S/L defense is the dual-stack target.

### Perma-Hasten / +Recharge

**Goal:** Hasten (+70% recharge for 120 seconds, 450 sec base recharge) is up at all times. Threshold: **+167.5% global recharge** (Hasten's recharge then equals its duration).

**Where the +Recharge comes from:**

- Hasten itself (+70% when active).
- Set bonuses: LotG +7.5% Recharge global × 5 = +37.5% recharge.
- Set 5-piece bonuses (+5% rech is common; +6.25% rech on some).
- Incarnate Alpha **Spiritual** (+33% Recharge bypass to ED).
- Incarnate Destiny **Ageless** (+30% Recharge for 60 sec, every 120 sec).

Builds that aim for perma-Hasten rotate around 5x LotG mules + 5–6 piece sets that grant +5% to +6.25% rech. Achievable on most ATs by level 50 + Alpha.

### Procmonster

**Goal:** Maximize per-attack damage by loading long-recharge attacks with damage procs. See [proc-strategy.md](proc-strategy.md).

**Trade-offs:**

- Less +Recharge bonus stacking (more recharge = lower proc chance).
- Less raw damage enhancement; more chance-on-fire damage spikes.
- Variable DPS — averages high but standard deviation is large.
- Best fit: Stalkers (long-recharge AS), Sentinels (snipes), some Brutes / Scrappers, Defenders with proc-friendly attacks.

### Farmer

**Goal:** AE (Mission Architect) farm efficiency — tear through fire-themed mobs at +4 difficulty for influence/XP.

**Required stats:**

- 75% S/L resist (capped).
- 45% Fire/Cold defense (soft-capped; many farms are Fire damage).
- 90%+ HP cap reached via set bonuses + Accolades.
- AoE attack chain — Foot Stomp, Spines, Burn, Fiery Embrace, etc.
- Self-sustain via Healing Flames, Dark Regeneration, Rebirth Destiny, Hybrid Melee.

**Canonical farmer ATs:**

- **Spines/Fiery Aura Brute** — the meta farmer for years.
- **Rad/Fiery Aura Brute** — newer alternative; Atom Smasher AoE.
- **Spines/Fiery Aura Scrapper** — same shtick, less Fury.
- **Bio/Spines Tanker** — defensive variant.

### PvP

**Goal:** Beat other players in zone PvP or Arena.

**Different rules from PvE:**

- Defense soft-cap is **30%**, not 45%.
- Mez (hold, stun, sleep) values are reduced by ~50% on player targets.
- Heals are reduced by ~50% on self in some PvP zones.
- Travel suppression: damaging an enemy stops your travel power for ~12 seconds.
- Enhancement schedules differ (some IOs reduced).
- Diminishing returns on stats stack faster.

PvP builds optimize differently — high burst damage, +Recharge, range, and KB/mez stacking matter more than soft-cap defense. **Specialist domain**; defer to PvP-specific community guides.

### Theme / RP build

**Goal:** Aesthetic / character-concept first; mechanical efficiency second.

**Trade-offs the theme accepts:**

- Suboptimal powerset combo (e.g. "fire/fire/fire Tanker" because that's the character).
- Suboptimal pool picks (took Sorcery for the visual, not Leadership).
- Lower DPS or survivability ceiling.

**What still applies:**

- ED, Rule of Five, slot caps.
- Soft-cap math when you do reach for it.

A theme build can still be *good* — just not maximally optimized. Don't apologize for them; help the player make their character work within the constraints they've chosen.

### Solo-99-difficulty build

**Goal:** Solo +4/x8 content (boss-rich, large groups).

**Required stats:**

- Soft-capped defense AND/OR resist cap on incoming damage types.
- Sustain (heal + recovery) sufficient to handle continuous damage.
- AoE damage to clear groups.
- Mez protection / clear.

Often overlaps with farmer build but emphasizes broader damage type coverage and mez protection over pure efficiency.

### Speed-runner / TF build

**Goal:** Beat task forces fast. Recharge + DPS focus.

- Perma-Hasten essential.
- Single-target burst for AVs (signature bosses).
- Travel power optimized (Speed pool, Sprint with stealth proc).
- Often: Spiritual or Agility Alpha; Ageless Destiny; Reactive Interface.

---

## How to pick a build type for the user

Ask in this order:

1. **What AT/powersets are they playing?** Some types fit; some don't.
2. **What content do they want to do?** (Solo low-difficulty / solo high / teams / TFs / iTrials / farms / PvP)
3. **What budget?** (Generic IOs / set IOs / purples / no limits.)

Then map:

- New player solo low-difficulty: **theme + early-IO** is fine; aim for "comfortable" not "soft-cap."
- Endgame solo high difficulty: **soft-cap defense** + **resist stacking** + **perma-Hasten** for most ATs.
- Endgame team play: lean into AT role — defenders on debuff, controllers on control, brutes on AoE damage, tanks on resist + threat.
- Min-maxing: combine soft-cap def + resist cap + perma-Hasten + procmonster on the heavy-hitter attack. The "everything" build.

---

## Decision priority order for endgame builds

A widely-accepted heuristic for level-50 IO builds:

1. **Survival** — soft-cap defense or resist cap on incoming damage.
2. **Recharge** — perma-Hasten as a bridge to perma-everything-on-cooldown.
3. **Damage** — slot for damage after survival is locked.
4. **Utility** — procs, control, mobility, set bonuses.

Don't reach for damage at the cost of survival. A dead build does no DPS.
