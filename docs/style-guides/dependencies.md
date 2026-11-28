# NuGet dependencies

Every package pinned in [`Directory.Packages.props`](../../Directory.Packages.props), with its licence and Native AOT status.
A package enters that file only after it has a row here.

The project is GPL-3.0-only.
A package's licence must allow distribution under GPL-3.0: permissive licences such as MIT, BSD, ISC, Apache-2.0 and CC0 qualify, and a licence that forbids GPL distribution does not.

Licences were read from each project's `LICENSE` file on its default branch on 2026-11-07, and those of SonarAnalyzer.CSharp, dotnet-coverage and ReportGenerator on 2026-11-27.
They are given as SPDX identifiers where one exists.
Native AOT status is the `IsAotCompatible` property in the package's own project files.
"Not shipped" means the package is a build-time analyzer, build tool or test framework and never reaches the published binary, so AOT does not apply.

| Package | Version | Licence | Native AOT | Source |
| --- | --- | --- | --- | --- |
| StyleCop.Analyzers | 1.3.180 | MIT | Not shipped (analyzer, every project) | <https://github.com/DotNetAnalyzers/StyleCopAnalyzers> |
| Meziantou.Analyzer | 3.0.341 | MIT | Not shipped (analyzer, every project) | <https://github.com/meziantou/Meziantou.Analyzer> |
| SonarAnalyzer.CSharp | 10.36.0.15337 | Sonar Source-Available License v1.0 (no SPDX identifier) | Not shipped (analyzer, every project) | <https://github.com/SonarSource/sonar-dotnet> |
| MinVer | 8.0.0 | Apache-2.0 | Not shipped (build tool, every project) | <https://github.com/adamralph/minver> |
| xunit.v3.mtp-v2 | 4.0.3 | Apache-2.0 | Not shipped (test framework, the five test projects) | <https://github.com/xunit/xunit> |
| Microsoft.CodeAnalysis.CSharp | 4.14.0 | MIT | Not shipped (CohEngine.Analyzers and its tests) | <https://github.com/dotnet/roslyn> |
| Microsoft.Extensions.AI | 11.0.0 | MIT | Compatible (net7.0 and later) | <https://github.com/dotnet/extensions> |
| Microsoft.Extensions.AI.OpenAI | 11.0.0 | MIT | Compatible (net7.0 and later) | <https://github.com/dotnet/extensions> |
| OpenAI | 2.15.0 | MIT | Compatible (net7.0 and later) | <https://github.com/openai/openai-dotnet> |
| Microsoft.Extensions.DependencyInjection | 10.0.15 | MIT | Compatible (net10.0) | <https://github.com/dotnet/runtime> |
| ModelContextProtocol | 2.4.0 | Apache-2.0 AND MIT | Compatible (net10.0) | <https://github.com/modelcontextprotocol/csharp-sdk> |
| ModelContextProtocol.Core | 2.4.0 | Apache-2.0 AND MIT | Compatible (net10.0) | <https://github.com/modelcontextprotocol/csharp-sdk> |
| YamlDotNet | 18.1.0 | MIT | Not shipped (CohEngine.Analyzers.Tests) | <https://github.com/aaubry/YamlDotNet> |
| Tomlyn | 2.11.1 | BSD-2-Clause | Not shipped (the text-check script's TOML parser) | <https://github.com/xoofx/Tomlyn> |

CohEngine.Ai references Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI and OpenAI.
CohEngine.Mcp references ModelContextProtocol, ModelContextProtocol.Core and Microsoft.Extensions.DependencyInjection.
Both ship in the CLI.
SonarAnalyzer.CSharp's licence is source-available, not one of the permissive licences above; it runs in every build as an analyzer and never reaches the published binary.

## Dotnet tools

Tools pinned in [`.config/dotnet-tools.json`](../../.config/dotnet-tools.json) never reach the published binary.

| Tool | Version | Licence | Source |
| --- | --- | --- | --- |
| dotnet-sonarscanner | 12.1.0 | LGPL-3.0 | <https://github.com/SonarSource/sonar-scanner-msbuild> |
| dotnet-coverage | 18.12.0 | Microsoft Software License Terms (the package's `License.txt`) | <https://www.nuget.org/packages/dotnet-coverage> |
| dotnet-reportgenerator-globaltool | 5.6.0 | Apache-2.0 | <https://github.com/danielpalme/ReportGenerator> |
| husky (Husky.Net) | 0.9.1 | MIT | <https://github.com/alirezanet/Husky.Net> |
| CycloneDX (cyclonedx-dotnet) | 6.3.1 | Apache-2.0 | <https://github.com/CycloneDX/cyclonedx-dotnet> |

dotnet-sonarscanner runs only in the `sonar` workflow.
Nothing links to it and nothing it produces ships, so its LGPL-3.0 terms do not reach the GPL-3.0-only product.

dotnet-coverage and ReportGenerator measure coverage in the `build` and `sonar` workflows; nothing they produce ships.

CycloneDX writes the SBOM each release carries, in the `build` and `release` workflows; its licence was read from its repository on 2026-11-27.
Nothing links to it and nothing it produces ships in the binary.

husky runs the Git hooks in `.husky/` on a developer machine and never in CI.
Nothing links to it and nothing it produces ships.

## Notes

- The Microsoft.Extensions.AI packages set `IsAotCompatible` in `src/Libraries/Directory.Build.props`, and OpenAI sets it in its root `Directory.Build.props`.
- Tomlyn's licence was read from `license.txt` on its default branch on 2026-11-27.
  It is pinned in `Directory.Packages.props` like every other package, because central package management refuses a version on the script's `#:package` directive.
- ModelContextProtocol is moving from MIT to Apache-2.0.
  New contributions are Apache-2.0, and contributions whose authors have not consented to relicensing stay MIT.
  Both are permissive licences.

## Excluded

- `Anthropic`, the official Anthropic NuGet package, is excluded because it is not Native AOT compatible.
  The tracker issue, [anthropics/anthropic-sdk-csharp issue 79](https://github.com/anthropics/anthropic-sdk-csharp/issues/79) ("Support AOT"), is open.

## SDK pin

`global.json` pins SDK 10.0.404 exactly (`rollForward: disable`).
The SDK adds the AOT compiler and linker packages at the runtime version it bundles, and the CLI's lock file records them.
A newer SDK patch would therefore fail every locked restore with NU1004.
The 10.0.3xx band resolves Microsoft.DotNet.ILCompiler 10.0.11, which carries GHSA-p3jp-9v83-76w9 (NU1903, fixed in 10.0.14) and fails restore under TreatWarningsAsErrors.
The 10.0.4xx band resolves a patched ILCompiler (10.0.15 with SDK 10.0.404).
