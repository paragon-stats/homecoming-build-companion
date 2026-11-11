# NuGet dependencies

Every package pinned in [`Directory.Packages.props`](../../Directory.Packages.props), with its licence and Native AOT status.
A package enters that file only after it has a row here.

The project is GPL-3.0-only.
A package's licence must allow distribution under GPL-3.0: permissive licences such as MIT, BSD, ISC, Apache-2.0 and CC0 qualify, and a licence that forbids GPL distribution does not.

Licences were read from each project's `LICENSE` file on its default branch on 2026-11-07 and are given as SPDX identifiers.
Native AOT status is the `IsAotCompatible` property in the package's own project files.
"Not shipped" means the package is a build-time analyzer, build tool or test framework and never reaches the published binary, so AOT does not apply.

| Package | Version | Licence | Native AOT | Source |
| --- | --- | --- | --- | --- |
| StyleCop.Analyzers | 1.2.0-beta.556 | MIT | Not shipped (analyzer) | <https://github.com/DotNetAnalyzers/StyleCopAnalyzers> |
| Meziantou.Analyzer | 3.0.192 | MIT | Not shipped (analyzer) | <https://github.com/meziantou/Meziantou.Analyzer> |
| MinVer | 8.0.0 | Apache-2.0 | Not shipped (build tool) | <https://github.com/adamralph/minver> |
| xunit.v3.mtp-v2 | 4.0.1 | Apache-2.0 | Not shipped (test framework) | <https://github.com/xunit/xunit> |
| Microsoft.CodeAnalysis.CSharp | 4.14.0 | MIT | Not shipped (analyzer and its tests) | <https://github.com/dotnet/roslyn> |
| Microsoft.Extensions.AI | 10.11.0 | MIT | Compatible (net7.0 and later) | <https://github.com/dotnet/extensions> |
| Microsoft.Extensions.AI.OpenAI | 10.11.0 | MIT | Compatible (net7.0 and later) | <https://github.com/dotnet/extensions> |
| OpenAI | 2.15.0 | MIT | Compatible (net7.0 and later) | <https://github.com/openai/openai-dotnet> |
| Microsoft.Extensions.DependencyInjection | 10.0.14 | MIT | Compatible (net10.0) | <https://github.com/dotnet/runtime> |
| ModelContextProtocol | 2.4.0 | Apache-2.0 AND MIT | Compatible (net10.0) | <https://github.com/modelcontextprotocol/csharp-sdk> |
| ModelContextProtocol.Core | 2.4.0 | Apache-2.0 AND MIT | Compatible (net10.0) | <https://github.com/modelcontextprotocol/csharp-sdk> |

The last five packages are pinned ahead of the stages that will reference them, so those stages do not edit the pin file.
Nothing references them yet.

## Notes

- The Microsoft.Extensions.AI packages set `IsAotCompatible` in `src/Libraries/Directory.Build.props`, and OpenAI sets it in its root `Directory.Build.props`.
- ModelContextProtocol is moving from MIT to Apache-2.0.
  New contributions are Apache-2.0, and contributions whose authors have not consented to relicensing stay MIT.
  Both are permissive licences.

## Excluded

- `Anthropic`, the official Anthropic NuGet package, is excluded because it is not Native AOT compatible.
  The tracker issue, [anthropics/anthropic-sdk-csharp issue 79](https://github.com/anthropics/anthropic-sdk-csharp/issues/79) ("Support AOT"), is open.

## SDK pin

`global.json` pins SDK 10.0.400 with `latestPatch`.
The 10.0.3xx band resolves Microsoft.DotNet.ILCompiler 10.0.11, which carries GHSA-p3jp-9v83-76w9 (NU1903, fixed in 10.0.14) and fails restore under TreatWarningsAsErrors.
The 10.0.4xx band resolves a patched ILCompiler.
