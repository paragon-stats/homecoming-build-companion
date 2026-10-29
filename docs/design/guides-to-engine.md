# Guides to engine knowledge: design and plan

Status: design, not yet started. Measured on 2026-10-27 while the pre-sunset crawl was still writing.
Every count below is a snapshot; the crawl adds files until it finishes, so re-run the counts then.

The counts came from throwaway measuring scripts that were not kept; Phase 0's tested guide index reproduces them.

## 1. The decision in one page

- **The engine owns every number.** Guides never supply a recharge, a cap, a slot count or a set bonus.
  A guide claim that carries a number is kept only as a question for the engine to answer, or dropped.
- **The guides own intent.** Per powerset, the knowledge records which powers carry the set's core mechanic,
  what role each power plays in each named playstyle, what works together, and what to avoid. Each claim names its source guides.
- **The engine loads the knowledge, not the session.** Given the build's archetype and powersets and the chosen playstyle, code loads every matching record in full, every time.
  The session's only input is the playstyle choice. No session reads an index or decides which knowledge applies.
- **Knowledge becomes checks that can fail.** Every claim that affects a build (a key power, a required role, a pitfall, a slotting intent) carries a machine check kind
  and runs as a registered rule in `validate` and `score`, producing diagnostics. Claims that cannot be checked are descriptive and never steer a build.
- **The gate is in the code path.** The `.mbd` writer refuses to emit a build that has not passed `validate` and the playstyle `score`, the same way it already refuses
  off-schedule picks with E46 to E49. A CI job runs the same checks on committed builds. No path produces a build that skipped the knowledge.
- **Model reading happens once, at extraction,** under code checks: every quote must be verbatim in its source file and every power name must resolve.
  The distilled guides are rendered from the records for people to read. They are output, never input a session is told to read.
- **Shape:** one JSON record per powerset and per archetype under `knowledge/coh/`, validated by a schema and listed in a code-read manifest.
  Each claim is scoped to the archetype version of the set it came from.
- **Gaps are filled by transfer first.** A powerset plays the same across archetypes apart from the archetype's scaling numbers, so claims from a guided archetype's version
  carry over to an unguided one. Code maps powers between the versions, and the archetype modifier tables give the numbers.
  Archetype-specific powers and inherents are compared from game data. Writing a guide from scratch is the fallback for sets with no guided version on any archetype.
  Transfer is measured before it is trusted: predicted claims for pairs that have their own guides are compared with those guides, and transfer blocks builds only where that test passes.
- **Current game:** each claim is dated by its guide, the patch timeline in `data/patches/` is searched for later changes to the same power or set,
  and a short catalog of game-wide changes retires whole classes of advice. Each claim ends as keep, correct or drop, with the patch record that decided it.
