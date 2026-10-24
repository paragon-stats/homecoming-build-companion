# Mids Reborn 4.0 alignment: what comes from game data and what Mids keeps built in

The engine's next refactor moves its inputs from MidsReborn's database and dump harness to game data.
This page records where Mids Reborn 4.0 draws that same line, so the engine stays compatible with the parts Mids still builds into the application and sources the rest the way Mids does.

Mids 4.0 is unreleased. Everything here is read from the source branch `4.0` of `LoadedCamel/MidsReborn` at commit `24ee176f4` (2026-08-21).
File references are paths under `MidsReborn/Core/` on that commit unless another folder is named.
There is no 4.0 release, prerelease or CI artifact, and the tool that produces its import data is not in the repository, so none of this is a claim about shipped behaviour.

## The shape of 4.0

4.0 does not build a database from game data alone. Its `Omni` importer merges an export folder into an existing Mids database:

- Powers are matched by canonical name and created only when no match exists (`Omni/OmniImporter.cs:1491-1546`).
- Existing `NeverAutoUpdate` flags on Mids records are honoured (`Omni/OmniImporter.cs:1565-1602`).
- An import is refused until a dry run of the same folder has been done (`Omni/OmniImporter.cs:1387-1390`).
- Homecoming build files are rewritten on every load through a hand-maintained `LegacyLoadMap.mhd` (`Compatibility/CompatibilityBuildLoader.cs:37-87`).

The calculation core is rewritten. `clsToonX.cs` is deleted.
`Toon.GenerateBuffedPowerArray` (`Toon.cs:352-397`) now runs a `PlannerPowerPipeline` whose phases keep the `GBPA_*` pass names (`PlannerPowerPipeline.cs:206-1387`).
Totals come from `ActorTotalsCalculator.Calculate` (`ActorTotalsCalculator.cs:20`) instead of the `GBD_*` methods.
The engine's literal port in `base_totals.py` mirrors 3.x; when 4.0 ships, parity goldens re-baseline against this pipeline.

## The export is a City of Data v3 raw export

The import reads a tree of snake_case JSON. Its field names bind one for one with the files in `data/canonical/`:

- Power fields in `Omni/OmniDtos.cs:51-390` (`full_name`, `powerset`, `effects`, `redirect`, `modes_required`, `allowed_boostset_cats`, …) match every canonical power file.
- Effect groups and templates (`requires_expression`, `is_pvp`, `ppm`, `chance`, `templates`, `child_effects`, `attribs`, `aspect`, `table`, `scale`, `tick_chance`) match `Omni/OmniDtos.cs:461-605`.
- Powerset `index.json` matches `OmniPowersetDefinition` (`Omni/OmniDtos.cs:393-431`).
- Class attributes are read from `tables/` and the `named_tables`, `attrib_base`, `attrib_max`, `attrib_max_max`, `attrib_strength_max` and `attrib_resistance_max` keys.
  Those are the keys the canonical archetype files carry (`Omni/OmniImporter.cs:8183-8296, 10637-10644`).

So the game-data source Mids 4.0 builds on is the one this repository already holds. The gap is generation, not format.
`data/canonical/` is revision `homecoming-20251209_7415`; the installed client is build `20261017_8623`.
4.0 also expects a newer export layout than ours in three places:

| 4.0 expects | This repository has |
| --- | --- |
| `enhancements/`, `enhancement_sets/` with `canonical_id` and `storage_key` | `boost_sets/` keyed by `name` |
| `enhancement_set_groups.json`, `enhancement_diversification.json`, `enhancement_effectiveness.json`, `enhancement_exemplar_scaling.json`, `gcm.json`, `progression_schedules.json` | `boostset_groups.json`; none of the others |
| entities with `class_id`, `pet_commandability`, `power_ids`, `power_references` | entities with `defaults.character_class_name`, `commandable_pet`, `defaults.power_full_names` |

Only `progression_schedules.json` is hard-required by the importer (`Omni/OmniImporter.Progression.cs:50-98`).
The enhancement tables are optional; without them 4.0 falls back to its legacy ED and effectiveness curves (`PlannerRulesets/EnhancementMathPolicy.cs:78-83`).
Class modifier tables are mandatory at runtime: "Legacy AttribMods are no longer supported" (`DatabaseAPI.cs:135-146`).

## Sourced from game data

These are the inputs the engine reads from the export or the client, never from Mids:

