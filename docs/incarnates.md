# Incarnate Powers

The Incarnate system is the level-50+ endgame progression. Once your character hits 50, you unlock a parallel "Incarnate level" track with its own slots, abilities, and content.

Authoritative data: derived from `Powers.json` records where `InherentType == "Incarnate"`, gated by `ServerData.EnabledIncarnates` (Homecoming-specific).

---

## Incarnate slots (Homecoming roster)

There are 10 named slots in the system, but **Homecoming only enables 6 of them**:

| Slot          | Enabled on Homecoming | Purpose                                                                                                                                |
| ------------- | :-------------------: | -------------------------------------------------------------------------------------------------------------------------------------- |
| **Alpha**     |          YES          | Global stat boost (recharge, damage, accuracy, end, etc.) — and bypasses ED on enhanced aspects. The most build-defining slot.         |
| **Judgement** |          YES          | Massive AoE nuke, every ~90 seconds. The "delete a spawn" button.                                                                      |
| **Lore**      |          YES          | Summons two large temporary pets. The emergency-DPS button.                                                                            |
| **Destiny**   |          YES          | Group-targeted click buff (Barrier, Ageless, Clarion, Rebirth). The team-saver button.                                                 |
| **Hybrid**    |          YES          | Toggle persona giving major buff (Assault for damage, Melee for resist, Support for buffs, Control for mez). The "constant edge" buff. |
| **Interface** |          YES          | Damage proc on every attack you fire (Reactive, Degenerative, Diamagnetic, etc.). The DoT/debuff layer.                                |
| **Genesis**   |          NO           | (Disabled on HC.)                                                                                                                      |
| **Omega**     |          NO           | (Disabled on HC.)                                                                                                                      |
| **Stance**    |          NO           | (Disabled on HC.)                                                                                                                      |
| **Vitae**     |          NO           | (Disabled on HC.)                                                                                                                      |

When recommending Incarnate choices, only use the 6 enabled slots. Confirm against `data/mids/derived/server_constants.json::EnabledIncarnates`.

---

## Tier progression

Each slot has 4 tiers:

- **T1 — Common** (no level shift, modest buff)
- **T2 — Uncommon** (small bonus over T1)
- **T3 — Rare** (substantial, **+1 level shift in iTrials**)
- **T4 — Very Rare** (best, **+1 additional level shift**, total +2 against +4 incarnate content)

Crafting flows: T1/T2 from common Empyrean salvage, T3 from rare salvage + Threads/Empyrean, T4 from very-rare salvage + significant resources.
Most builds aim for **T4 in every slot they care about**.

---

## Slot-by-slot recommendations

### Alpha — pick one based on your build's biggest gap

- **Cardiac (T4 Cardiac Core Paragon)** — +33% End reduction (subject to ED), +20% range, +20% defense debuff resist. **Endurance-starved or short-ranged builds.**
- **Musculature (T4 Musculature Core Paragon)** — +33% Damage, +33% End mod, +20% defense debuff resist. **Damage-focused builds.**
- **Spiritual (T4 Spiritual Core Paragon)** — +33% Recharge, +33% Heal, +20% Stun. **Builds that need more recharge or heal.**
- **Agility (T4 Agility Core Paragon)** — +33% Defense, +33% End mod, +33% Recharge. **Defense-stacking builds.** Often the default for defense softcap builds.
- **Resilient (T4 Resilient Core Paragon)** — +33% Resist, +33% Tox/Pis Resist, +33% Stun. **Resist-stacking builds.**
- **Vigor (T4 Vigor Core Paragon)** — +33% Heal, +33% End red, +20% Confuse. **Healers and end-starved heal-heavy builds.**
- **Intuition (T4 Intuition Radial Paragon)** — +33% ToHit Debuff, +33% Defense Debuff, +33% Hold, +20% Range, +33% Slow. **Debuff-focused defenders/corruptors.**

**Rule of thumb**: Cardiac for end issues, Spiritual for recharge, Agility for defense softcap, Musculature for damage, Resilient for resist softcap.

### Judgement — pick on theme + secondary effect

- **Ion** — Energy damage, AoE chain. Best raw damage.
- **Pyronic** — Fire damage, AoE. Similar to Ion.
- **Mighty** — Smashing damage, knockdown. Good with Force Feedback +Rech proc.
- **Void** — Negative damage, −Damage debuff. Defensive utility.
- **Vorpal** — Defense effect (Cone Negative Damage with +Defense buff to caster). Ice-themed.
- **Cryonic** — Cold damage, slows. AoE control flavor.

