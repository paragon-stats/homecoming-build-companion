# Canonical data shape

Canonical data is the City of Data v3 JSON export under `data/canonical/`, recorded in `data/canonical/manifest.json`.

## Power file naming

A power file lives at `data/canonical/powers/<powerset path>/<power>.json`.
Each dot-separated segment of the power's `full_name` becomes one lower-cased path segment.
Every `:` in a segment becomes `_`.
For example, `Training_Gadgets.Training_and_Gadgets.Tactical_Training:_Assault` is stored at `powers/training_gadgets/training_and_gadgets/tactical_training__assault.json`.

`coh_engine.attack_profile.canonical_power_path` is the single source of this rule.
`scripts/lane-b/run.ps1` and `scripts/check-overrides.ps1` apply the same mapping.

The zip distribution names these files with a literal `:`.
On NTFS, `name:stream` is an alternate data stream, so extracting them left empty files and the real JSON never reached the repository.
The exporter now renames them on extraction, and `src/coh_engine/tests/test_canonical_files.py` fails on any zero-byte canonical file.

## Supplementary powersets

Light Affinity and Sonic Aura shipped in Issue 28 Page 4, after the City of Data dump in the manifest, so the dump has no copy of either.
Their files come from the CoH Sidekick export instead, recorded under `supplements` in `data/canonical/manifest.json`.
Sidekick's bin-crawler decoded the same client bins this copy was checked against (`bin.pigg`, `bin_powers.pigg` and `bin_fxinfo.pigg` hash-match).

| Set | Player folders | Linked powers |
| --- | --- | --- |
| Light Affinity | `powers/{controller,corruptor,defender,mastermind}_buff/light_affinity/` | `powers/redirects/light_affinity/` |
| Sonic Aura | `powers/{brute,scrapper,sentinel,stalker,tanker}_defense/sonic_aura/` | `powers/redirects/sonic_aura/`, `powers/pets/sonic_aura/` |

Each category `index.json`, `all_power_search.json` and `at_power_search.json` list them like any other set.
`src/coh_engine/tests/test_canonical_files.py` fails if a supplemented set drops out of the player powersets or links to a power with no file.

### What the conversion changed

Sidekick's JSON differs from City of Data's, so these fields were rewritten to City of Data's form:

- `requires` and `requires_expression` are postfix token lists in Sidekick and infix strings here, for example `target>enttype eq 'critter'`.
  City of Data also normalizes them, writing `!(a eq b)` as `Ne(a, b)` and reordering clauses.
  So a token list both exports hold is looked up rather than rebuilt; only the simple forms, such as `Source.Mode?(kChain_Jolt_Mode)`, are rebuilt.
- `allowed_set_categories`, `num_allowed` and `priority_list` are renamed to `allowed_boostset_cats`, `number_allowed` and `priorities`.
- A null `allowed_set_categories` becomes `[]`, which is how City of Data writes a power that takes no sets.
- `effect_area` `Sphere` becomes `AoE`; `target_type` and `boosts_allowed` take City of Data's display names, such as `Foe (Alive)` and `Enhance Heal`.
- Each set `index.json` is rebuilt with City of Data's keys (`name`, `display_help`, `power_names` and the rest).

Each value mapping was read off the 1,174 power files both exports contain, and the expression rewrite reproduces every Darkness Affinity expression in this repository.

### What it did not change

- `flags` and the suppression `event` names keep Sidekick's spelling, because the two exports pair them ambiguously and nothing in the engine reads them.
- City of Data fields Sidekick does not export are absent, among them `magnitude_expression`, `max_boosts`, `boost_info` and the FX blocks.
- Sidekick-only fields such as `free`, `flags_raw` and `requires_pv` are kept.

### Where the client differs from the patch notes

The values are from the 2026-10-17 client, which is newer than the Issue 28 Page 4 notes (2026-10-06).
Where they differ from the notes, the client values are the ones stored.

Light Affinity:

- Magnify's damage buff is scale 3.0; the notes say 2.
- Searing Beam's area -Res is scale -2.5; the notes say -2.
- Searing Beam has no main-target 0.7 Fire hit and no -0.5 -Special; the notes list both.

Sonic Aura, the same for every archetype:

- Noise Cancellation gives scale 8.0 Defense to all types and 2.5 Resistance to all; the notes say 9.5, with 1.0 for Toxic and Psionic.
- Noise Cancellation heals scale 0.5 every 2 seconds for 15 seconds; the notes say 15 ticks of 0.25.
- Sonic Boom's range is 200 ft; the notes say 60 ft.
- Disruption Aura has no taunt of its own. Brutes and Tankers get Mag 4 Taunt from the Fury and Gauntlet proc auras it links to; the notes say Mag 3. Scrappers get none, though the notes list them.

A later City of Data dump that includes these sets replaces their files when `scripts/lane-b/run.ps1` re-extracts.