- **Authority, highest first:** hard limits and legality, the exemplar-10 gate, the goal profile's targets and caps, intent, then raw numeric strength.
- **First phase:** a tested guide subject index plus the internal-to-display power name resolver (#48), run once the crawl ends. No model reading.
  It also produces the per-archetype gap split that decides which versions are filled by transfer and which by writing.

## 2. Current-state inventory

### 2.1 What the guides are

| Source | Files | Span | Notes |
| --- | ---: | --- | --- |
| `community/pre-sunset/` (crawl running) | 1,398 at count | 2004 to 2012 | 7,835 threads in the ledger; 1,405 captured, 6,179 "not guide-shaped", 248 "not build-related" |
| `community/` live Homecoming captures | 123 (excluding `pre-sunset/`) | 2019 to 2026 | 2,296 ledger rows; 175 captured; 1,145 duplicate URL rows in the ledger |
| `community/builds_archive_index.json` | index of 7,922 Mids build files | i9 to i26, 5,508 tagged i25 | the build files themselves are not in the repo (see 2.5) |

Pre-sunset folders: `archetypes/` 985, `mechanics/` 288, `powersets/` 104, `theme/` 21.
By year: 2005 104, 2006 178, 2007 149, 2008 115, 2009 111, 2010 269, 2011 324, 2012 135.
Every pre-sunset file has the default `trust: single-author-opinion`, `contradicts_data: none` and the "Summary pending review" stub. Each holds the opening post only.
The ledger has 7 duplicate content hashes and 6 duplicate URLs to fold.

The crawl order shapes the counts. Sections so far are the Blaster and Brute forums and the Player Guides forum (plus an earlier unsectioned run).
That is why Brutes (527 now) and Blasters (328) dwarf every other archetype. Expect the remaining archetype forums to change this table.

### 2.2 What kind of document a capture is

The guide gate (`scripts/forum_scrape/guides.py`) keeps opening posts of 500 or more words with enough bold runs, lists and tables.
A pasted Mids forum export passes that gate, so in the archetype forums most captures are build-help posts, not guides:

| Archetype folder | Pre-sunset files | With a Mids export | Tagged discussion |
| --- | ---: | ---: | ---: |
| Brute | 527 | 495 | 244 |
| Blaster | 328 | 293 | 108 |
| Defender | 26 | 1 | 1 |
| Scrapper | 22 | 5 | 2 |
| Tanker | 22 | 6 | 0 |
| Corruptor | 19 | 2 | 0 |
| Controller | 16 | 1 | 0 |
| Stalker | 15 | 2 | 0 |
| Warshade | 13 | 3 | 0 |
| Mastermind | 12 | 0 | 0 |
| Dominator | 11 | 2 | 0 |
| Peacebringer | 8 | 1 | 0 |
| Arachnos | 5 | 2 | 0 |
| Kheldian | 4 | 0 | 0 |
| Sentinel | 0 | 0 | 0 |

Across all pre-sunset files, 864 contain a Mids export. These are valuable in a different way from prose:
each is a full set of power picks and slot counts that code can read without a model.
The powerset folder (104 files) and the per-AT forums outside Brute and Blaster hold the real power-by-power guides.
A sampled 2005 Ice Armor guide shows the typical shape: a bold heading per power, a verdict ("the defining power", "garbage, skip it"), and a pre-Enhancement Diversification slotting line.

### 2.3 Tagging is too coarse to find a powerset's guides

`topic_tags` comes from a keyword list run over the first 4,000 characters. It over-fires: `recharge-endurance` is on 1,177 of 1,398 files and `tools` on 835.
A powerset tag is added only when the title names a canonical powerset folder, and only outside archetype folders, so only about 100 files carry one.
Folder names also differ from display names (`bio_organic_armor` is Bio Armor, `brawling` is Street Justice, `kinetic_attack` is Kinetic Melee, `shock_therapy` is Electrical Affinity).
Finding a powerset's guides therefore needs a new subject index over titles and bodies using display names, aliases and power names, not the existing tags.

### 2.4 Era markers

150 titles carry an Issue marker (`I7`, `Issue 12`, `i13`), and 61 bodies name Issue 13 to 24.
147 files discuss Enhancement Diversification, 994 mention inventions or set bonuses, 173 mention incarnates.
`date_posted` is present on every capture; with the issue dates in `data/patches/` it maps each guide to the issue live when it was written.

### 2.5 Powerset coverage and the gap list

Method: a guide covers a powerset when its title names the set (display name, internal name, or a short alias list), or the body names it three or more times.
This is a lower bound for shorthand ("SS", "Inv", "WP", "Fire/Kin" are not matched) and an upper bound for sets whose name is also a common word or a power name
(Regeneration 316, Ice Blast 50, Fire Blast 46, Force Field 47 and Invulnerability 73 are inflated). 130 primary and secondary sets were measured; pools and epics were not.

**No coverage at all (37 sets):**

- Blast sets: Beam Rifle, Seismic Blast, Storm Blast, Water Blast.
- Blaster secondaries: Atomic Manipulation, Darkness Manipulation, Earth Manipulation, Martial Combat, Ninja Training, Plant Manipulation, Sonic Manipulation, Tactical Arrow.
- Melee: Psionic Melee, Radiation Melee, Savage Melee, Sonic Melee, Staff Fighting.
- Armor: Psionic Armor.
- Support: Darkness Affinity, Electrical Affinity, Marine Affinity, Nature Affinity.
- Control: Arsenal Control, Darkness Control, Pyrotechnic Control, Symphony Control, Wind Control.
- Dominator assault: Arsenal Assault, Dark Assault, Icy Assault, Martial Assault, Radioactive Assault, Savage Assault, Sonic Assault.
- Mastermind: Beast Mastery.
- Arachnos Soldier: Bane Spider Soldier, Crab Spider Soldier.

**Thin coverage, one to four guides (31 sets):** Arachnos Soldier, Demon Summoning, Earth Assault, Electric Control, Fiery Assault, Fortunata Training, Luminous Aura,
Night Widow Training, Radiation Armor, Broad Sword, Energy Assault, Ice Manipulation, Pain Domination, Sonic Attack, Temporal Manipulation, Thermal Radiation,
Time Manipulation, Widow Training, Battle Axe, Cold Domination, Electricity Assault, Electricity Manipulation, Fire Manipulation, Luminous Blast, Psionic Assault,
Street Justice, Thorny Assault, Bio Armor, Mental Manipulation, Umbral Aura, Umbral Blast.

**Not in the game data at all:** Issue 28 Page 4 (2026-10-06, `data/patches/2026-10-06-issue-28-page-4-r47.json`) added Light Affinity
(support for Defenders, Controllers, Corruptors and Masterminds) and a new armor set for the five melee-armor ATs whose first power heading is Sonic Aura.
Neither is in `data/canonical/` (client data dated 2025-12-09) or the Mids database 2026.1.1242 the engine runs on.

Many of the zero-coverage sets arrived late in 2012 or on Homecoming, after the archive's guides were written.
Live-era coverage is thin too: most sets have zero to two live captures.
The build archive partly fills the gap: by its file names it holds i25 builds for Water Blast (156), Staff Fighting (140), Beam Rifle (110),
Radiation Melee (100), Street Justice (94), Psionic Melee (70) and Savage Melee (68).

**Archetype gaps:** Sentinel has no pre-sunset guides (it is a Homecoming archetype), one live capture, and 192 archive builds.
Arachnos Soldier and Widow have 5 pre-sunset captures and their archive builds sit under an untagged "Veat" folder.
Controller has 16, Mastermind 12, Dominator 11 and the Kheldians 25 between them; all thin.
`community/builds/` holds 7 build files (Blaster 2, Defender 1, Scrapper 3), so most archetypes have no curated sample build there; `samples/builds/` adds about 20 more `.mbd`/`.mxd` files.

### 2.6 The gap split by archetype version

The unit that needs knowledge is a powerset on an archetype, not a powerset. The 130 sets give 358 (set, archetype) pairs; 83 sets exist on two or more archetypes.
A pair has its own guides when a guide covering the set is in that archetype's folder or names the archetype in its title. Same method and caveats as 2.5.

| Pair group | Pairs | How it is filled |
| --- | ---: | --- |
| Own guides | 155 | extraction (section 4) |
| No own guide, guided on another archetype | 100 | transfer from that archetype's version (5.4) |
| No own guide, set guided but no guide names an archetype | 21 | transfer with the source archetype unknown, warnings only |
| No guide for the set on any archetype | 82 (37 sets) | power-level transfer where powers are shared, else written from scratch (5.6) |

The 100 transfer targets by archetype: Sentinel 19, Tanker 17, Scrapper 16, Stalker 14, Defender 9, Mastermind 7, Corruptor 6, Brute 6, Controller 4, Blaster 2.
Brute and Blaster dominate the sources only because the crawl reached their forums first.

Within the 37 unguided sets, many reuse powers from guided sets under the same internal name (Dominator assault sets borrow from blast and melee sets, Blaster secondaries from melee and support sets).
Share of each set's powers that also appear by internal name in a guided set:

- **Half or more (9 sets), power-level transfer carries most claims:** Icy Assault, Darkness Manipulation and Dark Assault (89%), Darkness Affinity and Arsenal Assault (78%),
  Plant Manipulation and Earth Manipulation (67%), Tactical Arrow (56%), Bane Spider Soldier (50%).
- **Some (19 sets), transfer for the shared powers, writing for the rest:** Sonic Assault and Radioactive Assault (44%), Sonic Melee and Martial Assault (33%),
  Savage Melee and Radiation Melee (31%), Martial Combat (30%), Staff Fighting and Psionic Melee (25%), Ninja Training (22%), Crab Spider Soldier, Beam Rifle and Arsenal Control (20%),
  Sonic Manipulation, Darkness Control and Atomic Manipulation (11%), Storm Blast (10%), Seismic Blast (9%), Psionic Armor (8%).
- **None (9 sets), written from scratch:** Water Blast, Wind Control, Symphony Control, Pyrotechnic Control, Savage Assault, Nature Affinity, Marine Affinity, Electrical Affinity, Beast Mastery.
  Light Affinity and the new armor set join this group once they are in the data.

The test set for transfer (5.5) is the sets with three or more own guides on at least two archetypes; at this snapshot there are 14:
Regeneration, Claws, Invulnerability, Fire Blast, Willpower, Energy Melee, Kinetics, Force Field, Spines, Super Strength, Stone Armor, Stone Melee, Poison, Katana.
Regeneration's counts are inflated by the word's ordinary use, so its pairs need hand-checked guide lists before they count.

### 2.7 Patch history in the repo

`data/patches/` (written by `python -m scripts.forum_scrape.patch_notes`, documented in `data/patches/README.md`) holds 350 dated records and 23,292 change lines:

| Source | Records | Lines | Lines with a powerset field |
| --- | ---: | ---: | ---: |
| Paragon Wiki archive, 2004 to 2012 | 232 | 8,980 | 0 |
| Homecoming beta forums, 2019 to 2022 | 72 | 7,492 | 530 |
| Homecoming release notes, April 2023 on | 46 | 6,820 | 1,125 |

Only about 7% of lines name their powerset; the rest name a power in running text. Some fields are wrong where a heading carries over
(February 2026 lines under a "Kinetic Melee" heading are tagged Storm Blast). The README names the one span no source covers: the 2012 sunset to the 2019 relaunch.

Separately, `data/diff/current_overrides.json` (103 overrides) was built by an older PowerShell parser from `data/diff/patch_history/` (Issue 26 Page 1 to Issue 28 Page 3 only).
It has not been regenerated from `data/patches/`, so every change since November 2025 is missing from it, including Issue 28 Page 4's
new sets, its raised damage caps (+400% for Controllers, Defenders, Dominators, Masterminds, Kheldians and Arachnos), and the February and June 2026 powerset passes.

### 2.8 What the engine computes today

Computes, checked against Mids reference dumps:

- Build totals and AT caps (`base_totals.py`), enhancement values after Enhancement Diversification (`enhancement.py`, `ed.py`).
- Set bonuses and the Rule of Five (`set_bonuses.py`), incarnates (`incarnate.py`).
- Legality (`legality.py`, rules H-ENH-001 to 007) and hard limits (`hard_limits.py`, H-SLOT-001 to 005, H-POWER-001 to 005, H-POOL-001 and 002), both registries.
- Goal scoring against `strategy/build_profiles.json` (`scoring.py`): positional defense, minimum resistance, perma-Hasten, perma-Domination; cap adherence (P-CAP-001) and endurance (P-END-001).
- Attack chains, an endurance budget and a classifier that labels each power's delivery, shape and role from canonical data (`attack_chain.py`, `chain_report.py`, `docs/engine/chain-planner.md`).
- A level-by-level `.mbd` writer (`buildfile/mbd.py`) that already refuses bad input: E46 for a pick off its schedule level, E47 to E49 for slots it cannot place.
- The Mids dump harness (`tools/mids-dump`), which reloads builds in Mids headless and dumps their totals.
- Archetype scaling, two layers. `attribmod.py` ports Mids' modifier tables and `GetModifier` lookup: every effect scale (damage, buffs, debuffs, heals, control)
  resolves through the archetype's table column at level 50, so the same power yields each archetype's own numbers. This is the Mids-faithful layer.
- `at_modifiers.py` adds inherent damage effects as expected-value rules, not a Mids port: Fury, Containment, Scourge, Defiance, Vigilance, Scrapper criticals,
  Assassination, Opportunity, Supremacy and the two Kheldian inherents. Domination, Gauntlet and both Arachnos Conditionings are registered as neutral because they have no damage effect.
  Non-damage parts of inherents (Domination's control magnitude, Gauntlet's taunt, hide) are not modelled.

Cannot compute yet, by open issue:

- Proc chances (#40), full per-power damage with crits and conditionals (#31), all-aspect enhancement routing (#39), lore pets (#41), some pet damage (#71).
- Hit chance and team buffs in chain DPS (#70), repeat attacks and snipes in a cycle (#72), linked-entity -Res weighting (#73).
- Any powerset or change after Mids database 2026.1.1242 (#20 refreshes it); the real-game exemplar set-bonus rule (#64).
- A search for builds (#21, scoped to slotting only).
- A declared archetype on the build input and AT powerset legality (#35).
- Two rules the rule files name but no code registers: accuracy-first attack slotting (P-SLOT-001) and the exemplar-10 gate.
  `build_profiles.json` declares `universal_gate: exemplar-10`; nothing enforces it.
- A `score` subcommand: `python -m coh_engine` has `rules`, `validate` and `chain`, while `check-registry.md` describes `score`.
- Validation of a build the writer has just produced: `validate` reads a Mids dump directory, not a build spec or `.mbd` (#45 adds `.mbd` input).

### 2.9 The knowledge tree and the navigation rule

`knowledge/coh/` does not exist. `.claude/rules/coh-knowledge-navigation.md` and `CLAUDE.md` (reading order item 9) tell a session to read `knowledge/coh/INDEX.md`
and load only the files it points to, and to stop and ask while it is missing.
That is a reading instruction nothing enforces: a session can skip it, read part of it, or fill gaps from memory, and the build still looks finished.
This design replaces it (section 7.5).

Issues #35 and #36 cite a project memory, `project_at_powerset_framework.md`, for "the full vision and the two nested templates"; that memory file does not exist.
The two templates survive in the issue bodies, which is what section 5 builds on.

## 3. The knowledge model

### 3.1 What is extracted, and which claims become checks

Per powerset, claims of a small closed set of types. A claim type either becomes a registered check or stays descriptive; nothing in between.

| Claim type | Meaning | Becomes |
| --- | --- | --- |
| `core_mechanic` | the mechanic the set is built around (Domination, Fury, a combo meter, a form switch) and the powers that drive it | P-INTENT-001 for the driving powers; an existing or new metric where the mechanic has one (perma-Domination already does) |
| `power_priority` = must | the build is not this playstyle without it | P-INTENT-001, must-take missing |
| `power_priority` = skip | a trap for this playstyle | P-INTENT-002, skip power taken |
| `power_priority` = strong or situational | worth taking, in order | the `intent_alignment` metric (a weight, not a pass or fail) |
| `power_role` and the variant's required roles | anchor attack, AoE, mez protection, a damage spike, a heal, travel | P-INTENT-003, required role unfilled; the `role_coverage` metric |
| `pitfall` | a trap with a machine-checkable form (a knockback power without knockback-to-knockdown, an endurance-heavy toggle set without recovery) | P-INTENT-004, one check kind per pitfall form |
| `slotting_intent` | qualitative only: knockback to knockdown, proc carrier, set mule for a unique, accuracy first | P-INTENT-005, and a filter on the optimizer's choices; accuracy-first is the existing P-SLOT-001 |
| `synergy` | two powers, a set pair, or a set and a pool or epic that work together, stated as effects | a bonus inside `intent_alignment`; never a pass or fail |
| `playstyle_variant` | a named way to play the set with the role weights it implies | selects which checks and weights apply; not a check itself |

Every claim of the first six rows must carry a `check` naming a registered check kind and its arguments; the schema rejects a build-affecting claim without one.
A guide statement with no machine-checkable form is stored as a `note` and is descriptive only.

Descriptive only, never steering a build: the set's identity text, reasons, notes, AT differences prose, exemplar-10 commentary, the change log, sources,
and the effects tables (which code generates from `data/canonical/`, the effects catalog issue #36 asks for).

Numbers are not a claim type. A guide sentence with a number is reduced to its intent ("Hoarfrost can be made permanent") and becomes an engine question
the engine answers for the build at hand. The original number is never stored as knowledge.

### 3.2 Severity follows trust

A check's severity in the gate comes from the claim's trust, so weak knowledge cannot block a build:

- `first-party` and `community-consensus` must-take and required-role claims are **blocking** in the gate.
- `single-author-opinion` and written claims raise **warnings** only.
- P-INTENT-002, 004 and 005 are warnings at every trust level; they are reported, not blocking.
- **Transferred claims** (5.4) keep their source trust, capped at `community-consensus`. They block only when three conditions hold:
  the source claim would block on its own archetype, the power mapping is `same` or `scale_only`, and the transfer test (5.5) has passed for that set's category
  and that source-to-target archetype pairing. Otherwise they warn. A claim transferred from a guide with no attributed archetype always warns.

The rule IDs, messages and fixes follow `error-output.md`; each diagnostic names the claim ID so the reader can see where the knowledge came from.

### 3.3 Record shape

One file per powerset family, `knowledge/coh/powersets/<category>/<set>.intent.json`, covering every archetype version of the set.
`versions` maps each archetype to its own internal powerset; every claim names the archetype version it applies to and powers by that version's internal names (through the #48 resolver).
A trimmed example, with one own claim for Brutes and the same claim transferred to Sentinels:

```json
{
  "schema_version": 1,
  "set": "Willpower",
  "versions": {"Brute": "Brute_Defense.Willpower", "Sentinel": "Sentinel_Defense.Willpower", "Tanker": "Tanker_Defense.Willpower"},
  "game_build": "Homecoming 2026.1.1242",
  "reconciled_through": "2026-10-20",
  "variants": [
    {"id": "default", "display_name": "Steady regeneration tank", "required_roles": ["mitigation_toggle"], "role_weights": {"mitigation_toggle": 3, "heal": 2}}
  ],
  "claims": [
    {
      "id": "willpower.brute.rise-to-the-challenge.priority",
      "at": "Brute",
      "provenance": "captured",
      "type": "power_priority",
      "power": "Brute_Defense.Willpower.Rise_to_the_Challenge",
      "variant": "default",
      "value": "must",
      "check": {"kind": "must_take", "power": "Brute_Defense.Willpower.Rise_to_the_Challenge"},
      "reason": "core_mechanic",
      "text": "Its regeneration scales with foes nearby; it is the set's engine and runs at all times.",
      "trust": "community-consensus",
      "status": "keep",
      "sources": [
        {"file": "pre-sunset/powersets/<slug>.md", "content_hash": "<16 hex>", "date_posted": "2009-03-02", "issue": "I14", "quote": "<verbatim words>"}
      ],
      "evidence": []
    },
    {
      "id": "willpower.sentinel.rise-to-the-challenge.priority",
      "at": "Sentinel",
      "provenance": "transferred",
      "transfer": {"from_claim": "willpower.brute.rise-to-the-challenge.priority", "from_at": "Brute", "mapping": "scale_only", "effects_diff": ["Regeneration scale"], "test": "armor:Brute->Sentinel"},
      "type": "power_priority",
      "power": "Sentinel_Defense.Willpower.Rise_to_the_Challenge",
      "variant": "default",
      "value": "must",
      "check": {"kind": "must_take", "power": "Sentinel_Defense.Willpower.Rise_to_the_Challenge"},
      "trust": "community-consensus",
      "status": "keep"
    }
  ]
}
```

Claim provenance is `captured`, `transferred`, `written` or `skeleton`. A transferred claim is never edited by hand: it is regenerated by code whenever its source claim,
the power mapping or the test result changes. An own captured claim for the same power and archetype replaces the transferred one.

`status` is `keep`, `correct` or `drop`. Only `keep` and `correct` claims produce checks. A dropped claim stays in the file with its evidence,
so the decision can be audited and is never re-extracted as new. `evidence` holds the patch record file and line that changed it, or the canonical field that contradicts it.
Archetype records (`knowledge/coh/archetypes/<at>.intent.json`) use the same shape with AT-level claims: the inherent mechanic, the intent axis inside the allowed categories
(for a Dominator, melee or ranged assault), and standing criteria (perma-Domination for every Dominator build, already a metric).

Every player powerset has a record from the start. Where no guide covers a version and nothing transfers to it, its claims are **skeleton** claims generated from canonical data
and the engine's power classifier: required roles the set can fill, no must-take claims. They load like any other claim and raise an info diagnostic naming the gap.

### 3.4 Where it lives

```text
knowledge/coh/
  manifest.json                    code-read: powerset or AT -> record file, content hash, schema version, game build
  schema/intent.schema.json        the record schema; CI validates every record
  archetypes/<at>.intent.json      AT record
  powersets/<category>/<set>.intent.json   one family, every archetype version
  transfer/mappings.json           code-generated power mapping between archetype versions, with effect differences
  transfer/test-results.json       the transfer test's measured results per category and archetype pairing (5.5)
  global-changes.json              the game-wide change catalog (section 6.2)
  _work/                           extraction work files: subject index, claim drafts, merge reports (not loaded by the engine)
docs/guides/
  archetypes/<at>.md               rendered for people from the record; marked generated
  powersets/<category>/<set>.md    rendered for people from the record; marked generated
```

There is no reading index. The manifest is the only index, and only code reads it: the loader resolves records through it,
and CI checks that every player powerset and archetype in `data/canonical/` has an entry whose hash matches its file.
Rendered guides live under `docs/guides/`, apart from the records, so nothing mistakes them for engine input.

`strategy/build_profiles.json` stays the goal axis (numeric targets from `breakpoints.json`). Intent is a second, independent axis.
A build is then AT + primary + secondary + pools and epic + goal profile + playstyle variant, and neither file restates the other.

### 3.5 Why this shape

- **Claims, not summaries.** A prose summary cannot be merged, dated against patches, or turned into a check. Atomic claims can be counted, contradicted, retired and run.
- **A check on every build-affecting claim.** What cannot fail cannot be shown to have changed a build; making the check mandatory is what turns knowledge into an effect.
- **Keyed by internal power names.** Patch records, canonical data, Mids dumps and the build writer all meet on power names; display names drift (issue #48's table).
- **One family record, claims scoped by archetype.** Versions of a set differ mostly in scale, so they share one file where transfer is visible claim by claim,
  while each claim still says which archetype it is true for. Separate files per version would hide the transfer; one undivided claim list would hide the differences.
- **Loaded by code.** Reading cannot be checked; loading in full by key can, and a missing or invalid record is a refusal, not a silent gap.
- **Alternatives rejected.** A reading index for sessions (unenforceable, section 2.9). Whole guides searched at run time (expensive, unreviewable, dates nothing).
  Intent inside `build_profiles.json` (mixes the per-set axis into the per-goal axis). Filling each capture's Summary (one guide is one opinion; the consensus lives above the captures).

## 4. The extraction method

Extraction is the only place a model reads guides. It runs once per powerset, under code checks, and its output is a record that code then loads and runs.

### 4.1 Stage 0: freeze the input

After the crawl ends, record the file list with `content_hash` as the extraction snapshot. Fold the duplicate hashes and URLs.
Every later stage cites `(file, content_hash)`, so a refreshed capture shows which claims to re-check.

### 4.2 Stage 1: mechanical, no model

- **Document type.** Guide, build post (Mids export plus a help or critique title), discussion (already tagged), theme. Build posts feed pick counts, not prose claims.
- **Subject.** Archetype from the folder or title; powersets from title combos (`Fire/Kin`, `Claws/SR`) and body counts of display names and aliases.
  Powers come from canonical display names, scoped to the subject sets; the #48 resolver keeps NPC copies of player powers out.
- **Era.** `date_posted` mapped to the live issue by the dated records in `data/patches/`; an Issue marker in the title overrides it.
- **Power snippets.** Guides with a bold heading per power name are split into per-power snippets, so the model reads a snippet rather than a whole guide.
- **Pick counts.** Parse each Mids forum export (`find_forum_export` already locates them in `scripts/forum_scrape/scrape.py`) into `(power, pick level, added slots)`.
  Do the same for the build archive's `.mxd`/`.mbd` files with the engine's parsers once they are restaged (#43 covers the single-line share blocks).
  Count, per (AT, powerset, power), how often it is taken and at what level, by era. If 9 of 10 i25 builds take a power, it is load-bearing, with no model involved.
- **Ranking.** Per powerset, rank guides by subject match, era (later first), length and structure, and author (one vote per author).

Output: `knowledge/coh/_work/guides.jsonl` (one row per guide) and `pick_counts.json`, both regenerated from the snapshot by tested code.

### 4.3 Stage 2: model reading, narrow

Per powerset, one packet: the top 6 to 8 ranked guides, plus the power snippets from every other guide on the set, plus the set's canonical power list and effects,
plus the list of registered check kinds. The model writes claims in the schema and nothing else. Code then enforces:

- Each claim's quote is a substring of the cited file, or the claim is rejected.
- Each power or set name resolves through #48, or the claim is rejected.
- Each build-affecting claim names a registered check kind with valid arguments, or it is demoted to a descriptive note.
- Numbers are stripped to intent, as in 3.1.
- No claim from training memory: a power the guides do not discuss gets no claim, and the gap is reported.

Rough size: about 130 primary and secondary sets with coverage, each packet near 20,000 tokens, so about 2.5 million tokens of reading in total, in batches a reviewer can follow.

### 4.4 Stage 3: merge into one consensus per powerset

- Group claims by `(set, archetype, power, type, variant)`. Count distinct authors for and against.
  Agreement across archetypes is recorded too: it feeds the transfer test, but a claim's own trust counts only guides on its own archetype.
- **Trust**, using the project's schema:
  - `first-party` only for developer statements: patch-note text (a set's introduction "Highlights" are design intent) and developer posts.
  - `community-consensus` when three or more distinct authors across two or more guides agree, nothing later contradicts it, and the pick counts do not argue against it
    (for example, a must-take power taken in fewer than a third of builds).
  - `single-author-opinion` otherwise.
- **Contradictions** are kept with both sides. A newer guide outranks an older one only after reconciliation (section 6) shows the game changed between them;
  otherwise a stable split becomes two playstyle variants rather than one winner.
- A capture's own `trust` field is not used: every pre-sunset capture carries the default, and consensus is a property of the claim, not the file.

### 4.5 Checks on the extraction

- Schema validation of every record, and the quote, name and check-kind checks above, in CI.
- **Sample audit:** a reviewer reads 10 random claims per set against their quotes; the target is 9 of 10 correct before the set's record replaces its skeleton.
- **Pick-count cross-check:** must and skip claims that disagree with what builds take are listed for review before they can be blocking.
- **Effect check:** run the set's new checks over the community builds for that set; a blocking check that fails most of them is suspect and goes back for review.
- **Repeatability:** re-run one packet and diff the claims; drift beyond wording means the prompt or the packet is underspecified.

## 5. The guide-writing framework

### 5.1 What it builds on

The repository has no general document framework beyond the Markdown style guide. It has three pieces this framework builds on:

- **The AT and powerset templates in issues #35 and #36.** They are the user's own framework, written for the Dominator onramp,
  and say to source from forum guides, not memory. Their memory file is missing, so the issue bodies are the record.
- **`community/CAPTURE.md`'s Summary rules:** lead with the conclusion, three to five bullets, preserve attribution.
- **The build rules:** B9 (trade-offs are real tensions; unverified specifics go in "What I would verify"), `io-set-naming.md` (full set names), `no-user-name.md` and the trust schema.

The templates are kept and extended, not replaced: their sections become the record's fields and the rendered guide's fixed order, and their "effects catalog" becomes generated output.
The framework is applied in the record; the rendered guide is its view for people and is never read back by a session or the engine.

### 5.2 Powerset guide sections, fixed order

Every rendered powerset guide, whatever its claims' provenance, has these sections in this order. A section with nothing to say states that, so two guides compare line by line.
A family record renders one guide per archetype version, so a Sentinel reader sees the Sentinel version with each transferred claim marked as such.

1. **Frontmatter:** set and archetype version (internal and display name), category, the claim count by provenance (captured, transferred, written, skeleton),
  the source archetypes of transferred claims, trust, game build, reconciled-through date, source count and era span.
2. **Summary:** three to five bullets, conclusions first.
3. **Identity and core mechanic:** what the set does and the powers that drive it.
4. **Powers:** one table row per power: role, priority per variant, one-line reason, effects (generated), pick level (generated), sources.
5. **Checks this set adds:** each check the record registers, its rule ID, severity, and the claim behind it. This is what the knowledge does to a build.
6. **Playstyle variants:** each named variant and what it emphasizes.
7. **Synergies:** with the other category's sets, pools and epics, stated in effects.
8. **Pitfalls.**
9. **AT differences:** the power mapping to the other versions, each archetype-specific power and its effect difference, and the inherent's effect on claims (5.4).
10. **Exemplar-10 notes:** which powers carry the set at low level.
11. **What changed since the guides:** the count of claims kept, corrected and dropped, with the deciding patch records.
12. **What would confirm this:** open questions and the concrete check for each.
13. **Sources:** every guide cited, with author, date and issue.

The AT guide follows the #35 template in the same way: identity and role, inherent mechanic and its build implication, allowed categories and the intent axis inside them,
structural specials, pool and epic options, caps (referenced from `data/canonical/archetypes/` and `breakpoints.json`, never restated), checks this AT adds, playstyle profile,
exemplar-10 notes, what changed, what would confirm this, sources.

### 5.3 Rendering

A small script renders each guide from its record: prose from claim text, the checks section from the registered check kinds, tables and effects from canonical data.
A correction belongs in the record, so the two cannot drift; the rendered file is marked generated, and CI fails when a rendered guide is out of date with its record.
Re-rendering after a reconciliation or a data refresh updates every guide the same way.

### 5.4 Filling gaps by transfer

Normally a powerset changes between archetypes only through the archetype's scaling numbers, so a guide on one archetype's version answers most questions about another's.
Transfer is the primary method for the 100 pairs guided on another archetype and the 21 pairs whose guides name no archetype (2.6).
It is also used power by power for unguided sets that reuse guided powers. It is deterministic code over extracted claims and game data; no model reads anything.

**Map the powers.** For each pair of versions, code pairs powers first by internal leaf name, then by tier position for the rest.
It then diffs their canonical effects, and each power pair gets one mapping class:

| Mapping | What differs | What happens to a claim |
| --- | --- | --- |
| `same` | nothing beyond the archetype's table column | carries over unchanged |
| `scale_only` | magnitudes only: damage, buff, debuff, heal or duration scale | carries over; any quantitative reason is re-asked of the engine for the target archetype |
| `shape` | targets, radius, arc, range or cast and recharge time | carries over with the AoE- or chain-dependent parts re-checked by the engine's classifier and chain report |
| `mechanic` | a different effect set: an added taunt, hide, a critical, a different mez, a different summon | does not carry over as a check; it becomes a note naming the difference, or is dropped when the mechanic is gone |
| `unmatched` | an archetype-specific power with no counterpart (a replacement or added power) | no transferred claim; the power gets a skeleton claim from its own effects and the engine's classifier |

The numbers always come from the engine. `attribmod.py` resolves every scale through the target archetype's column of the modifier tables,
so a `scale_only` claim is re-evaluated with the target's real values, never with the source's. Transfer moves intent, not numbers.

**Account for the inherent.** Each archetype's inherent can change what a claim means even when the power maps `same`. A small table in
`knowledge/coh/transfer/inherents.json`, checked by code against the archetype records, says which claim types each inherent touches and whether it keeps, re-checks or demotes them:

- **Stalker:** Hide, Assassin's Strike and criticals from hide favor single-target openers; AoE and taunt-dependent claims are demoted to notes, and the replaced powers map `unmatched`.
- **Brute:** Fury rewards a fast, steady attack rate; endurance and attack-chain claims are re-checked by the chain report with the Fury rule from `at_modifiers.py`.
- **Tanker:** Gauntlet's taunt and the Tanker's larger melee AoE targets raise AoE priority; AoE claims are re-checked rather than copied.
- **Scrapper:** criticals; damage-ranking claims are re-checked with the critical rule.
- **Blaster:** Defiance and the sustain powers in the secondaries; mitigation and sustain claims from other archetypes do not carry over.
- **Defender, Corruptor:** the same sets as primary or secondary at different scales, with Vigilance or Scourge; priorities carry over and quantitative reasons are re-asked.
- **Controller, Dominator:** Containment rewards control before damage; Domination's control and endurance effects are not modelled by the engine, so claims resting on them warn only.
- **Mastermind:** Supremacy and pets carry the damage, and support sets buff pets; personal-attack priorities from other archetypes do not carry over.
- **Sentinel:** Opportunity; armor sets differ more than scale on some powers, so the mapping decides power by power.
- **Kheldians and Arachnos:** their sets exist on one archetype each; transfer applies only through shared powers.

**Unguided sets that share powers.** For the 37 sets with no guide on any archetype, a power found by internal name in a guided set takes that power's claims by the same mapping rules.
Such claims always warn: the set around the power differs, so set-level roles and variants are not transferred.

**Provenance.** Every transferred claim records `from_claim`, `from_at`, the mapping class, the effect differences and the test result it relies on (3.3), and its trust follows 3.2.

### 5.5 The transfer test

Transfer is measured before it can block a build. The test runs once in Phase 8, and again whenever the extracted records change materially.

- **Test set:** every (set, archetype) pair that has its own guides, on a set guided on at least one other archetype. Weighted toward the pairs with the most guides:
  the 14 sets with three or more own guides on two or more archetypes (2.6), recounted after the crawl.
- **Method:** hide the pair's own guides, predict its claims by transfer from the set's other versions, and compare with the claims extracted from the hidden guides.
- **Claim agreement:** over the decision claims (must, strong, situational, skip per power, and required roles), the share of powers whose predicted priority equals the real one or is one step away.
  Separately, the count of opposite verdicts (predicted must where the guides say skip, or the reverse).
- **Build agreement,** using Phase 6's pick counts for the target archetype: the rank correlation between predicted priority and how often builds take each power,
  and the share of predicted must-take powers taken in at least 70% of that archetype's builds.
- **Baseline:** the same measures for a naive prediction (every power "strong"). Transfer has to beat it, or it adds nothing.
- **Pass bar, per category and source-to-target archetype pairing:** claim agreement of 80% or more, no opposite verdicts on would-be blocking claims,
  rank correlation of 0.6 or more, 70% or more of predicted must-take powers taken in 70% of builds, and better than the baseline on every measure.
  A pairing needs at least three test pairs to be judged; with fewer it is unjudged and its transferred claims warn.
- **On failure:** transferred claims for that category and pairing warn only and never block. When the failures concentrate in one claim type (for example AoE priority from Brutes to Stalkers),
  that type is added to the inherent table as demoted for the pairing and the test re-runs. When a set fails on its own while its category passes, transfer is turned off for that set,
  and its versions fall back to writing (5.6). Results are stored in `knowledge/coh/transfer/test-results.json` and rendered in each affected guide's "What would confirm this".

### 5.6 Writing from scratch, the fallback

For sets with no guided version on any archetype and too few shared powers (2.6), and for the two Issue 28 Page 4 sets, the skeleton claims are upgraded to written ones. Inputs, in trust order:

1. **Canonical data:** each power's help text, effects, targets, recharge and pick level. This settles what each power does.
2. **The engine's numbers:** the chain classifier's delivery, shape and role per power, chain DPS, endurance budget, and totals for a draft build.
3. **First-party design intent:** the patch record that introduced or revamped the set (Issue 28 Page 4's "Highlights" for Light Affinity are an example).
4. **Pick counts** from i25 and later archive builds for the set, where they exist (Water Blast, Staff Fighting, Beam Rifle and others; see 2.5).
5. **Similar sets:** the closest sets in the same category by effect profile, which the effects catalog makes measurable.

Rules for written claims:

- `provenance: written`, and the rendered guide opens with "Written from game data and similar sets, not captured from a guide."
- Trust is `single-author-opinion` at most, so their checks warn and never block (3.2); a claim taken from patch-note intent keeps `first-party` for that claim only.
  Whether written claims deserve a fourth trust value is an open question (section 9).
- Every claim lists its basis (`canonical`, `engine`, `patch`, `pick-counts`, `similar-set:<name>`), never a guide quote.
- "What would confirm this" is mandatory and concrete: a captured guide that agrees, pick counts above a threshold, a Mids harness run meeting the profile, or combat-log results (#62, #63).
- Once one archetype version of a written set has a captured guide, the other versions take transfer from it instead, through the same test.

The Light Affinity and new armor records wait for the data refresh (#20); until then they cannot have even skeleton claims, because the sets are not in the data.

## 6. The one-time update to the current game

### 6.1 Date every claim

A claim's date is its source guide's `date_posted`, mapped to the issue live at that time (title markers win). Era bands used for bulk decisions:
before Enhancement Diversification, before inventions, before inherent Fitness, late live game, Homecoming. The boundaries come from the patch records' own dates, not memory.

### 6.2 Find what changed since

Two searches, both mechanical:

- **Per power and set.** Run the #48 resolver over all 23,292 patch lines to tag the 93% that lack a powerset field (and fix the heading carry-over errors).
  For each claim, list the change lines dated after its guide that name its power or set.
- **Game-wide.** A short hand-curated catalog, `knowledge/coh/global-changes.json`, of changes that retire whole classes of advice.
  Each entry cites its patch record and names the claim types it affects.
  Candidates to confirm against the records: Enhancement Diversification, the invention system, inherent Fitness, travel power prerequisite changes,
  proliferation waves, Homecoming's archetype changes (Sentinels, raised damage caps), and enhancement and set changes in the 2026 updates.
  Example: any "take Stamina by level 20" claim is dropped once Fitness is inherent.

### 6.3 Reconcile: keep, correct or drop

- **No later change, no game-wide hit:** keep, marked "no recorded change since" the claim's issue.
- **A later change or a game-wide hit:** the model reads the claim, the change lines and the current canonical values, and returns keep, correct (with the new wording) or drop, citing the line.
  The same quote and name checks apply to its output.
- **Mechanical drops:** a claim naming a power that no longer exists (after rename resolution), or a claim that is only a number.
- **Engine checks:** a claim whose reason is quantitative ("it can be made permanent") is checked by the engine for a draft build, not by the model.

Each verdict writes its evidence into the claim, and only kept and corrected claims produce checks. The update runs once, over the whole knowledge base, after the data refresh.

### 6.4 Target build, and what is missing

- **Pick the target.** The engine's data trails the live game: client data from 2025-12-09, Mids database 2026.1.1242 (dumped July 2026), patches through 2026-10-20.
  Reconcile against the build the engine computes on, after #20 refreshes it to include Issue 28 Page 4. Stamp `game_build` and `reconciled_through` on every record,
  and keep a list of patches newer than the data so a claim is never "correct" for a game the engine cannot compute.
  The loader refuses a record whose `game_build` differs from the engine's database (E83 below), so a data refresh forces a re-check rather than silent drift.
- **Regenerate `current_overrides.json` from `data/patches/`.** The PowerShell-built file stops at Issue 28 Page 3 (November 2025).
- **Missing: the 2012 to 2019 gap.** No source records what changed between the sunset and Homecoming's relaunch.
  Mitigation: compare each claim with current canonical data directly (a power whose effects changed shows it), and weight live-era guides and i25 builds higher where they disagree with old guides.
- **Missing: wiki lines carry no powerset field.** The resolver tagging in 6.2 fills it; its accuracy needs a hand-checked sample.
- **Afterwards:** each new patch record runs the per-power search for new hits only, so the knowledge stays current without repeating the one-time pass.

## 7. Engine integration

### 7.1 The loader: code chooses the knowledge

`coh_engine/knowledge.py` takes the build's archetype, primary, secondary, pools and epic (all read from the build itself) and the playstyle choice,
and returns a frozen `PlaystyleProfile`: every matching record from the manifest, loaded in full and schema-checked, with the chosen variant applied.
From each family record it takes every claim for the build's own archetype version: captured, transferred, written and skeleton alike, each with its own severity.
Transferred claims and their test results are already materialized in the record by code, so the loader makes no transfer decisions at build time.

- The playstyle choice is the only input a session gives: a variant ID per powerset (or `default`) and a goal profile from `build_profiles.json`.
- The session never names files, records or claims. Which knowledge applies is decided by the build's own powersets.
- Refusals, following `error-output.md`: E82 when a powerset or archetype has no manifest entry; E83 when a record fails its schema, its hash, or its `game_build` check;
  E84 when the playstyle names a variant the record does not define.

### 7.2 The checks: a new registry, per check-registry.md

- **Intent registry**, `coh_engine/intent.py`: a frozen `IntentContext` (the resolved build powers and slots, the `PlaystyleProfile`, the chain report) and `@intent_check(kind)` functions,
  one per check kind (`must_take`, `skip`, `required_role`, each pitfall form, each slotting-intent form). The runner folds every claim's check over the context and contains no per-kind logic.
  Adding a check kind is one decorated function; adding a claim is data.
- **Rule IDs:** P-INTENT-001 must-take missing, P-INTENT-002 skip taken, P-INTENT-003 required role unfilled, P-INTENT-004 pitfall present, P-INTENT-005 slotting intent unmet,
  P-INTENT-000 (info) skeleton, transferred or written claims in use. Severity follows trust (3.2).
- **Metrics** in the existing scoring registry: `intent_alignment` (priority and synergy weights summed over picked powers, as a fraction of the best possible) and `role_coverage`.
- **Rules the rule files already promise:** the exemplar-10 gate (travel by 12, two attacks by 15, two added slots in a defensive toggle by 15, the anchor attack's slots by 14)
  and accuracy-first attack slotting (P-SLOT-001). They are independent of the guides and land with the intent registry.
- **Declared archetype and AT powerset legality** from #35, so the loader keys on the build's own archetype.
- **`validate`** runs legality, hard limits and the exemplar-10 gate. **`score`** (new subcommand) runs the metric, adherence and intent registries for the playstyle.

No dispatcher changes: each item is a new registered function or a new metric name. The numeric port stays a literal port.

### 7.3 The gate: no build skips the knowledge

- **The writer gates itself.** `write_mbd` takes the playstyle choice alongside the spec. Before it returns anything it runs `validate` and the playstyle `score`
  and raises `BuildSpecError` on failure, exactly as it raises E46 to E49 today: E80 for any `validate` error, E81 for any blocking `score` diagnostic,
  and E82 to E84 from the loader. There is no flag that turns the gate off.
- **Waivers are explicit and visible.** A spec may waive one diagnostic by rule ID with a reason, using the `noqa: <rule-id> -- <reason>` convention in `error-output.md`.
  Hard-limit and legality errors cannot be waived. Waivers are written into the build's sidecar and listed in the CI report.
- **The sidecar.** Every emitted build gets `<build>.playstyle.json`: the playstyle choice, the record hashes it was checked against, the diagnostics, and any waivers.
  The forum-export Markdown (B6) is rendered from the same written build, so it cannot exist without passing the gate.
- **CI.** A job runs the same `validate` and `score` over every committed build under `samples/builds/generated/` and `samples/builds/operator-builds/` against its sidecar,
  and fails on a missing sidecar, a stale record hash, or a blocking diagnostic. Captured community builds are source material, not output: CI scores them in report mode only.
- **Dependency.** `validate` today reads a Mids dump directory. The gate needs the engine to evaluate a spec or `.mbd` directly (#45), or to call the dump harness in the loop.
  Until then the gate can be built and tested against harness dumps, and CI runs it where the harness is available.

### 7.4 Where the optimizer fits

The open optimizer issue (#21) is scoped to slotting. A build has two decisions, and the plan splits them:

1. **Power plan:** the 24 picks and their levels, driven by the playstyle profile: must-take first, then required roles early enough for the exemplar-10 gate.
2. **Pick constraints:** the hard-limits rules enforce pool prerequisites and the four-pool limit; a greedy fill with local swaps against `intent_alignment` is likely enough.
3. **Slotting:** the #21 search over the 67 slots and enhancements, against the goal profile and caps, with legality as a hard constraint and slotting intent as a filter.

The objective follows the order of authority in section 1. Whatever the search returns still goes through the writer's gate; the optimizer is a proposer, not a bypass.

### 7.5 Replacing navigation-by-reading

- Mark `.claude/rules/coh-knowledge-navigation.md` for replacement by a short rule: builds come only through the `.mbd` writer, and a build is emitted only when it passes
  `validate` and the playstyle `score`. A session states the playstyle; it does not hand-write build JSON or forum exports, and it does not choose knowledge files.
- Update the references that point at the old rule: `CLAUDE.md` reading-order item 9 and the "Workflow for emitting a build" steps, and the pre-flight checklist in
  `build-creation.md`, whose data and exemplar items become the checks `validate` and `score` run.
- `knowledge/coh/INDEX.md` is not built. The manifest (3.4) is the only index, and only code reads it.

### 7.6 Verification in MidsReborn

1. The writer emits the build level by level (#44) after its gate passes.
2. The Mids dump harness (`tools/mids-dump`) reloads it and dumps totals; the engine's totals must match within the parity tolerance (`test_mids_parity.py`).
3. `score` runs on the Mids totals as well as the port's: the build "performs" when Mids' own numbers meet the goal targets.
4. Later, combat logs (#62, #63) confirm claims about real play and can raise or lower a claim's trust.

## 8. Phased plan

| Phase | Deliverable | Depends on | Size | Issues |
| --- | --- | --- | --- | --- |
| 0 | Name resolver plus a tested guide subject index and coverage report (`_work/guides.jsonl`, gap list) | crawl finished | 1 PR, small | reuse #48; file "guide subject index" |
| 1 | Intent schema with check kinds and archetype-scoped claims, manifest, loader (E82 to E84), skeleton claims for every player powerset version and AT, renderer | 0 | 1 to 2 PRs, medium | file "intent schema, manifest and loader" |
| 2 | Intent registry and metrics, exemplar-10 and P-SLOT-001 rules, declared archetype, `score` subcommand | 1; #35 | 2 PRs, medium | file "intent checks", "exemplar-10 and accuracy-first rules"; reuse #35 |
| 3 | The gate: writer refuses (E80, E81), waivers, sidecar, CI job on committed builds; navigation rule replaced | 2; #44, #45 | 1 to 2 PRs, medium | file "gate the writer on validate and score" |
| 4 | Pilots end to end: Willpower on Brutes (captured) carried to Sentinels (transferred, warning only until tested), and Arsenal Control (written), all checked in the gate | 1, 2, 3 | 1 PR, medium | reuse #36 for Arsenal Control |
| 5 | Patch-line tagging over all records, date-to-issue table, global-change catalog, `current_overrides.json` regenerated from `data/patches/` | 0 | 1 PR, medium | file "tag patch lines and regenerate overrides"; relates to #20 |
| 6 | Pick counts from forum exports and the restaged build archive | 0; #43 | 1 PR, medium | file "pick counts from community builds" |
| 7 | Extraction, merge and reconciliation for every covered set, in audited batches | 4, 5, 6; #20 for the reconcile target | many small PRs, large | file one tracking issue with a checklist per category |
| 8 | Transfer: power mapping and effect diff between versions, inherent table, transferred claims; the transfer test run and its results stored | 4, 6, 7 | 2 PRs, medium | file "transfer claims between archetype versions", "measure transfer" |
| 9 | Written claims for sets with no guided version and too few shared powers, including the Issue 28 Page 4 sets; sets that failed transfer | 4, 6, 8; #20 | batches, medium | file "written records for gap sets"; reuse #35, #37 |
| 10 | Power plan search and slotting optimizer proposing builds through the gate | 3; #21; #31 and #40 for damage and procs | 2 PRs, large | widen #21 or file "power plan search" |

**Start next: Phase 0.** It needs no model, writes nothing under `community/`, and every later phase stands on its output.
It can start the day the crawl finishes; the resolver half (#48) can start now. Phases 1 to 3 put the gate in place before any extracted knowledge exists,
so the first real record changes builds the day it lands.

## 9. Risks and open questions

### 9.1 Risks

- **The crawl changes the picture.** Counts here come from a partial crawl ordered Blasters, Brutes, guides; the gap list may shrink. Phase 0 recounts.
- **Most archetype captures are build posts.** Prose claims will come from a few hundred real guides, not 1,400; the build exports carry the rest as pick counts.
- **Model extraction errors.** Mitigated by quote, name and check-kind checks, sample audits, pick-count and effect cross-checks; not eliminated.
  Trust-scaled severity keeps a wrong single-author claim from blocking builds.
- **A wrong blocking check.** A consensus claim that is wrong would refuse good builds. The effect check (4.5) and waivers limit the damage; the fix is in the record.
- **Checks only cover what has a check kind.** Advice with no machine-checkable form stays descriptive and does not steer builds; new kinds are added as needed.
- **The gate depends on evaluating a spec.** Until #45 or a harness-in-the-loop path exists, the gate runs only where the harness runs.
- **Old advice that looks current.** The 2012 to 2019 gap has no patch records; direct comparison with canonical data catches changed powers, not changed play.
- **Patch tagging errors** (wiki lines untagged, heading carry-over) feed reconciliation; the tagger needs its own checked sample.
- **The data lags the game.** Until #20 lands, the two newest sets and the 2026 cap changes cannot be computed, and records target an old build.
- **Intent against numbers.** A playstyle can demand a power the numbers rate weak. The order of authority settles it, but the user should confirm that order.
- **Transfer carries a wrong idea further.** A mistaken source claim spreads to every version. Transferred claims regenerate from their source, so one fix corrects them all.
- **A small test set.** At this snapshot only 14 sets have three or more own guides on two archetypes, almost all with Brutes or Blasters as one side.
  Many category and pairing cells will be unjudged, and their transferred claims will warn, until the crawl adds the other archetype forums.
- **Inherents the engine does not model.** Hide, Gauntlet's taunt and Domination's control effects are not in the engine, so claims resting on them can only be demoted, not checked.
- **Mapping by position can mislead** when two versions order their powers differently; the effect diff catches most of it, and `mechanic` mappings never transfer a check.

### 9.2 Questions for the user

1. **Use of forum text.** The forum's signal is `ai-train=no, use=reference`. Is model reading of captured guides to write attributed, quoted claims inside reference use as you read it?
2. **Reconcile target.** Reconcile against the engine's data build after #20 refreshes it (recommended), or against today's patch notes even where the engine cannot compute them?
3. **Trust for written claims.** Reuse `single-author-opinion` with `provenance: written` (recommended), or add a fourth trust value?
4. **The build archive.** `builds_archive_index.json` points at `scratch/build-archives`, which is not in the checkout. Where are the 7,922 files, and may they be restaged?
5. **Archive copies.** The index lists many folders twice (`Mids Archives - Part 1/...` and `Mids Builds Archives 1905 FX/Mids Archives/...`). Fold them by content hash?
6. **The missing framework memory.** Issues #35 and #36 cite `project_at_powerset_framework.md`, which does not exist. Are the issue templates the whole framework?
7. **Order of authority.** Confirm: hard limits, exemplar-10 gate, goal targets, intent, numbers.
8. **Blocking threshold.** Is "consensus or first-party must-take and required-role claims block; everything else warns" the right line?
9. **Waivers.** Should any intent diagnostic be waivable, or should a disagreement always go back into the record?
10. **Dominator onramp sequencing.** #35 to #37 are marked post-CP10. Should the Arsenal pilot in Phase 4 wait for that?
11. **Optimizer scope.** Widen #21 to include the power plan, or file the power plan separately?
12. **Pools and epics.** Not measured here. Should they get records in the first pass, or skeletons until after the primaries and secondaries?
13. **Transfer pass bar.** Are 80% claim agreement, no opposite verdicts, rank correlation 0.6 and the 70% must-take build share the right bar, and is three test pairs enough to judge a pairing?
14. **Transferred trust.** Keep source trust capped at `community-consensus` and block only after the test passes (recommended), or never let transferred claims block?
15. **Power-level transfer across sets.** Should claims move between differently named sets that share a power (Dark Blast to Dark Assault, for example), or only between versions of one set?
