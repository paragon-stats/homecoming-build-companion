# .NET port of the build engine

The C# engine in `CohEngine.slnx` is a port of the Python engine in `src/coh_engine/`.
It is checked against the same Mids reference dumps, value for value.
This page covers its layout, the single-precision contract, how fixtures are found, how a parity test is written, and which error codes each stage owns.

## Layout

| Path | Holds |
| --- | --- |
| `src/CohEngine.Core/Maths/` | `F32` (single-precision quantization) and `MathTables` (the Maths.mhd tables). |
| `src/CohEngine.Core/Data/` | One record per Python dataclass, one source-generated `JsonSerializerContext` per dump family, the family loaders (`EffectData`, `EnhancementData`, `AssemblyData`, `ArchetypeData`, `LevelData`, `SetBonusData`, `TotalsData`, `IncarnateData`), and `FixtureStore`. |
| `src/CohEngine.Core/Registry/` | `RuleRegistry`: the legality, hard-limit, enhancement-aspect and metric registries as explicit lists. |
| `src/CohEngine.Core/CliRunner.cs` | The whole CLI. `src/CohEngine.Cli/Program.cs` is a one-line shim. |
| `tests/CohEngine.Core.Tests/` | `Maths/`, `Data/` (`[Trait("Category", "Fixtures")]`) and `Parity/` (`[Trait("Category", "Parity")]`). |
| `tests/CohEngine.Cli.Tests/` | CLI tests, including the `rules` golden comparison. |
| `tests/fixtures/` | The Mids reference dumps (`mids/`), `Maths.mhd`, and `rules-golden.txt`. Both the Python and the C# suites read them. |

Records keep the Python field names in PascalCase (`n_magnitude` becomes `NMagnitude`) and the Python types.
Expression strings stay strings until they are parsed in P2.
Serialization uses source generation only. Reflection-based `JsonSerializer` calls are not allowed, because the CLI publishes Native AOT.

## The F32 contract

Mids stores its numbers as C# `float`.
The Python port mirrors that with `maths.f32`, which rounds a double to the nearest single, ties to even, and keeps it as a double.
`F32.Round(double)` gives the same result for every input, including `-0.0`, NaN and the infinities.
It throws `OverflowException` for a finite value that would round past the single range, because Python's `struct.pack("f")` refuses that value too.

On load, a field gets `[JsonConverter(typeof(F32JsonConverter))]` exactly when the Python loader wraps it in `f32()`.
A field the Python reads raw, such as the archetype caps or the dumped totals, stays a plain `double`.
P2 code quantizes at the same points the Python does, and the Python code is the reference for where those points are.

## Finding fixtures

`FixtureStore.Locate(AppContext.BaseDirectory)` walks up from the test assembly to the directory that holds `CohEngine.slnx`, and roots the store at `tests/fixtures`.
Shared database dumps load lazily as properties (`Archetypes`, `EnhancementEffects`, `SetBonuses`, `Levels`, `Maths`, and the rest), and each file is read once.
`Build(name)` returns a `BuildFixture` for `tests/fixtures/mids/builds/<name>/` with one lazy property per file.
`BuildNames()` lists the build directories in ordinal order, so a theory over builds picks up a new dump without a code change.
The test projects share one store through `Fixtures.Store` in `tests/CohEngine.Core.Tests/Fixtures.cs`.

`AttribMod.json` still sits at `src/coh_engine/tests/fixtures/attribmod/`, and `FixtureStore.AttribMods` reads it from there.

## Writing a parity test

A parity class follows `tests/CohEngine.Core.Tests/Parity/ParityScaffoldTests.cs`:

1. Mark the class `[Trait("Category", "Parity")]`.
2. Make each test a `[Theory]` with `[MemberData(nameof(Builds))]`, where `Builds => Fixtures.Builds()`.
3. Load the expected side from the build's dump through `Fixtures.Store.Build(name)`.
4. Compute the actual side with the engine.
5. Compare every field with `ParityAssert.Equal(expected, actual, path)`, where `path` names the build and the field, for example `"{name}.Totals.Def"`.

`ParityAssert` passes both sides through `F32.Round` and compares them with `==`, which is the idiom in `src/coh_engine/tests/test_base_totals_parity.py`.
So `0.0` equals `-0.0`, and NaN equals nothing.
A failure reports the field path and both values.
Today the scaffold compares each dump with itself, and P2 replaces the actual side with engine output.

## The literal-port boundary

Totals/ and Enhancement/AspectRouting are literal ports of MidsReborn, with no "simplify" refactors.
Ported code carries no comments that cite the MidsReborn source; the parity tests prove fidelity.
Everything else follows the registry pattern in `.claude/rules/check-registry.md`.
A new rule is a new entry in an explicit list, and nothing is discovered by reflection.

## Error codes

Existing codes keep their numbers.
The loaders reuse the Python messages: `E85` for a missing extended assembly key and `E86` for a build power with no record.
Loaders throw typed exceptions (`InvalidDataException`, `FileNotFoundException`, `JsonException`), and diagnostics arrive with P3a.

| Block | Owner |
| --- | --- |
| E100-E109 | P3b |
| E110-E119 | P3c |
| E120 and up | CP work |
