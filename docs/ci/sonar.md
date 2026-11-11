# SonarQube Cloud analysis

The `sonar` workflow (`.github/workflows/sonar.yml`) analyses the repository with SonarQube Cloud on every pull request to `main` and every push to `main`.
It follows the workflow in paragon-stats/paragon-stats.

## What is analysed

- The .NET solution, through the MSBuild scanner: begin, build, tests with coverage, end.
- Coverage comes from `coverage.xml`, the Visual Studio XML report `dotnet-coverage` writes for the `build` workflow's 100% coverage gate.
  The scanner reads it through `sonar.cs.vscoveragexml.reportsPaths`.
- The remaining Python (`scripts/**`, `src/coh_engine`) is analysed with `sonar.python.version=3.14`.
  It has no coverage in this job: the Python coverage workflow writes no coverage XML, and re-running its matrix here would double the cost.

## What is excluded

| Property | Pattern | Why |
| --- | --- | --- |
| `sonar.exclusions` and `sonar.cpd.exclusions` | `data/**`, `**/tests/fixtures/**`, `samples/**`, `knowledge/**` | Generated or third-party data, not code under review. The fixture pattern matches `tests/fixtures` at any depth: captured pages and build files used as test input. |
| same | `**/obj/**`, `**/bin/**` | Build output. |
| same | `tools/mids-dump*/**` | The Mids dump harness is outside the solution, so this job does not build it. |
| `sonar.coverage.exclusions` | `**/*JsonContext.cs` | Source-generated JSON contexts, marked `[ExcludeFromCodeCoverage]`, which the 100% gate leaves out too. |
| same | `scripts/**`, `src/coh_engine/**` | Python with no coverage report in this job; left in, it would count as uncovered. |

Sonar's coverage figure and the repository's gate therefore measure the same .NET code.

## How the quality gate blocks

The begin step sets `sonar.qualitygate.wait=true`, so the end step waits for SonarQube Cloud and fails the `sonar` job when the project's quality gate fails.
A failed job is a failed `sonar` check.

Pull requests from forks cannot read `SONAR_TOKEN`, so the job is skipped for them and reports as skipped.
Dependabot pull requests run the scan, using the organisation's Dependabot secret of the same name.

## One-time setup (operator)

1. In SonarQube Cloud, organisation `paragon-stats`, create the project `paragon-stats_homecoming-build-companion` and bind it to this repository.
2. Choose its quality gate.
3. The organisation secret `SONAR_TOKEN` must be visible to this repository and, for Dependabot pull requests, present in the organisation's Dependabot secrets.
4. Open the pull request and wait for the first green `sonar` run.

## Make the check required

Only after the first green `sonar` run: the ruleset change is a separate commit, and the operator applies it with the command in [`github-rulesets/README.md`](github-rulesets/README.md).
Requiring a check that has never reported freezes every pull request.

## First-run failures

- Project not created yet: the begin step fails with a message that the project key `paragon-stats_homecoming-build-companion` was not found or that the token has no permission on it.
  Create and bind the project, then re-run the job.
- Coverage report not found: the end step logs a warning that no coverage report matched `coverage.xml`, and the project shows 0% coverage.
  Check that the "Test with coverage" step wrote `coverage.xml` in the workspace root.
