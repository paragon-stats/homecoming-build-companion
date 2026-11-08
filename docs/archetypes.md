# Archetypes

An **archetype (AT)** is the character's class.
It determines: what powerset categories you can pick from, what your inherent power is, what your base hit points and recovery are, and the per-AT caps for resistance, damage, regen, recovery, and recharge.

Authoritative data: [data/mids/database/Archetypes.json](../data/mids/database/Archetypes.json) and [data/canonical/archetypes.json](../data/canonical/archetypes.json).

---

## Playable archetypes (Homecoming)

Two-row split mirrors the classic hero/villain origin but Going Rogue made all 15 cross-faction.

**Hero-side ATs:**

- **Tanker** — Defense primary, Melee secondary. Highest resist cap (90% PvE). Inherent: **Gauntlet** (melee attacks taunt).
- **Scrapper** — Melee primary, Defense secondary. High single-target damage. Inherent: **Critical Hit** (chance for double damage).
- **Defender** — Buff/Debuff primary, Ranged secondary. Highest debuff/buff multipliers in the game. Inherent: **Vigilance** (lower endurance cost when teammates are hurt).
- **Controller** — Control primary, Buff/Debuff secondary. Hard control + team support. Inherent: **Containment** (double damage to mezzed targets).
- **Blaster** — Ranged primary, Manipulation secondary. Highest player damage cap. Inherent: **Defiance** (damage scales as health drops; mez-immune attacks).

**Villain-side ATs:**

- **Brute** — Melee primary, Defense secondary. Builds Fury for damage. Inherent: **Fury** (damage scales with attack/take damage activity).
- **Stalker** — Melee primary (Stealth-based), Defense secondary. Invisible + assassinations. Inherent: **Hide / Assassin's Focus** (hidden bonus on first attack; AS crits).
- **Corruptor** — Ranged primary, Buff/Debuff secondary. Damage + support hybrid. Inherent: **Scourge** (damage scales up against low-HP targets).
- **Dominator** — Control primary, Assault secondary. Strong control + ranged damage. Inherent: **Domination** (builds toward perma-Dom: lockdown + recovery).
- **Mastermind** — Pet primary, Buff/Debuff secondary. Commands henchmen. Inherent: **Supremacy** (pets buffed when near you).

**Epic ATs (level 50 unlock historically; on Homecoming available from creation):**

- **Peacebringer** (Hero, Kheldian) — Light/energy form-shifter. Self-sufficient.
- **Warshade** (Hero, Kheldian) — Dark/quantum form-shifter. Drain-focused.
- **Arachnos Soldier** (Villain, Wolf Spider/Bane Spider/Crab Spider branches) — Ranged or melee build paths.
- **Arachnos Widow** (Villain, Fortunata/Night Widow branches) — Psionic ranged or stealth melee.
- **Sentinel** — Ranged primary, Defense secondary. Survivable ranged AT (one of the newer designs).

---

## Per-AT caps and base stats

These are the hard ceilings on what you can buff to. Pulled from `Archetype.cs` defaults; per-AT actual values live in `Archetypes.json`.

| Field           | Default                          | What it means                                      |
| --------------- | -------------------------------- | -------------------------------------------------- |
| `Hitpoints`     | varies by AT                     | Base HP at level 1 (scales with level via NLevels) |
| `HPCap`         | 5000                             | Maximum HP (pre-buffs from sets/Accolades)         |
| `ResCap`        | 90% (Tanker) / 75% (most others) | Max damage resistance                              |
| `DamageCap`     | 400% (varies)                    | Max damage buff (Blaster: 500%)                    |
| `RechargeCap`   | 500%                             | Max recharge buff (incl. Hasten + sets)            |
| `RegenCap`      | 2000%                            | Max regen rate (HP/sec)                            |
| `RecoveryCap`   | 500%                             | Max endurance recovery rate                        |
| `PerceptionCap` | 1153 ft                          | Max stealth-detection range                        |
| `BaseRecovery`  | 1.67 end/sec                     | Endurance regen baseline (with Stamina)            |
| `BaseRegen`     | 1 HP/sec                         | Health regen baseline (with Health)                |
| `BaseThreat`    | 1.0 (Tanker > 1, others = 1)     | Aggro magnet multiplier                            |

---

## Powerset categories per AT

Each AT exposes four categories:

- **Primary** — your defining set (Defense for Tankers, Melee for Scrappers, etc.).
- **Secondary** — complementary set (Melee for Tankers, Defense for Scrappers).
- **Ancillary / Patron Pool** — unlocked at level 35; AT-specific endgame powerset.
- **Pool** — universal pools (Fighting, Speed, Leaping, Leadership, Concealment, Medicine, Presence, Teleportation, Flight, Sorcery, Force of Will, Experimentation, Gadgetry, Utility Belt).
  Pool slots are limited to 4 chosen pools per build (each with up to 4 powers).

The exact list of available powersets per AT lives in `Archetypes.json` as `Primary[]`, `Secondary[]`, `Ancillary[]` arrays of powerset indices.
Resolve them through `PowerSets.json` to get the named sets.

---

## Reading the data

Per AT in `Archetypes.json`:

```json
{
  "ClassName": "Class_Tanker",
  "DisplayName": "Tanker",
  "ClassType": "Hero",
  "Hitpoints": 1874.1,
  "HPCap": 3534.0,
  "ResCap": 90.0,
  "DamageCap": 400.0,
  "RechargeCap": 500.0,
  "BaseThreat": 4.0,
  "Primary": [
    /* powerset indices */
  ],
  "Secondary": [
    /* powerset indices */
  ],
  "Ancillary": [
    /* powerset indices */
  ],
  "Origin": ["Magic", "Mutation", "Natural", "Science", "Technology"]
}
```

Tankers have higher `BaseThreat` (4.0+) so enemies attack them preferentially. Brutes have moderate threat (~3.0). Scrappers/Stalkers are ~1.0.
This affects taunt/aggro mechanics; Claude should mention threat when discussing tank-vs-non-tank survivability.

---

## Common AT-specific notes for build planning

- **Tankers** care about **resistance soft-cap** (75% to 90% S/L for most builds) and **defense soft-cap** (45% to typed defenses). Many builds aim for both.
- **Brutes** care about **resistance** (75% cap) + **Fury maintenance** (need fast attack chain). Often the highest practical DPS at endgame because Fury caps high.
- **Scrappers** care about **defense** (45% soft-cap) + **damage** (high crit rate against bosses).
- **Stalkers** care about **stealth + Assassin's Focus stacks**. Best burst damage; weakest sustained DPS in long fights.
- **Sentinels** care about **defense + ranged single-target DPS**. Inherent crit on ranged powers.
- **Blasters** care about **damage + sustained survivability** (Defiance keeps them attacking through mez). Endurance often the bottleneck.
- **Corruptors** care about **debuffs (-resist procs are signature) + Scourge** vs low-HP targets.
- **Defenders** care about **buff/debuff multipliers** (highest in game) — they make teams.
- **Controllers** care about **AoE control + Containment damage doubling** + secondary buffs.
- **Dominators** care about **Permadom** (need ~165% global recharge to keep Domination always-on).
- **Masterminds** care about **pet buffs + pet positioning + Supremacy aura**. Pet survivability is the build challenge.
