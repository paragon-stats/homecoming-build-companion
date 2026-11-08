# Powers and effects

A **Power** is the unit of "thing your character can do." Every attack, toggle, click buff, passive, summon, and inherent is a Power record.
Every Power has zero or more **Effects** describing what it does to the target (or self).

Authoritative data: [data/canonical/powers.json](../data/canonical/powers.json) (game-truth) and [data/mids/database/Powers.json](../data/mids/database/Powers.json) (Mids interpretation).

---

## Anatomy of a Power record

The fields below are on every Power. Field names use MidsReborn's spelling — canonical Cryptic field names may differ, but the semantics match.

### Identity

- `FullName` — UID, hierarchical, e.g. `Tanker.Invulnerability.Unstoppable`. Use as the primary key for cross-references.
- `DisplayName` — what the player sees ("Unstoppable").
- `PowerName` — short internal name ("Unstoppable").
- `SetName` — powerset short name ("Invulnerability").
- `GroupName` — category ("Tanker_Defense", "Pool", "Inherent", "Incarnate.Alpha", etc.).
- `PowerSetID` / `PowerSetIndex` — index references into `PowerSets.json`.
- `InherentType` — `None` / `Class` / `Inherent` / `Powerset` / `Power` / `Prestige` / `Incarnate` / `Accolade` / `Pet` / `Temp`. Determines where the power shows up in the UI grid.

### When you can pick it

- `Level` — the **earliest** character level you can pick this power. Tier-1 primaries are 1; tier-9 primaries are typically 32; pool tier-1s are 4; ancillary tier-1s are 35.
- `AvailableAtLevel` — same idea, sometimes used for pet/auto-grant.

### Mechanics