| Domain | Export source | Lands in | Reference |
| --- | --- | --- | --- |
| Archetype attribute tables and caps | `archetypes/*.json` `attrib_*` keys, sampled at level 50 for the summary fields | `ClassAttributes`, `Archetype` | `Omni/OmniImporter.cs:10621-10671` |
| Modifier (scale) tables | `tables/*.json` `named_tables` | `ClassAttributes[c].NamedTables`, the only modifier source at runtime | `Omni/OmniImporter.cs:8177-8296`; `DatabaseAPI.cs:148-172` |
| Powers, powersets, requirements, modes, redirects | `powers/**` | `Power`, `Powersets`, `AdvancedRequirements`, `PowerRedirect` effects | `Omni/OmniImporter.cs:4518-4589, 1623-1678` |
| Entities and tags | `entities/*.json`, `tags/*.json` | `SummonedEntity` with `ClassName` from `class_id` | `Omni/OmniImporter.cs:1836-1876`; `Omni/OmniImporter.RuntimeMetadata.cs:180-268` |
| Progression schedule | `progression_schedules.json` | `BuildProgressionMetadata` | `Omni/OmniImporter.Progression.cs:48-123` |
| ED, effectiveness, exemplar scaling | the three enhancement JSON tables | `EnhancementMathPolicy` | `PlannerRulesets/EnhancementMathPolicy.cs:62-83, 207-336` |
| Enhancements, sets, set bonuses, recipes, salvage, conversions | `enhancements/`, `enhancement_sets/`, `set_bonuses.json`, `recipes/`, `salvage/`, `set_conversions.json` | the enhancement database | `Omni/OmniImporter.Enhancements.cs:18-41, 185-203` |
| Mode list | `attribute_names.json` `pp_mode[]`, index is the mode id | `OmniModeMapper.ModeIds` | `Omni/OmniModeMapper.cs:86-128` |
| Proc-chance tags | `gcm.json` | `EffectIds` | `Omni/OmniImporter.cs:9156-9215` |

## Still built into Mids

Two kinds, treated differently.

### Game rules the data lacks

The engine carries equivalents as named constants with these citations, so a divergence is a diff rather than a discovery:

- Proc chance: minimum `0.015 × PPM + 0.05`, maximum `0.9`, an area factor, and click versus toggle periods (`PlannerRulesets/PlannerRulesets.cs:591-647`).
- PvP defence diminishing returns constant `1.2` (`PlannerRulesets/PlannerRulesets.cs:659-670`).
- Final caps come from the class tables, falling back to the archetype field and then to defaults such as recharge 5 and resistance 0.9 (`PlannerRulesets/PlannerRulesets.cs:672-708`).
- Archetype inherents: a 27-entry catalog keyed by provider, class and power (`Omni/ArchetypeInherentCatalog.cs:66-354`).
  It covers Fury, Containment, Scourge, Defiance, Domination, Gauntlet, Vigilance, Opportunity, Assassination, Critical Hit, Cosmic Balance, Dark Sustenance, Supremacy and Conditioning.
  It rewrites game expressions such as `source>kMeter`, `target>kHeld` and `Mode?(Domination)` into planner controls, because the data has the expressions but not combat state.
  This is the compatibility reference for `at_modifiers.py`.
- Enhancement math fallbacks when the imported tables are absent (`PlannerRulesets/EnhancementMathPolicy.cs:111-116, 362-444`).
  These are Superior ×1.25, the legacy ED curve (0.9, 0.7, 0.15), and the legacy effectiveness of +5% or −10% per level.
- Mez protection versus mez resistance is a heuristic on the power's description text (`Omni/OmniMidsMapper.cs:1212-1223`). Known, not copied.

### Mids planner preference

The engine may ignore these, and must not let them leak into data:

- Planner-only states: combo levels, Staff Perfection, Blood Frenzy, Engagement, Fast_Mode (`Omni/PlannerStateCatalog.cs:95-205`).
- Import scope lists: whitelisted non-playable classes, excluded powers and powersets, included power roots (`Omni/OmniImportScope.cs:5-140`).
- About 60 power-name aliases such as `Shock_Therapy` → `Electrical_Affinity` (`Omni/OmniImporter.cs:97-162`).
- Default stat inclusion, inherent grid order, accolades forced to level 49 (`PowerEntry.cs:20-23, 105-112, 266-298`).
- `Class_Minion_Pets` is the class given to an entity whose `class_id` is blank (`Omni/OmniImporter.cs:50`). Every canonical entity carries a class, so the engine never needs that name.
  The pet classes Mids 3.x lacked, `minion_controllerpets` and `boss_praetoriangrunt_pet`, are present in `data/canonical/tables/`.

