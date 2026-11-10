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

## Exact float comparison

Rule: `cs/equality-on-floats`.
Compare through `Exact.Same` in `src/CohEngine.Core/Maths/Exact.cs`; never write `==` or `!=` on a float or double.
The port reproduces Python's `==`: NaN equals nothing, and 0.0 equals -0.0.
A tolerance changes which branch runs, and a bit comparison treats NaN as equal to itself and 0.0 as different from -0.0, so either changes the captured output.

```csharp
// before
if (divisor != 0)
// after
if (!Exact.Same(divisor, 0))
```

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
CodeQL ignores `paths-ignore` for a language analysed with a build, so `scripts/ci/filter_sarif.py` removes results whose path has an `obj` segment from the C# results before upload.

## Deliberate exceptions

An alert that cannot be fixed without changing ported behaviour is dismissed on the Security tab with a written reason.
It is never suppressed with a comment or a query filter in code.

- `src/CohEngine.Core/Maths/Exact.cs:16`: the one exact float comparison; the rest of the code calls it.
- `src/CohEngine.Core/Assembly/CrossPowerPass.cs:167`: a complex condition kept as the literal port of the Mids branch order.
- `src/CohEngine.Core/Totals/ActorTotalsCalculator.cs:101`: the loop calls `Mag` for every Fly effect, and `Mag` can throw for an unported expression.
  `Any` stops at the first match, so it would change which effects are evaluated.
- `src/CohEngine.Core/Enhancement/SlotValue.cs:84-85`: `!(mag <= 0.0)` and `!(mag >= 0.0)` are true for NaN, which `mag > 0.0` and `mag < 0.0` are not, and the long condition keeps Mids' branch order.