- `PowerType` — `Click` / `Auto` / `Toggle` / `Boost` / `GlobalBoost`. Toggles cost endurance per second; clicks fire once.
- `Accuracy` / `AccuracyMult` — base hit chance multiplier. The game applies a 75% base ToHit; the power's accuracy multiplies that.
- `Range` — for ranged attacks, max distance in feet.
- `EffectArea` — `SingleTarget` / `Cone` / `Sphere` / `Location` / `Touch` / `Self`.
- `Radius` — for AoEs, the radius in feet.
- `MaxTargets` — cap on the number of targets an AoE affects.
  **Authoritative source for Homecoming-current values is the patch notes**.
  Specifically [Issue 26 Page 4](https://homecoming.wiki/wiki/Issue_26_Page_4) which raised most Tanker melee AoE caps to 16 and reset others (e.g. Cleave) to 10.
  The canonical zip (`data/canonical/`) currently lags I26P4 — its `max_targets_hit` field reads pre-patch values for Tanker AoEs.
  See [data/diff/SUMMARY.md](../data/diff/SUMMARY.md) for the systemic drift.
  For Brute / Scrapper / Stalker / Sentinel AoEs, the canonical zip is current.
- `Target` — `Self` / `Foe` / `Ally` / `Teammate` / `DeadTeammate` / `Caster` / etc.
- `TargetLoS` — does the power require line of sight?

### Costs

- `EndCost` — endurance per activation (clicks) or per second (toggles).
- `RechargeTime` — base recharge in seconds before any buffs/enhancements.
- `BaseRechargeTime` — pre-Mids-modification recharge.
- `CastTime` / `CastTimeReal` — animation time. **CastTimeReal** is what you actually spend; **CastTime** is the "card" value.
- `InterruptTime` — how long before the cast becomes uninterruptible.
- `ActivatePeriod` — for autos/toggles, how often the effect ticks.

### Slotting filters

- `Slottable` — can it accept enhancement slots at all? (False for most inherents.)
- `Enhancements[]` — list of allowed enhancement *types* (e.g. Damage, Accuracy, Recharge, EndRdx). Determines which TO/DO/SO/IO single-aspect enhancements can go in.
- `BoostsAllowed[]` — alias / extension of `Enhancements[]`.
- `SetTypes[]` — list of allowed **invention set categories** (e.g. Targeted AoE, Melee Damage, Defense Sets). Determines which IO sets can slot. **Critical for slotting decisions.**
- `IgnoreEnh[]` — explicit exclusions: enhancements MidsReborn intentionally rejects despite the type matching.
- `MaxBoosts` — usually 6 (1 inherent + 5 added). Some inherents are 1.

### Effects

- `Effects[]` — array of `IEffect` records. Each describes one observable behavior:
  - `EffectType` — Damage, Heal, Buff (Defense, Resistance, ToHit, Recharge, etc.), Debuff, Mez (Hold, Stun, Sleep, Disorient, Fear, Confuse, Immobilize, Knockback), Grant Power, Resurrect, Summon, etc.
  - `DamageType` (when relevant) — Smashing, Lethal, Fire, Cold, Energy, Negative Energy, Toxic, Psionic.
  - `Magnitude` / `Scale` — strength of the effect.
  - `Duration` — for buffs/debuffs/mez.
  - `EffectChance` — probability of firing (procs use this).
  - `ProcsPerMinute` — for PPM-based procs.

A power often has multiple effects: a damage effect + a knockback effect + a self-buff effect.

---

## Powerset categories

Powerset categories determine which AT can use them.

- **Defense** — armor sets. Tanker primary, Brute secondary, Scrapper secondary, Stalker secondary, Sentinel secondary.
- **Melee** — close-range attack sets. Tanker secondary, Brute primary, Scrapper primary, Stalker primary.
- **Ranged** (sometimes "Blast") — ranged attack sets. Blaster primary, Corruptor primary, Defender secondary, Sentinel primary.
- **Manipulation** — Blaster secondary (close-range support).
- **Buff** — team support sets (Empathy, Force Field, Sonic Resonance, etc.). Defender primary, Corruptor secondary, Controller secondary, Mastermind secondary.
- **Debuff** — enemy-weakening sets (Dark Miasma, Radiation Emission, Trick Arrow, etc.). Defender primary, Corruptor secondary, Controller secondary.
- **Control** — hard-control sets (Mind Control, Ice Control, etc.). Controller primary, Dominator primary.
- **Assault** — Dominator secondary (hybrid melee/ranged damage).
- **Pet** — Mastermind primary (Mercenaries, Necromancy, Robotics, Demons, Ninjas, Beast Mastery, Thugs).
- **Pool** — universal pools (Fighting, Speed, Leadership, etc.).
- **Ancillary / Patron** — endgame AT-specific pools at level 35+.
- **Inherent** — auto-granted (Brawl, Sprint, Rest, Fitness pool grants Health/Stamina, AT inherents like Gauntlet).
- **Incarnate** — endgame slot powers (Alpha, Judgement, Lore, Destiny, Hybrid, Interface).

---

## Reading the data

A typical Power record (abridged) from `Powers.json`:

```json
{
  "FullName": "Tanker.Super_Strength.Foot_Stomp",
  "DisplayName": "Foot Stomp",
  "Level": 28,
  "PowerType": "Click",
  "Target": "Foe",
  "EffectArea": "Sphere",
  "Radius": 15,
  "MaxTargets": 16,
  "Accuracy": 1.0,
  "EndCost": 18.5,
  "RechargeTime": 20.0,
  "CastTime": 2.67,
  "Slottable": true,
  "Enhancements": ["Accuracy","Damage","EndRdx","Recharge","Knockback","Knockup"],
  "SetTypes": ["Pet_Box_4","Targeted_AoE_4","Knockback","Damage_Reduction","Force_Feedback"],
  "Effects": [
    {"EffectType":"Damage","DamageType":"Smashing","Magnitude":89.5},
    {"EffectType":"Mez","MezType":"Knockback","Magnitude":0.67}
  ]
}
```

Reading rules:

- A power is **slottable** if `Slottable: true` AND its `Enhancements[]` is non-empty.
- A power **accepts an IO set** if the set's category is in the power's `SetTypes[]`.
- A power's **earliest pickable level** is `Level`. For pool tier-1 powers, that's level 4 (was higher pre-i26).
- A power's **effects array** is the source of truth for what it does. Don't infer behavior from the name alone — check the effects.

---

## Inherent powers worth knowing

Auto-granted to every character (or all of one AT):

- **Brawl** — universal melee attack, level 1, accepts melee enhancements.
- **Sprint** — toggle run speed buff, level 1, accepts run-speed and stealth (Celerity +Stealth proc).
- **Rest** — heal-out-of-combat, level 1, accepts heal/recharge.
- **Health** (Fitness pool, free since i19) — passive HP regen, accepts heal enhancements + global procs (Numina, Miracle, Panacea).
- **Stamina** (Fitness, free) — passive endurance regen, accepts EndMod + Performance Shifter +End proc.
- **Swift** (Fitness, free) — passive run speed buff.
- **Hurdle** (Fitness, free) — passive jump height buff.

These don't take a power-pick slot but DO accept enhancement slots — and the global IOs (Numina, Miracle, LotG, etc.) you slot into them give some of the best returns in the build.

---

## When canonical and Mids disagree

`data/mids/database/Powers.json` is what MidsReborn shows in-app. `data/canonical/powers.json` is what the game client actually sees. They usually agree, but watch for:

- **Mids data updates lag the game** — if Homecoming patched a power last week, Mids may not have caught up.
- **Mids interprets some fields** — e.g. computed damage values for "scale" entries are pre-multiplied; canonical shows scale + multiplier separately.
- **Mids hides some effects** — pure-cosmetic / scripted effects don't show up in Mids' Effects[] but do in canonical.

When the user asks a number-question, prefer canonical. When the user asks a slotting-question, prefer Mids (it owns the allowed-enhancements interpretation).