## Transform rules the engine aligns with

How 4.0 reads the same JSON the engine reads:

- Every template attrib becomes one effect row; child groups inherit the parent's tags, requires expressions and PvP flag (`Omni/OmniMidsMapper.cs:118-351`).
- Effect probability is the product of the group `chance` values and the template `tick_chance`, clamped; `ppm` comes from the group (`Omni/OmniMidsMapper.cs:134, 241-245`).
- A blank modifier `table` means `Melee_Ones`; a missing class or table evaluates to 1.0 (`Omni/OmniMidsMapper.cs:212-214`; `DatabaseAPI.cs:157-171`).
- `GlobalChanceMod` and `PowerChanceMod` templates become one chance-mod effect per filter tag; the scope is power-local only for the `Power_Chance_Mod` forms (`Omni/OmniMidsMapper.cs:299-327, 498-512`).
- Redirect kind: any destination with `EntCreate` is a summon delivery; all destinations offensive is an execution variant; any destination with modes is a mode variant; otherwise a buff variant (`Omni/OmniPowerClassifier.cs:553-604`).
- Pet versus pseudo-pet, in order (`Omni/OmniBuildActor.cs:69-150`): a name or `ai_config` containing one of 15 fragments (patch, trap, mine, …) with no commandability is a pseudo-pet.
  Then `pet_commandability > 0` is a pet; a class containing Pet or Henchman with powers is a pet; otherwise a pseudo-pet.
- Power type: `type` maps auto, toggle, boost, inspiration and globalboost; anything else is Click (`Omni/OmniPowerClassifier.cs:457-468`).
- Mode names drop a leading `k` and pass through an alias table; power-level `modes_required` and `modes_disallowed` become flags (`Omni/OmniModeMapper.cs:29-74`; `Omni/OmniImporter.cs:4542-4543`).
- Target routing parses `target_requires` into player, owned pet, self and friend/foe clauses; anything else is deferred (`Omni/PlannerTargetRoutingPolicy.cs:123-158, 221-406`).

## Build-file compatibility

`.mbd` stays `CharacterBuildData` JSON (`BuildFile/BuildManager.cs:29-83`):

- Enhancement `Uid` is unchanged, and the older `Enhancement` display-name key is still accepted as a fallback (`BuildFile/EnhancementDataConverter.cs`).
- `PowerSets` is a list of full names; on-disk levels stay 1-based (`Level + 1` on save, `Level - 1` on load).
- New fields are additive and optional: `MetaData.DatabaseProvider`, `SlotData.SlotSource` and `GrantedRuleId`, `CombatContext`.
- A Homecoming build is rewritten on load through `LegacyLoadMap.mhd`; the load fails when a powerset cannot be mapped or the map file is missing (`Compatibility/CompatibilityBuildLoader.cs:292-297`).
- `.mxd` goes through `TryLoadLegacyMxd` first, then the old hex and zlib path.

This repository's `.mbd` artifacts remain valid inputs. The engine resolves them against game data by name and does not depend on the rewrite.

## What the engine changes

In order, each its own issue and pull request, after the chain-planner branch (#67, #68, #69) lands:

1. Read modifier tables from `data/canonical/tables/<class>.json` instead of Mids `AttribMod.json`, and caps from `archetypes/*.json`.
   This retires `--mids-db`, the pet-class data gaps, and any `Class_Minion_Pets` fallback.
2. Read the level schedule, ED and exemplar scaling from the export when the current generation is in hand; until then the harness values stand, labelled as such.
3. Refresh `data/canonical/` to the client's build, from a current City of Data export or by decoding the client's `bin.pigg` and `bin_powers.pigg` directly.
   The `.pigg` container is simple; the `Parse7` payload is schema-driven and is the real cost of the direct route.
4. Resolve `.mbd` builds against game data by name, so `validate` and `chain` run without the dump harness. The harness stays as the parity golden.
5. Carry the hard-coded game rules above as named constants with their 4.0 citations.

## Reference

- [`mids-port-spec.md`](./mids-port-spec.md): the 3.x port specification and its fidelity boundary.
- [`chain-planner.md`](./chain-planner.md): the planner this branch delivered, and the data gaps that motivated the move.
- [`../canonical-data-shape.md`](../canonical-data-shape.md): the canonical export's field reference.
- [`../../.claude/rules/check-registry.md`](../../.claude/rules/check-registry.md): the fidelity boundary rule that the rewritten core affects.
