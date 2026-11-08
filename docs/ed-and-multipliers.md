# Enhancement Diversification (ED) and Multipliers

Enhancement Diversification (ED) is the diminishing-returns rule that prevents stacking same-aspect enhancements indefinitely.
Without ED, slotting six damage IOs into one attack would multiply damage by ~7.5x; with ED, the practical cap is ~3.95x.

Authoritative data: [data/mids/derived/effect_tables.json](../data/mids/derived/effect_tables.json).
The `MultED`, `MultTO`, `MultDO`, `MultSO`, `MultIO` tables transcribed from `Database` jagged arrays.
Reference code: `MidsReborn/Core/I9Slot.cs::GetScheduleMult()` and `MidsReborn/Core/Enhancement.cs::ApplyED()`.

---

## How an enhancement's value is computed

For a single slot (one enhancement) the contribution is:

```text
contribution = base_multiplier × schedule × relative_level_factor × superior_factor
```

- **`base_multiplier`** — table value from `MultED/TO/DO/SO/IO` keyed by `(grade, schedule)`.
- **`schedule`** — the enhancement's classification (A/B/C/D, see below).
- **`relative_level_factor`** — for non-attuned IOs and SOs, modifies based on RelativeLevel:
  - +5 / +4 / +3: 1.05× (5% bonus per level shift over base)
  - +2 / +1 / +0: 1.0× (full strength)
  - −1: 0.9×
  - −2: 0.8×
  - −3: ~0.6× (varies)
- **`superior_factor`** — `Enhancement.Superior == true` → ×1.25.

For attuned IOs, RelativeLevel = 0 always; you don't get the +1 to +5 boosts but you also can't be exemplared down past your build level.

---

## Enhancement schedules

Each enhancement aspect is on a schedule that controls its strength:

|                        Schedule                         |  TO  | DO (Origin) | SO (+0) | SO (+3) | IO (lvl 50) | Superior IO (+5) |
| :-----------------------------------------------------: | :--: | :---------: | :-----: | :-----: | :---------: | :--------------: |
|                **A** (Damage, Heal, Mez)                | 8.3% |    16.7%    |  33.3%  |  38.3%  |    42.4%    |      53.0%       |
| **B** (Defense, ToHit, Recharge, Acc, EndRdx, Recovery) | 5.2% |    10.4%    |  21.0%  |  24.1%  |    26.5%    |      33.1%       |
|   **C** (Confuse, Stealth, KB Distance, ToHit Debuff)   | 3.1% |    6.3%     |  12.5%  |  14.4%  |    16.0%    |      19.9%       |
|                 **D** (Range Increase)                  | 2.0% |    4.0%     |  8.0%   |  9.2%   |    10.0%    |      12.5%       |

Numbers are approximate; the authoritative table is `effect_tables.json`. Variations exist for older/newer enhancements.

---

## ED tiers (the diminishing-returns formula)

ED applies to the _sum_ of same-aspect enhancements within a single power, computed as a stepwise piecewise-linear function:

| Pre-ED total | Effective total (post-ED) | Notes                               |
| ------------ | ------------------------- | ----------------------------------- |
| 0% – 70%     | 100% pass-through         | First ~70% of stacking is free.     |
| 70% – 90%    | 90% factor                | Slight knee.                        |
| 90% – 100%   | 70% factor                | The "soft cap" for casual slotting. |
| 100% – 200%  | 15% factor                | Hard diminishing returns.           |
| 200%+        | 0%                        | Effectively no further increase.    |

Example: six damage IOs each at 42.4% = 254.4% pre-ED → ~95% post-ED. Three damage IOs = 127% pre-ED → ~95% post-ED.
**The third damage IO gets you most of the benefit; the fourth/fifth/sixth provide little additional damage.**

This is why **Frankenslotting** works: instead of six damage IOs, slot 3x Damage + 2x Accuracy + 1x EndRdx.
Each aspect stays under the ED knee, and you get utility from the other aspects you couldn't fit otherwise.

The exact ED math lives in `Enhancement.ApplyED()`. Reproduce it from there if you need precise numbers; the table above is the standard community simplification.

---

## What ED applies to

ED is **per-aspect, per-power**. Each enhancement aspect (Damage, Accuracy, Defense, Resistance, Recharge, EndRdx, Heal, Range, Run Speed, etc.) is computed independently.
Stacking 3 Damage IOs and 3 Accuracy IOs in the same power → both Damage and Accuracy are at ~95% post-ED, completely independent.

ED does **NOT** apply to:

- **Set bonuses** — uncapped except by the Rule of Five.
- **Global buffs** — Hasten, Build Up, Domination's recharge, Incarnate Alpha's bypass, etc.
- **Procs** — they fire as separate effects, not enhancement values.
- **Inherent power buffs** — Containment, Fury, Defiance, Scourge, Vigilance, etc.
- **Across-power buffs** — leadership toggles, Power Boost, Adrenaline Boost, etc.

Alpha incarnate slots **partially bypass** ED on the chosen aspect: the +33% from Cardiac Core Paragon's End Reduction goes through ED-like checks but on a separate, more generous curve.
Same for Spiritual / Musculature / etc.

---

## Practical slotting math

For a typical attack (single-target):

- **3 Damage / 1 Acc / 1 EndRdx / 1 Recharge** Frankenslotted SOs → ~95% damage, ~21% acc, ~21% endrdx, ~21% recharge.
- **5x Crushing Impact set IOs + 1 proc** → ~71% damage (subject to ED), full set bonuses (typically 5% recharge + accuracy + HP bonuses), 1 chance-on-hit damage proc.
- **6x Crushing Impact set IOs** → ~95% damage (post-ED), full set bonuses including 6th tier.

Your call between Frankenslotted and set: Frankenslotted gets you full enhancement values + procs; set gets you set bonuses but caps the enhancement at the set's per-aspect contribution.

For a defense toggle:

- **5x Reactive Defenses (set)** → typed defense + 5-piece bonuses including +Resist scaling proc.
- **5x LotG (set)** → defense + the +7.5% Recharge unique × however many you slot (up to 5 in build).
- **3x Defense IO + 3x EndRdx IO (Frankenslotted)** → no set bonus, but stronger pure defense and end reduction.

LotG +Recharge is so valuable that **most builds slot it as a 1-slot mule** in inherently-good defense toggles (Combat Jumping, Hover, Maneuvers, Stealth, etc.).
You get the +7.5% global recharge per copy without committing to the full set.

---

## Reading the multipliers in `effect_tables.json`

The structure (transcribed from `Database`):

```json
{
  "MultED":  [[1.0, 0.9, 0.7, 0.15, 0.0, ...], ...],
  "MultTO":  [[0.0833, ...], ...],
  "MultDO":  [[0.1667, ...], ...],
  "MultSO":  [[0.3333, ...], ...],
  "MultIO":  [[0.4243, ...], ...]
}
```

Each row indexes by schedule (0 = A, 1 = B, 2 = C, 3 = D). The values are the per-aspect contribution at full strength.

`I9Slot.GetScheduleMult()` selects the right row based on the slotted enhancement's schedule and grade, then applies RelativeLevel and Superior modifiers.
The result feeds into the `Enhancement.ApplyED()` piecewise function above.
