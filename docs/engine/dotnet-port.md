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
Everything else follows the registry pattern in `docs/style-guides/engine-architecture.md`.
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

## Rule layer and CLI

The rule layer follows `docs/style-guides/engine-architecture.md`: the context is built once, a rule is a pure function of it, and the runner folds the registered rules.
A Python decorator becomes one line in an explicit list in `Registry/RuleRegistry.cs`, in the order the Python registered them.
Nothing is found by reflection.

| Python | C# registration | Rule body |
| --- | --- | --- |
| `@legality_rule` in `legality.py` | `RuleRegistry.LegalityRules`, run by `LegalityRunner` | `Registry/LegalityRules/*Rule.cs` |
| `@hard_limit_rule` in `hard_limits.py` | `RuleRegistry.HardLimitRules`, run by `HardLimitRunner` | `Registry/HardLimitRules/*Rule.cs` |
| `@metric` in `scoring.py` | `RuleRegistry.ScoringMetrics`, resolved by `Scoring.MetricValueOf` | `Registry/ScoringMetrics/*Metric.cs` |
| `@_register` in `enh_aspects.py` | `RuleRegistry.EnhancementAspects` (identities only; the handlers are `Enhancement/AspectRouting`) | `Enhancement/` |
| `diagnostics.py` | `Diagnostics/Diagnostic`, `DiagnosticFormatter` | |
| `profiles.py`, `at_names.py` | `Profiles/ProfileResolver`, `AtNames` | |
| `exemplar.py` | `Exemplar/ExemplarWindow` | |
| `__main__.py` `rules`, `validate` | `CliRunner`, `ValidateArguments`, `Registry/ValidateCommand` | |

`rules` lists the registries from `RuleInfo`, so the listing and the rule bodies cannot drift.
`validate` takes a Mids dump directory.
A `.mbd` file goes through `IMbdBuildSource`, which the build-file reader implements; with no source `validate` reports the form as unavailable (E72).
`scoring.score_build` takes the attack chain's endurance per second as an argument, because the chain planner is a separate port.

Messages, fixes and the expected and actual values are the Python strings.
`Diagnostics/PyText` prints Python's `repr`, `str` and float repr, and `DiagnosticFormatter` writes the JSON by hand to match `json.dumps(indent=2)`: ASCII only, lowercase `\u` escapes.
The stock JSON writer escapes `'` and `+` and writes non-ASCII differently, so it cannot reach byte equality.

The CLI parity method: `tests/fixtures/cli/` holds the Python engine's stdout and exit code for `rules` and for `validate` in both formats over every build under `tests/fixtures/mids/builds/`.
`CliParityTests` runs the same commands in process and compares the text exactly.
The folder's README gives the capture commands.
A new sample build needs a new capture, and `Every_sample_build_has_a_capture` fails until it has one.

## Error codes

Existing codes keep their numbers.
The loaders reuse the Python messages.
`E85` is a record or effect missing a key in `assembly_powers.json`, `assembled_powers.json` or `power_assembly.json`, or a `slots.json` entry without `ProcInclude`.
`E86` is a build power with no record.
Loaders throw typed exceptions (`InvalidDataException`, `FileNotFoundException`, `JsonException`).
`Diagnostics/ErrorCodes.cs` lists every code the Python uses, and `EngineException` carries a message that already starts with its code.
The CLI prints such a message as `error: <message>` on stderr and exits 1; a missing file, or a dump of the wrong shape, is E72.

| Block | Owner |
| --- | --- |
| E100-E109 | P3b |
| E110-E119 | P3c |
| E120 and up | CP work |

## Build files

`src/CohEngine.Core/BuildFile/` reads the two build formats and writes the modern one.
The Python it ports is `src/coh_engine/buildfile/`.

| Format | Reader | Notes |
| --- | --- | --- |
| `.mbd` | `Mbd.LoadMbd`, `Mbd.ParseMbd` | Plain JSON, read through `BuildFileJsonContext` into `MbdDocument`, then into `MbdBuild`. |
| `.mxd` | `Mxd.ReadMxd`, `Mxd.ReadMxdBuild` | A share block of hex-coded zlib, then the little-endian stream `BinaryCursor` reads. |
| Static indexes | `DbIndex` | `StaticIndex` to power name and to enhancement identity; negative indexes are dropped. |

The model keeps the engine's levels, not the file's.
A power's and a slot's stored `Level` is one more than the model's, so the reader subtracts one and the writer adds it back.
A placeholder entry (empty `PowerName`) keeps its stored level.
`LastPower` is raw on a `.mbd`.
The header `Level` is the character level minus one, stored as text, and a JSON number is accepted on read.
`.mxd` levels are the stored 0-based values, with no offset.

A `.mbd` has no `Chosen` flag, so `Mbd.PowersFromBuild` leaves `Chosen` true on every entry.
A pool sub-power that Mids grants without a pick (Translocation from Mystic Flight, Double Jump) therefore counts as one of the 24 picks, and a pick-count check can over-count a build that holds one.

The writer's byte contract, which `RoundTripTests` pins to the Python writer's output for every sample and fixture build:

- Two-space indent, LF line endings, one final newline.
- UTF-8 without a BOM.
- Keys in the order the document types declare them, which is the order `json.dumps` was given.
- Every character past `~` as a lowercase `\uXXXX` escape (surrogate pairs as two), as `json.dumps` does with `ensure_ascii`.
  `System.Text.Json` has no built-in encoder for that, and a custom one needs `unsafe`, so `Mbd.Serialize` rewrites the relaxed-escaping output.
- `<`, `>`, `&`, `'` and `+` stay literal.

The document types use settable properties.
The source generator would write `default(T)` into an absent init-only property, which breaks the defaults an older file relies on.
Keys the Python reads with `obj["Key"]` are nullable in the document type and checked in `ParseMbd`, which raises `E118`.

The writer (`Mbd.WriteMbd`) walks a `BuildSpec` level by level, places added slots against the level schedule, and checks the result with `Mbd.CheckMbdInvariants` before returning it.
It keeps the Python's `E46` to `E49` and `E73` to `E78` codes and messages.

Errors that the Python raised as a bare `ValueError` now carry codes `E110` to `E119`, each prefixing its message and ending in a fix clause:

| Code | Meaning |
| --- | --- |
| `E110` | A read ran past the end of a binary buffer. |
| `E111` | A 7-bit length prefix ran past five groups. |
| `E112` | A share block lacks its header or payload line, or the header is malformed. |
| `E113` | A share payload is not decodable: wrong format, bad hex, size mismatch or a zlib failure. |
| `E114` | The `.mxd` magic number is absent. |
| `E115` | The `.mxd` was saved by a newer version. |
| `E116` | A qualified-names `.mxd`, or a class UID the archetype table lacks. |
| `E117` | An enum ordinal or name is outside its table. |
| `E118` | A `.mbd` or dump file is unreadable: invalid JSON, a missing required key or the `BuiltWith` block missing. |
| `E119` | A length-prefixed string is not valid UTF-8. |
