# Rule: How we create builds

**Status:** MUST. Applies to every build emitted in this project.

This document is the playbook for producing correct, playable, comparable builds. The principles are stated as positive practices.
The "Lessons learned" section at the end records the v1 TW/Bio Scrapper benchmark errors that motivated each principle, so future-you understands the precedent.

## Principles (positive framing)

### P1. Format is a spec — read the source first

When producing an artifact that mimics a tool's output (e.g. MidsReborn's forum export), read the tool's emitter source before drafting.
  The format detail is fine-grained and easy to invent incorrectly from memory.

For MidsReborn forum exports:

- `(N)` = character level when the slot was placed in this power.
- `(A)` = inherent slot (free with the power).
- IO level (when shown) goes after the name as `: Level XX` or `:XX`.
- Default for set IOs at level 50: omit the IO level (assumed character level / attuned).

Reference: [`MidsReborn/clsOutput.cs:240`](../../../../MidsReborn/MidsReborn/clsOutput.cs).

### P2. Builds are level-by-level allocations

Every build models the level-by-level allocation of:

- **Power picks** at their `Level` field from `Powers.json` or canonical.
- **Slots** at the per-level grant levels from `Levels.json` (or the standard CoH schedule when `Levels.json` isn't harvested).

A slot at level X satisfies all three:

1. The level-X slot grant exists in the schedule.
2. The power receiving the slot was picked at level ≤ X.
3. The power has fewer than 6 total slots before the addition.

Walk levels 2–50 in order and confirm each slot satisfies these.

### P3. Track running constraints during draft

Maintain a running tally during composition, not an at-end check:

- 67 added slots total (Homecoming `MaxSlots`).
- 5 added slots per power max (1 inherent + 5 added = 6 total).
- 24 power picks.
- 4 power pools max.

When the running tally approaches a cap, re-budget early. Trim in-progress, not after.

### P4. Round-trip prose against content

When prose claims something about the artifact's format, structure, or content, the artifact demonstrably matches. Before emit:

- Search the prose for format claims (notation, layout, fields).
- Verify each claim against a sample line in the artifact.
- When they conflict, fix one or the other.

Show the format by using it; annotate only where it's non-obvious or where the artifact deviates from a documented spec.

### P5. Use the canonical home for unique globals

Unique globals always go in their canonical homes:

| Unique | Canonical home |
| --- | --- |
| Numina's Convalescence — +Regeneration/+Recovery | Health |
| Miracle — +Recovery | Health |
| Panacea — +Hit Points/Endurance | Health |
| Regenerative Tissue — +Regeneration | Health |
| Performance Shifter — Chance for +Endurance | Stamina |
| Power Transfer — Chance for Self Heal | Stamina (or any toggle/click) |
| Steadfast Protection — Resistance/+Defense | any resistance set with open slots |
| Gladiator's Armor — TP Protection/+3% Defense (All) | any resistance set |
| Shield Wall — +Resist (All) | any defense set |
| Reactive Defenses — Scaling Resist Damage | any defense set |
| Luck of the Gambler — Defense/Increased Recharge Speed (+7.5% rech global) | up to 5 defense toggles (CJ, Hover, Maneuvers, Stealth, Weave, EA, etc.) |
| Celerity — +Stealth | Sprint |
| Blessing of the Zephyr — Knockback Reduction | any travel power |
| Winter's Gift — Slow Resistance | any travel power |

Fitness pool (Health, Stamina, Swift, Hurdle) is auto-granted on Homecoming. The Health/Stamina uniques go in those auto-granted powers.

### P6. Cite canonical data; treat training memory as approximate

For specific numbers (recharge, end cost, damage scale, max targets, power pick level, ancillary pool composition, set bonus values), the order is:

1. `data/canonical/powers/<role>/<set>/<power>.json` — primary source.
2. `data/diff/current_overrides.json` — patch-history corrections.
3. Captures in the source root (private `homecoming-source-material` repository, found through `--source-root`, else `COH_SOURCE_ROOT`, else `../homecoming-source-material`).
4. Training memory — labeled "approximate; verify in MidsReborn" when used.

For ancillary pool / patron pool composition, verify the powers exist in the canonical zip before listing them.

### P7. Emit JSON alongside markdown

Every build proposal produces two artifacts:

- **JSON** — schema-valid per [`build-format/schema.json`](../../../build-format/schema.json). The loadable artifact; the user pastes it into MidsReborn.
- **Markdown** — MidsReborn forum-export format. For review, comparison, and forum sharing.

The JSON is the source of truth; the markdown is a view. They agree on every power, slot, and enhancement. When they conflict, the JSON wins.

### P8. The trade-offs section presents real design tensions

The "Trade-offs" section contains genuine design choices:

- Defense vs damage trade-offs.
- AoE vs single-target priority (e.g. Whirling Slice for AoE coverage vs Shatter Armor for ST finisher).
- ATO 6-piece bonuses vs procmonster slotting.
- Specific slotting picks where two options are competitive at level 50.

Arithmetic mistakes get fixed before emit. Format errors get fixed. Unverified specifics belong in "What I would verify."

### P9. Frontload defensive toggles per the exemplar-10 rule

Per [`exemplar-10.md`](./exemplar-10.md):

- Defensive toggles get ≥ 2 added slots by character level 15.
- Anchor attack gets ≥ 2 added slots by character level 14.
- Every attack's inherent slot (A) and first added slot hold an Acc-bearing IO (Acc/Dmg, Acc/Dmg/Rchg, Acc/Dmg/EndRdx, Acc/Dmg/EndRdx/Rchg). Damage-only pieces appear at slot 3 or later.
  See [`exemplar-10.md` § Slot-aspect priority for attacks](./exemplar-10.md#slot-aspect-priority-for-attacks-accuracy-first).

Confirm on the slot-allocation table by the level-15 row.

### P10. Build-specific format claims live in the artifact, not the prose

When the format is non-obvious, annotate it inline near the affected lines. When the format is standard MidsReborn forum export, let the artifact speak for itself — no narrative format claim required.

---

## Pre-flight checklist

Walk this list before emitting any build. Every item passes; otherwise the build doesn't ship.

### Format

- [ ] Slot prefixes use `(A)` for inherent and `(N)` for character-level placement, where N is a real slot-grant level from the in-game schedule.
- [ ] Slot prefixes ascend through the level schedule (not all `(50)`).
- [ ] IO level (when shown) appears as `: Level XX` or `:XX` after the enhancement name.
- [ ] Set names match canonical (`data/canonical/boost_sets/<name>.json::display_name`).

### Hard limits

- [ ] Total added slots = 67 (or under, with intentional slack noted as design choice).
- [ ] Per-power added slots ≤ 5.
- [ ] Power picks = 24 (excluding inherents).
- [ ] Power pools ≤ 4.
- [ ] Tier prerequisites respected (T1/T2 before T3 before T4 within a pool).
- [ ] Each power picked at or after its `Level` from `Powers.json`.
- [ ] Each slot placed at or after its power's pick level.
- [ ] Each slot placed at a real slot-grant level.

### Exemplar-10 playability

- [ ] Picks at levels 1–14 cover the functional core: 2 attacks, 1 defensive layer (where AT has one), travel power picked by level 12, Build Up / Aim if AT has one, sustain mechanic if needed.
- [ ] Defensive toggles have ≥ 2 added slots by character level 15.
- [ ] Anchor attack has ≥ 2 added slots by character level 14.
- [ ] Every attack's slot (A) and first added slot hold an Acc-bearing IO; damage-only pieces appear no earlier than slot 3 (exceptions: pre-buffed snipes, procmonster vehicles, auto-hit powers).

### Verification against data

- [ ] Each power's pick level checked against canonical or in-app MidsReborn.
- [ ] Patch overrides in `data/diff/current_overrides.json` consulted for any field cited.
- [ ] Ancillary pool composition verified against canonical.

### Output

- [ ] JSON artifact emitted, validated against `build-format/schema.json`.
- [ ] Markdown artifact emitted, mirroring MidsReborn's forum export format.
- [ ] Both artifacts agree on every power, slot, and enhancement.

### Honesty

- [ ] Trade-offs section contains design tensions only.
- [ ] Unverified specifics labeled "approximate; verify in MidsReborn" with a link to where the user can verify.
- [ ] What-I-would-verify section enumerates the gaps with concrete actions.

When any check fails, surface the failure and either fix it or escalate to the user with a concrete description of what's wrong.

---

## Lessons learned (precedent record)

The principles above were derived from concrete failures in the v1 TW/Bio Scrapper benchmark.
Recorded here so future-you understands the precedent and the 5 Whys reasoning that produced each principle.

### L1. Slot-prefix format (motivates P1, P2)

**Failure observed:** Every slot line read `(50) Set Name - Aspect`, treating the parenthesized number as the IO level. The actual format uses it for slot placement level.

**5 Whys:**

1. Why did slots use `(50)` everywhere? — IO level (most modern IOs are level 50) was conflated with slot-placement level.
2. Why did the conflation happen? — Two MidsReborn notations were merged: `:Lvl 50` for IO level and `(N)` for slot placement.
3. Why did the merge happen? — The format was drafted from training memory without checking the emitter.
4. Why no emitter check? — The build was treated as a writing task, not a code-validation task.
5. Why is that wrong? — The artifact directly imitates a tool's machine output. The format is a spec, not a style choice.

**Resulting principle:** P1 (read the emitter source first), P2 (model level-by-level allocations).

### L2. Slot count overshot the cap (motivates P3)

**Failure observed:** Initial draft was 9 slots over the 67-slot cap; trim was hand-waved in the trade-offs section.

**5 Whys:**

1. Why was the count over? — Many powers got 6 slots without tracking running totals.
2. Why no tracking? — Per-power slotting was drafted in isolation.
3. Why isolation? — At-end validation was assumed sufficient, software-design style.
4. Why assume that? — Habit from larger systems where validation is end-of-pipeline.
5. Why is that wrong here? — Builds have a small, fixed budget (67 slots). Constraints are continuous, not terminal.

**Resulting principle:** P3 (track running constraints during draft).

### L3. Format claim in prose, different format in artifact (motivates P4, P10)

**Failure observed:** The reply prose said "slot lines in `(A)`, `(3)`, `(5)`, …" but the artifact used `(50)` throughout.

**5 Whys:**

1. Why the mismatch? — Prose was drafted aspirationally; artifact was drafted independently.
2. Why drift? — No round-trip validation between the two.
3. Why no round-trip? — Coherent intent was assumed to produce coherent artifact.
4. Why is that wrong? — Writing is a sequence of small decisions, each can drift.
5. Why does drift matter? — A build's whole value is its consistency between description and content.

**Resulting principle:** P4 (round-trip prose against content), P10 (build-specific format claims live in the artifact).

### L4. Unique globals placed in the wrong power, then "corrected" mid-document (motivates P5)

**Failure observed:** Numina/Miracle/Panacea/Regen Tissue uniques placed in Inexhaustible (Bio passive), then a self-correction noting Fitness is auto-granted, leaving inconsistent guidance.

**5 Whys:**

1. Why placed in Inexhaustible? — Bio's Inexhaustible has +HP/regen properties similar to Health.
2. Why the conflation? — Missing quick-reference for "where do unique globals go" by AT.
3. Why no quick-reference? — Hadn't authored one yet.
4. Why does the gap cause confusion? — Unique-global placement is high-impact and easy to get wrong.
5. Why are we still doing this from memory? — The reference was a real gap in the project's docs.

**Resulting principle:** P5 (use the canonical home table).

### L5. Power levels unverified (motivates P6)

**Failure observed:** TW power tier-unlock levels used training approximations because canonical's `available_at_level` field returned empty.

**5 Whys:**

1. Why training approximations? — Canonical's `available_at_level` was empty; fell back to memory.
2. Why fall back rather than dig further? — Time pressure.
3. Why time pressure? — User said "now"; speed was prioritized over correctness.
4. Why is speed wrong here? — The build was a benchmark for correctness comparison.
5. Why does that matter? — A wrong artifact is worth less than no artifact in a comparison study.

**Resulting principle:** P6 (cite canonical first, label training memory as approximate).

### L6. No JSON output alongside markdown (motivates P7)

**Failure observed:** Only the markdown was produced. The build can't be loaded into MidsReborn without manual reconstruction.

**5 Whys:**

1. Why no JSON? — User asked for "Markdown rendering similar to forum output."
2. Why was that interpreted as markdown-only? — Took the request literally.
3. Why not produce both? — Optimized for the explicit request, not the implicit need.
4. Why is JSON the implicit need? — JSON is the loadable artifact; markdown is presentation.
5. Why does that matter? — A build that can't be loaded can't be tested or compared. Markdown alone is unfalsifiable.

**Resulting principle:** P7 (emit JSON alongside markdown).

### L7. Trade-offs section contained arithmetic errors (motivates P8)

**Failure observed:** "Trade-offs" listed "slot budget over by ~7" as a trade-off rather than fixing it.

**5 Whys:**

1. Why was the error in trade-offs? — Treated as something to disclose rather than fix.
2. Why disclose rather than fix? — Time pressure and momentum.
3. Why does disclosure feel acceptable? — "Honesty" can mask unfinished work.
4. Why is that wrong? — Trade-offs are real choices; errors are fixable problems. Conflating them devalues both.
5. Why does the distinction matter? — A reader trusts trade-offs as design decisions; errors framed as trade-offs erode trust.

**Resulting principle:** P8 (trade-offs contain design tensions only).

### L8. Defensive frontloading rule existed but wasn't applied (motivates P9)

**Failure observed:** [docs/slotting-and-leveling.md](../../slotting-and-leveling.md) says "≥ 2 added slots in defensive toggles by level 14."
The v1 build had only 1 added slot in Hardened Carapace by level 14.

**5 Whys:**

1. Why didn't the build apply its own rule? — Slot-allocation walk wasn't checked against the rule.
2. Why no check? — Assumed the rule was internalized.
3. Why isn't internalization sufficient? — Rules don't apply themselves; they need explicit verification.
4. Why no explicit verification? — Pre-flight checklist hadn't been written yet.
5. Why hadn't it? — Project gap.

**Resulting principle:** P9 (frontload defensive toggles), and the pre-flight checklist that confirms it.

---

## Reference

- [`hard-limits.md`](./hard-limits.md) — game-engine invariants.
- [`exemplar-10.md`](./exemplar-10.md) — universal exemplar-10 playability rule.
- [`naming-and-words.md`](../naming-and-words.md) — generic-reference rule.
- [`docs/slotting-and-leveling.md`](../../slotting-and-leveling.md) — slotting + leveling mechanics.
- [`docs/build-types-and-goals.md`](../../build-types-and-goals.md) — build type taxonomy.
- [`build-format/schema.json`](../../../build-format/schema.json) — JSON schema for build artifacts.
- [`MidsReborn/clsOutput.cs`](../../../../MidsReborn/MidsReborn/clsOutput.cs) — forum export emitter (source of truth for output format).