Judgement is build-flexible — pick the damage type your build doesn't already cover.

### Lore — pet pairs (two pets, one big and one supporting)

- **Cimerorans** — high single-target DPS via Romulus + Surgeon support. Often considered top-tier.
- **IDF** — Praetorian-themed; balanced.
- **Longbow** — buff-heavy support pets.
- **Carnival of Light/Shadow** — ranged + buff/debuff.
- **Polar Lights** — pet-focused, ranged.
- **Demolitionists / Vanguard / Storm Elementals / etc.** — many flavors; some niche-better than others.

Cimerorans Radial (DPS-focused) is the conventional best for damage. For survivability, Longbow Radial brings healing.

### Destiny — group click buffs (every ~120 sec)

- **Barrier (Core Epiphany)** — +Defense + +Resist to all teammates, decaying over 2 minutes. Universal solver.
- **Clarion (Core Epiphany)** — Mez Protection to all teammates. Critical for non-armored ATs (Defenders, Controllers, Blasters, Corruptors).
- **Ageless (Core Epiphany)** — +Recovery + +Recharge to all teammates. Solves end issues + more recharge.
- **Rebirth (Core Epiphany)** — +Heal Over Time + +Regen burst. Team save button.
- **Incandescence (Radial Epiphany)** — Teleport teammates to caster + brief buff. Niche utility.

For solo or duo: Barrier is the safest pick. For a Defender/Controller team support: Clarion is huge.

### Hybrid — toggle persona

- **Assault (Core Embodiment)** — +25% Damage, +Mez resist when active. **Damage builds.**
- **Melee (Core Embodiment)** — +Resist all, +Defense AoE/Melee when active. **Tank/Brute survival.**
- **Support (Core Embodiment)** — Healing AoE pulse, +Recovery. **Support ATs and team play.**
- **Control (Core Embodiment)** — +Damage to held/stunned, +Mez Mag. **Controllers, Dominators.**

Toggle once activated for ~120 seconds, ~120 sec recharge. Track uptime. Most builds default to Assault Core Embodiment.

### Interface — proc per attack

- **Reactive (Radial Flawless)** — Fire DoT proc + −Resistance proc. Universal damage amplifier.
- **Degenerative (Radial Flawless)** — Tox damage proc + −HP proc. Strong vs hard targets.
- **Diamagnetic (Radial Flawless)** — −ToHit + −Regen proc. Tank/debuff layer.
- **Preemptive (Core Flawless)** — Energy damage + −Recovery. End-drain themed.
- **Spectral (Core Flawless)** — Negative damage + Confuse. Mez utility.

For pure damage, Reactive Radial is the default. For specialty roles, the others have niches.

---

## Unlock progression

- **Level 50** — Mender Ramiel arc unlocks the Alpha slot.
- **Dark Astoria storyline** — solo path to incarnate progression; unlocks via Mender Ramiel and the Astoria contacts. Drops Threads.
- **iTrials** — Lambda, BAF, Underground, Keyes, TPN, MoM, Magisterium, etc. Drop Empyrean Merits (universal incarnate currency) and rare/very-rare components. The fastest path past T1.
- **Incarnate XP** — earned from level-50+ content (incarnate trials, ouroboros incarnate missions, Pylon attacks). Slot abilities unlock on a parallel XP track.

---

## Build planning notes

- **Alpha is the headline.** Picking the right Alpha can enable the rest of your build (Cardiac fixes endurance, Spiritual hits recharge breakpoints, Agility softcaps defense). Decide Alpha first.
- **Hybrid + Destiny on a 120s rotation** — both have ~120s recharge. Build a routine: pop Destiny on a tough fight, run Hybrid for the duration, re-pop both as they refresh.
- **Judgement + Lore + Hybrid** — three "burst" buttons that synergize on hard fights. Most teams call them out for boss spawns.
- **Don't over-rely on Incarnate buffs in slotting.**
  The build should be functional without them.
  Incarnates push it from "good" to "endgame-strong."
  Plan slots to soft-cap **before** Alpha; Alpha should push past softcap, not be the reason you reach it.
