# C# patterns code scanning rejects

CodeQL (`security-and-quality`) runs on every pull request, and a pull request that adds an alert cannot merge.
These are the patterns that produced the bulk of the alerts. Write the second form.

## Casts to a value's own type

Rule: `cs/useless-cast-to-self`.
Delete the cast. When a collection expression needs a target type, give the generic arguments instead of casting.

```csharp
// before
.Select(group => (IReadOnlyList<string>)[.. group.Where(keep)])
// after
.Select<IReadOnlyList<string>, IReadOnlyList<string>>(group => [.. group.Where(keep)])
```

## Joining paths

Rule: `cs/path-combine`.
Use `Path.Join`. `Path.Combine` silently drops everything before a rooted second argument.

```csharp
// before
Path.Combine(Store.MidsDirectory, "enhancements.json")
// after
Path.Join(Store.MidsDirectory, "enhancements.json")
```

## Float comparison

Rules: `cs/equality-on-floats`, Sonar `S1244`.
Never write `==` or `!=` on a float or double. Write the comparison the code means:

- A quantity that cannot be negative is tested with `> 0` or `<= 0`, once its source shows it cannot be negative.
- A value on the float32 grid, one `F32.Round` produced or a sum of such values, is tested with `F32.IsZero`.
  Every such value is a whole multiple of 2^-149, so half that separates zero from all of them and the test is exact.
- Where the representation is the question, such as a shortest round-trip spelling, compare bit patterns with `BitConverter.DoubleToInt64Bits`.
- Exact IEEE equality, only where that is the meaning, goes through `Exact.Same` in `src/CohEngine.Core/Maths/Exact.cs`.
  It reproduces Python's `==`: NaN equals nothing, and 0.0 equals -0.0.

```csharp
// before
if (accumulator != 0)
// after
if (!F32.IsZero(accumulator))
```

The callers of `Exact.Same` are the ones whose meaning is exact equality on arbitrary doubles:
the division guard and the `and_`/`or_` truth test in `Expressions/Evaluator.cs`, the profile `==` operator in `Registry/ScoringMetrics/Scoring.cs`,
the summation compensation and the zero case of `General` in `Chain/PyFloat.cs`, and the zero case of `NumberRepr` in `Diagnostics/PyText.cs`.

## Filtering inside a loop

Rules: `cs/linq/missed-where`, `cs/linq/missed-select`.
Put the condition in `Where` and the projection in `Select`, and keep the loop body for the work.
Evaluation order does not change, because `Where` is lazy.

```csharp
// before
foreach (SubPower sub in buildPower.SubPowers) { if (sub.StatInclude) { ... } }
// after
foreach (SubPower sub in buildPower.SubPowers.Where(sub => sub.StatInclude)) { ... }
```

## Disposal

Rule: `cs/local-not-disposed`.
Declare every `IDisposable` local (readers, streams, `JsonDocument`, `HttpClient`, messages, `CancellationTokenSource`) with `using`, so a throw cannot skip the dispose.

```csharp
using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
```

## Other patterns

- Useless assignment to a local (`cs/useless-assignment-to-local`): delete it.
- Empty block or loop body (`cs/empty-block`): remove it, or write the statement it stands for.
- String concatenation in a loop (`cs/string-concatenation-in-loop`): use `string.Join` or `StringBuilder`.
- Nested `if` that can be one (`cs/nested-if-statements`): combine them outside literal-port arithmetic.

## Generated code

Findings under `obj/` (JSON source-generator output, the xUnit entry point) are not ours to edit.
CodeQL ignores `paths-ignore` for a language analysed with a build, so `scripts/ci/filter-sarif.cs` removes results whose path has an `obj` segment from the C# results before upload.

## Deliberate exceptions

An alert that cannot be fixed without changing ported behaviour is dismissed on the Security tab with a written reason.
It is never suppressed with a comment or a query filter in code.

- `src/CohEngine.Core/Maths/Exact.cs:16` (CodeQL alert 356, dismissed; Sonar `S1244`, ignored for this file): the one exact float comparison.
  The Sonar exclusion is a `sonar.issue.ignore.multicriteria` entry in `.github/workflows/sonar.yml`; [the Sonar page](../ci/sonar.md) lists it.
  Its callers, listed under Float comparison, need exact IEEE equality; every other comparison states its meaning without it.
- `src/CohEngine.Core/Assembly/CrossPowerPass.cs:167`: a complex condition kept as the literal port of the Mids branch order.
- `src/CohEngine.Core/Totals/ActorTotalsCalculator.cs:101`: the loop calls `Mag` for every Fly effect, and `Mag` can throw for an unported expression.
  `Any` stops at the first match, so it would change which effects are evaluated.
- `src/CohEngine.Core/Enhancement/SlotValue.cs:84-85`: `!(mag <= 0.0)` and `!(mag >= 0.0)` are true for NaN, which `mag > 0.0` and `mag < 0.0` are not, and the long condition keeps Mids' branch order.
