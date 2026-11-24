# Patch-history reconciliation — escalations

You asked Claude to walk Homecoming patch notes chronologically (latest-wins) and escalate when reconciling can't be done programmatically. Here's where we landed.

Tooling: `parse-patches.ps1`, `check-overrides.ps1` (since removed).

---

## Headline finding (no escalation needed)

The CoD raw-data zip dated **2025-12-09** is **pre-Homecoming-I26P4** for Tanker melee target caps. **35 Tanker AoE max_targets_hit values** in `data/canonical/` are stale relative to the patch
notes. See [SUMMARY.md](SUMMARY.md) for the full list. Claude should treat the override values (from I26P4) as Homecoming-current, not the canonical zip values.

---

## Pipeline summary

| Stage | Count |
| --- | ---: |
| Patch pages walked (I26P1 → I28P3) | 15 |
| Raw structured changes extracted | 127 |
| Distinct (entity, field) pairs after latest-wins dedup | 103 |
| Canonical match (override agrees with zip) | 5 |
| **Override drifts from canonical (apply override)** | **35** |
| Unresolved (needs your input) | 63 |

---

## Escalation bucket 1 — schema mismatch (canonical has nested effects)

Entries where the override field isn't a top-level field in the canonical JSON. Likely the field actually lives under `effects[].magnitude` / `effects[].scale` rather than at the top level.

| Entity | Field | Override value | Canonical file |
| --- | --- | ---: | --- |
| Blaster_Secondary_Revamp.Aging_Touch | damage_scale | 1.02 | `powers\blaster_support\time_manipulation\aging_touch.json` |
| Blaster_Secondary_Revamp.Blinding_Powder | magnitude | 3 | `powers\blaster_support\ninja_training\blinding_powder.json` |
| Blaster_Secondary_Revamp.Telekinetic_Thrust | damage_scale | 2.92 | `powers\blaster_support\mental_manipulation\telekinetic_thrust.json` |
| (~10 more) | various | various | various |

**What I need from you:** confirm whether canonical stores `damage_scale` / `magnitude` under `effects[]` vs. top-level. If yes, the cross-check should walk effects arrays — I can extend the
resolver. If no, these are real drifts that need a different identifier mapping.

---

## Escalation bucket 2 — multi-AT ambiguity (patch lists "Power" without specifying AT)

The same power name exists across many ATs. Patch notes don't always say which AT was changed.

| Entity | Field | Override value | Candidates |
| --- | --- | ---: | --- |
| Blast_Power_Set_Updates.Full_Auto | arc | 90 | 13 canonical files (Blaster, Corruptor, Defender, Sentinel, NPC variants) |
| Earth_Manipulation.Seismic_Smash | magnitude | 3 | 3 forms (blaster_support, mission_maker_secondary, v_arachnos_proxy) |
| (more in SUMMARY.md) | | | |

**What I need from you:** when a patch changes a power across many ATs, do you want me to:
(a) apply the override to all matching files (broad assumption),
(b) apply only when the entity prefix narrows the AT (narrow assumption),
(c) flag every multi-match as ambiguous (current behaviour)?

---

## Escalation bucket 3 — patch references NPC enemies, not player powers

I28P2 has a section adjusting enemy stats (Crey, Skulls, Gold Brickers, Warriors). Canonical zip stores enemy archetypes under `data/canonical/archetypes/<rank>.json` (e.g. `boss_grunt.json`,
`minion_swarm.json`), not under `powers/`.

| Entity | Field | Override |
| --- | --- | ---: |
| Enemies.Crey_Industries.Crey_Agents | max_targets_hit | 1 |
| Enemies.Skulls.Death_Heads | accuracy | 2.5 |
| Enemies.Warriors | damage_scale | 1.32 |
| (more in SUMMARY.md) | | |

**What I need from you:** confirm whether NPC enemy stats matter for build planning. If yes, I'll add an `archetypes/` resolver path. If they're informational only (no direct build impact), I can
mark these as "informational-only" and skip the cross-check.

---

## Escalation bucket 4 — section heading lost in entity path

Some entities lost their AT/powerset context because of how the wiki nests headings. E.g. `Earth_Assault | recharge_time | 14` came from the section `Powers: Dominator Assault Sets > Earth Assault`,
but my parser kept only the innermost. The dropped context was needed to resolve to `dominator_assault/earth_assault/...`.

Examples:

- `Earth_Assault.<power>` → should be `Dominator.Earth_Assault.<power>`
- `Disruption_Arrow` → likely `Trick_Arrow.Disruption_Arrow` (Defender/Corruptor/Mastermind)
- `Cauterizing_Aura` → `Blaster.Fiery_Manipulation.Cauterizing_Aura`

**What I need from you:** approve a heuristic to enrich entity paths with implied AT context based on patch section names ("Blaster Secondary Revamp" → Blaster, "Dominator Assault Sets" → Dominator,
etc.). I can encode the mapping; ~15 patterns covers most cases.

---

## Wiki typo I noticed in passing

[Issue 26 Page 4](https://homecoming.wiki/wiki/Issue_26_Page_4) lists `Tanker > Dark Melee > Shadwo Maul 5 => 10`. The power name is **Shadow Maul**; "Shadwo" is a typo in the wiki itself. My parser
preserves it; canonical lookup fails. Worth noting but not actionable on our side.

---

## Suggested next steps (your call)

1. Approve / adjust the four escalation buckets above. I implement the chosen heuristics in `parse-patches.ps1` / `check-overrides.ps1` and re-run.
2. Spot-check the 35 confirmed drifts — open a few in MidsReborn and confirm the in-app values match the override (16) not the canonical (10).
3. Decide retention policy for `current_overrides.json`: keep it as a sidecar that Claude consults, or merge it into a regenerated `data/canonical/` copy where overrides have been applied?
