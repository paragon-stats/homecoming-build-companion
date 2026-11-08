# .NET port of the build engine

The C# engine in `CohEngine.slnx` is a port of the Python engine in `src/coh_engine/`.
It is checked against the same Mids reference dumps, value for value.
This page covers its layout, the single-precision contract, how fixtures are found, how a parity test is written, and which error codes each stage owns.

## Layout

| Path | Holds |
| --- | --- |
| `src/CohEngine.Core/Maths/` | `F32` (single-precision quantization) and `MathTables` (the Maths.mhd tables). |
| `src/CohEngine.Core/Data/` | One record per Python dataclass, one source-generated `JsonSerializerContext` per dump family, the family loaders (`EffectData`, `EnhancementData`, `AssemblyData`, `ArchetypeData`, `LevelData`, `SetBonusData`, `TotalsData`, `IncarnateData`). |
| `src/CohEngine.Core/Registry/` | `RuleRegistry`: the legality, hard-limit, enhancement-aspect and metric registries as explicit lists. |
| `src/CohEngine.Core/Expressions/`, `Assembly/`, `Enhancement/`, `SetBonuses/`, `Totals/` | The numeric core; see [Numeric core](#numeric-core). |
| `src/CohEngine.Core/CliRunner.cs` | The whole CLI. `src/CohEngine.Cli/Program.cs` is a one-line shim. |
| `tests/CohEngine.TestSupport/` | `FixtureStore` and `BuildFixture`. They are test-only, so they never ship in the CLI. |
| `tests/CohEngine.Core.Tests/` | `Maths/`, `Data/` (`[Trait("Category", "Fixtures")]`) and `Parity/` (`[Trait("Category", "Parity")]`). |
| `tests/CohEngine.Cli.Tests/` | CLI tests, including the `rules` golden comparison. |
| `tests/fixtures/` | The Mids reference dumps (`mids/`), `attribmod/AttribMod.json`, `Maths.mhd`, and `rules-golden.txt`. Both the Python and the C# suites read them. |

Records keep the Python field names in PascalCase (`n_magnitude` becomes `NMagnitude`) and the Python types.
They are immutable, because the test fixture store shares them across tests.
A field older dumps lack takes its Python default from a `[JsonConstructor]` parameter default, since the source generator writes `default(T)` into an absent init-only property.
A Python `frozenset` is an `IReadOnlySet<T>`, filled from an internal `ISet<T>` that the JSON binds to.
A repeated key keeps the last record, as a Python dict comprehension does.
Expression strings stay strings until they are parsed in P2.
Serialization uses source generation only. Reflection-based `JsonSerializer` calls are not allowed, because the CLI publishes Native AOT.

## The F32 contract

Mids stores its numbers as C# `float`.
The Python port mirrors that with `maths.f32`, which rounds a double to the nearest single, ties to even, and keeps it as a double.
`F32.Round(double)` gives the same result for every input, including `-0.0`, NaN and the infinities.
It throws `OverflowException` for a finite value that would round past the single range, because Python's `struct.pack("f")` refuses that value too.

On load, a field gets `[JsonConverter(typeof(F32JsonConverter))]` exactly when the Python loader wraps it in `f32()`.
A list the Python quantizes cell by cell, such as the totals vectors, gets `F32ListJsonConverter`.
A field the Python loader reads raw, such as the archetype caps or the `enhanced_powers.json` values, stays a plain `double`.
P2 code quantizes at the same points the Python does, and the Python code is the reference for where those points are.

## Finding fixtures

`FixtureStore.Locate(AppContext.BaseDirectory)` walks up from the test assembly to the directory that holds `CohEngine.slnx`, and roots the store at `tests/fixtures`.
Shared database dumps load lazily as properties (`Archetypes`, `EnhancementEffects`, `SetBonuses`, `Levels`, `Maths`, and the rest), and each file is read once.
`Build(name)` returns a `BuildFixture` for `tests/fixtures/mids/builds/<name>/` with one lazy property per file.
`BuildNames()` lists the build directories in ordinal order, so a theory over builds picks up a new dump without a code change.
The test projects share one store through `Fixtures.Store` in `tests/CohEngine.Core.Tests/Fixtures.cs`.

## Writing a parity test

A parity class follows `tests/CohEngine.Core.Tests/Parity/ParityScaffoldTests.cs`:

1. Mark the class `[Trait("Category", "Parity")]`.
2. Make each test a `[Theory]` with `[MemberData(nameof(Builds))]`, where `Builds => Fixtures.Builds()`.
3. Load the expected side from the build's dump through `Fixtures.Store.Build(name)`.
4. Compute the actual side with the engine.
5. Compare every field with `ParityAssert.Equal(expected, actual, path)`, where `path` names the build and the field, for example `"{name}.Totals.Def"`.

`ParityAssert` quantizes only the dumped side with `F32.Round` and compares it to the actual value with `==`, the `f32(want) == got` idiom in `src/coh_engine/tests/test_base_totals_parity.py`.
Engine output must therefore already be single precision, and a missing final quantization fails the test.
`0.0` equals `-0.0`, and NaN equals nothing.
A failure reports the field path and both values, as does a count mismatch (`ParityAssert.Count`) and a dumped value past the single range.
The scaffold compares engine output on every build. A field the Python engine also misses is listed in its `KnownGaps` with the Python engine's value, and the C# must equal that value while still differing from Mids. Effect magnitudes are compared against the Python engine's per-build mismatch count, effect count and a SHA-256 digest of the magnitude sequence, since the engine reads the pre-Pass5 magnitude and differs from `Math_Mag` on most builds. The per-power values neither engine computes (accuracy, range, damage, effect durations) keep the dumped value through its `StandIn` helper.

## The literal-port boundary

Totals/ and Enhancement/AspectRouting are literal ports of MidsReborn, with no "simplify" refactors.
Ported code carries no comments that cite the MidsReborn source; the parity tests prove fidelity.
Everything else follows the registry pattern in `.claude/rules/check-registry.md`.
A new rule is a new entry in an explicit list, and nothing is discovered by reflection.

## Numeric core

The numeric core is laid out on the phases of Mids 4.0's `PlannerPowerPipeline` and `ActorTotalsCalculator`.
Its arithmetic is the Python's, which is Mids 3.x, because the 3.x reference dumps are the only goldens.

| Python | C# | Mids 4.0 phase |
| --- | --- | --- |
| `expressions.py` | `Expressions/Evaluator`, `Parser`, `Conditionals`, `ExpressionContext` | `Expressions.Parse`, `Effect.ValidateConditional`, `Effect.CanInclude` |
| `assembly.py` per power | `Assembly/PowerAssembler`, `PetAbsorption`, `Absorption`, `Chance` | `ExecuteAssemblyPhase`, `GBPA_SubPass0_AssemblePowerEntry` |
| `assembly.py` cross-power | `Assembly/CrossPowerPass` | `GBPA_ApplyCanonicalCrossPowerGlobalBoostEffects` |
| `enhancement.py`, `ed.py`, `enh_pipeline.py` | `Enhancement/SlotValue`, `Ed`, `Pipeline` | `GBPA_Pass1_EnhancePreED`, `GBPA_Pass2_ApplyED` |
| `pass3.py`, `enh_aspects.py` | `Enhancement/AspectRouting`, `GlobalEnhance` | `GBPA_Pass3_EnhancePostED`, `GBPA_Pass4_Add` |
| `set_bonuses.py` | `SetBonuses/Accumulator`, `RuleOfFive`, `ExemplarGate`, `SetSlotting` | `Build.GenerateSetBonusData` |
| `incarnate.py` | `Totals/Incarnate/IncarnateResolver` | `GBPA_ApplyIncarnateEnhancements` |
| `effect.effect_mag` | `Totals/EffectMagnitude` | `Effect.Mag`, `DatabaseAPI.GetModifier` |
| `base_totals._apply_enhance_effects` | `Totals/Phases/EnhancementBucketPhase` | `ExecuteEnhancementBucketPhase` |
| `base_totals._compute_enh_multipliers` | `Totals/Phases/PerPowerEnhancementMathPhase` | `ExecutePerPowerEnhancementMathPhase` |
| `base_totals._apply_buff_effects` | `Totals/Phases/SelfBuffBucketPhase` | `ExecuteSelfBuffBucketPhase` |
| `base_totals._gbd_*` | `Totals/ActorTotalsCalculator` | `ActorTotalsCalculator.Calculate` |
| `base_totals.compute_base_totals` | `Totals/Pipeline.ComputeBaseTotals` | `Toon.GenerateBuffedPowerArray` |
| `statistics.compute_power_stats` | `Totals/PowerStatistics` | per-power buffed scalars |

The quantization rule: every place the Python calls `f32(...)`, the C# calls `F32.Round(...)`, and nowhere else.
Accumulations keep the Python's order, so a sum is a loop, never a LINQ `Sum`, which may vectorize and reorder.
`src/CohEngine.Core/.editorconfig` turns off the simplification suggestions and the method-length rule for `Totals/` and `Enhancement/AspectRouting*`, and leaves the correctness analyzers on.

One divergence is ported as it stands.
An effect carrying `ActiveConditionals` counts only when its conditions hold, and an effect with a `SpecialCase` never counts, because the `CanInclude` character-state switch is unported.
Mids keeps many default-true special cases, so a build where one feeds a read bucket under-reports until #31 lands.
`BaseTotalsParityTests.Excluded_conditionals_do_not_affect_totals` forces every such effect back in through `Pipeline.BuildOptions.IncludeAllConditionals` and checks the committed builds' totals do not move.

## Error codes

Existing codes keep their numbers.
The loaders reuse the Python messages.
`E85` is a record or effect missing a key in `assembly_powers.json`, `assembled_powers.json` or `power_assembly.json`, or a `slots.json` entry without `ProcInclude`.
`E86` is a build power with no record.
Loaders throw typed exceptions (`InvalidDataException`, `FileNotFoundException`, `JsonException`), and diagnostics arrive with P3a.

| Block | Owner |
| --- | --- |
| E100-E109 | P3b |
| E110-E119 | P3c |
| E120 and up | CP work |

## Chain planner

The `chain` command plans a build's attack rotation and layers the damage of auras, patches, pets, click buffs and debuffs over it.
The C# port prints byte-identical text and JSON to the Python on every sample build.
`tests/fixtures/cli/chain/README.md` says how the Python captures were made.

| Python | C# | Holds |
| --- | --- | --- |
| `damage_model.py` | `DamageLayers/DamageModel` | The model records, the `Stance`, `Role`, `Shape` and `Delivery` enums, and `Evaluate`. |
| `damage_sources.py` | `DamageLayers/DamageSources` | The chain, aura, patch and buff-cast sources. |
| `damage_modifiers.py`, `at_modifiers.py` | `DamageLayers/DamageModifiers`, `AtModifiers` | Click buffs, target debuffs and the archetype inherents. |
| `damage_plugins.py` | `DamageLayers/DamagePlugins` | The default source and modifier lists, in the Python registration order. |
| `damage_reference.py` | `DamageLayers/DamageReference` | The Mids-computed damage figures that stand in for the unported damage port. |
| `attack_profile.py` | `Chain/AttackProfiles`, `CanonicalJson` | The classifier and the canonical power reader. |
| `attack_chain.py` | `Chain/AttackChain` | The sequencer and the subset planner. |
| `pet_damage.py` | `Pets/PetDamage` | Summons, pets and the pet source. |
| `endurance_budget.py` | `Statistics/EnduranceBudgets` | The endurance and health budgets and the `totals.json` reader. |
| `power_names.py` | `Names/PowerNames` | The internal and display name resolver. |
| `chain_report.py`, `__main__.py` chain | `Chain/ChainReport`, `ChainCommand`, `PyJson` | Setup, rules, the text and JSON renderers and the command. |

The namespace of the damage folder is `DamageLayers`, because a `Damage` namespace shadows identifiers the numeric core's tests already use.
The command's logic lives in Core as `ChainCommand`, like `CliRunner`, so the test projects can reach it and the coverage gate measures it.
`Program.cs` routes `chain` to it with one line.

### Printing floats

The report prints Python text, so `PyFloat` reproduces it instead of approximating it.
`Fixed` rounds the exact binary value half to even, which is what `format(x, ".1f")` does, and .NET's own formatter does not.
`Repr` gives the shortest text that reads back to the same double, in Python's layout: `1e-05`, `1e+16`, `1.0`.
`General` is `format(x, "g")`.
`Sum` is the `sum()` of Python 3.12 and later, which adds floats with Neumaier compensation, so a left-to-right loop would differ in the last digit.
JSON goes through `PyJson` rather than a source-generated context, because the output must keep `0` apart from `0.0`, escape every non-ASCII character, and keep the Python key order.

### Left as stubs

`PowerDps.Compute` still fails with `E15`, as `compute_power_dps` does.
Damage per activation is the Mids reference figure until the `FXGetDamageValue` port lands (#31).
Pet AI, form-locked auras and incarnate summons stay unmodelled, as in the Python (#70 to #73).
